using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using dev;
using Microsoft.EntityFrameworkCore;

namespace Coflnet.Sky.Core
{
    public partial class ItemPrices
    {
        public static ItemPrices Instance;

        /// <summary>
        /// Filterhook for the commands module
        /// </summary>
        public static Func<IQueryable<SaveAuction>, Dictionary<string, string>, IQueryable<SaveAuction>> AddFilters;

        private const string INTRA_HOUR_PREFIX = "IPH";
        private const string INTRA_DAY_PREFIX = "IPD";

        private Dictionary<int, bool> BazzarItem = new Dictionary<int, bool>();

        private bool IsFilterable(int itemId)
        {
            // TODO: find historic enchantments in the db to determine filterability by enchants and reforges
            return !IsBazaar(itemId);
        }

        public bool IsBazaar(int itemId)
        {
            return BazzarItem.ContainsKey(itemId);
        }

        public async Task<Resonse> GetPriceFor(ItemSearchQuery details)
        {
            var itemId = ItemDetails.Instance.GetItemIdForTag(details.name, false);
            var itemTag = details.name;

            if (details.Reforge != ItemReferences.Reforge.Any
                    || (details.Enchantments != null && details.Enchantments.Count != 0)
                    || details.Tier != Tier.UNKNOWN
                    || details.Filter != null)
                return await QueryDB(details);


            if (details.Start > DateTime.Now - TimeSpan.FromHours(1.1))
                return await RespondIntraHour(itemId, itemTag);


            if (details.Start > DateTime.Now - TimeSpan.FromDays(1.01))
                return await RespondHourly(itemId, itemTag);

            using (var context = new HypixelContext())
            {
                Console.WriteLine("query prices db ");
                var response = await context.Prices
                                .Where(p => p.ItemId == itemId && p.Date > details.Start && p.Date <= details.End).ToListAsync();

                return FromList(response, itemTag);
            }
        }

        protected virtual async Task<Resonse> RespondIntraHour(int itemId, string tag)
        {
            ItemLookup res = await GetHourlyLookup(itemId);
            if (res != null)
                return FromItemLookup(res, tag);
            throw new CoflnetException("404", "there was no data found for this item. retry in a miniute");
        }

        private static async Task<ItemLookup> GetHourlyLookup(int itemId)
        {
            var key = GetIntraHourKey(itemId);
            var res = await CacheService.Instance.GetFromRedis<ItemLookup>(key);
            return res;
        }

        private static string GetIntraHourKey(int itemId)
        {
            return INTRA_HOUR_PREFIX + DateTime.Now.Hour + itemId;
        }

        protected virtual async Task<Resonse> RespondHourly(int itemId, string tag)
        {
            /* cache usage disabled
            ItemLookup res = await GetLookupForToday(itemId);
            if (res != null)
                return FromItemLookup(res, tag, (await GetHourlyLookup(itemId))?.CombineIntoOne(default(DateTime), DateTime.Now));
                */

            return await QueryDB(new ItemSearchQuery()
            {
                End = DateTime.Now,
                Start = DateTime.Now - TimeSpan.FromDays(1.09),
                name = tag
            });
        }

        public static async Task<ItemLookup> GetLookupForToday(int itemId)
        {
            var key = GetIntradayKey(itemId);
            var res = await CacheService.Instance.GetFromRedis<ItemLookup>(key);
            return res;
        }

        private static string GetIntradayKey(int itemId)
        {
            return INTRA_DAY_PREFIX + itemId;
        }

        private Resonse FromItemLookup(ItemLookup value, string itemTag, AveragePrice additional = null)
        {
            return FromList(additional == null || additional.Volume == 0 ? value.Prices : value.Prices.Append(additional), itemTag);
        }

        private Resonse FromList(IEnumerable<AveragePrice> prices, string itemTag)
        {
            var isBazaar = IsBazaar(ItemDetails.Instance.GetItemIdForTag(itemTag));
            return new Resonse()
            {
                Filterable = true,
                Bazaar = isBazaar,
                // exclude high moving 
                Prices = isBazaar ? prices.Where(p => p.Max < prices.Average(pi => pi.Min) * 1000).ToList() : prices.ToList()
            };
        }


        static ItemPrices()
        {
            Instance = new ItemPrices();
        }


        public async Task AddEndedAuctions(IEnumerable<SaveAuction> auctions)
        {
            TimeSpan aDay, oneHour;
            DateTime lastHour, startYesterday;
            ComputeTimes(out aDay, out oneHour, out lastHour, out startYesterday);

            foreach (var auction in auctions)
            {
                await AddAuction(aDay, oneHour, lastHour, startYesterday, auction);
            }
        }

        private async Task AddAuction(TimeSpan aDay, TimeSpan oneHour, DateTime lastHour, DateTime startYesterday, SaveAuction auction)
        {
            var id = ItemDetails.Instance.GetItemIdForTag(auction.Tag);
            var res = await GetHourlyLookup(id);
            if (res == null)
                res = new ItemLookup();
            res.AddNew(auction);
            await CacheService.Instance.SaveInRedis(GetIntraHourKey(id), res, TimeSpan.FromHours(1));
            await DropYesterDay(aDay, oneHour, lastHour, startYesterday, id, res);
        }

        public async Task AddBazaarData(BazaarPull pull)
        {
            TimeSpan aDay, oneHour;
            DateTime lastHour, startYesterday;
            ComputeTimes(out aDay, out oneHour, out lastHour, out startYesterday);
            foreach (var item in pull.Products)
            {
                var id = ItemDetails.Instance.GetOrCreateItemByTag(item.ProductId);
                //await CacheService.Instance.ModifyInRedis(GetIntraHourKey(id),)
                var res = await GetHourlyLookup(id);
                if (res == null)
                    res = new ItemLookup();
                res.AddNew(item, pull.Timestamp);
                await CacheService.Instance.SaveInRedis(GetIntraHourKey(id), res);
                await DropYesterDay(aDay, oneHour, lastHour, startYesterday, id, res);
            }

            if (BazzarItem.Count() == 0)
            {
                BazzarItem = pull.Products.ToDictionary(p => ItemDetails.Instance.GetOrCreateItemByTag(p.ProductId), p => true);
            }

        }

        private static void ComputeTimes(out TimeSpan aDay, out TimeSpan oneHour, out DateTime lastHour, out DateTime startYesterday)
        {
            aDay = TimeSpan.FromDays(1);
            oneHour = TimeSpan.FromHours(1);
            lastHour = (DateTime.Now - oneHour).RoundDown(oneHour) + TimeSpan.FromMinutes(1);
            startYesterday = (DateTime.Now - aDay).RoundDown(aDay);
        }

        private async Task DropYesterDay(TimeSpan aDay, TimeSpan oneHour, DateTime lastHour, DateTime startYesterday, int id, ItemLookup res)
        {
            if (res.Oldest.Date != default(DateTime) && res.Oldest.Date < lastHour)
            {
                // move the intrahour to hour
                var hourly = await GetLookupForToday(id);
                if (hourly == null)
                    hourly = new ItemLookup();
                var beginOfHour = DateTime.Now.RoundDown(oneHour);
                var oneHourRecord = res.CombineIntoOne(default(DateTime), beginOfHour);
                if (oneHourRecord.Avg != 0)
                    hourly.AddNew(oneHourRecord);

                if (hourly.Oldest.Date < startYesterday)
                {
                    hourly.Discard(DateTime.Now - aDay);
                    //ComputeBazaarPriceFor(id);
                }
                await CacheService.Instance.SaveInRedis(GetIntradayKey(id), hourly);
            }
        }

        private async Task<Resonse> QueryDB(ItemSearchQuery details)
        {
            using (var context = new HypixelContext())
            {
                var itemId = ItemDetails.Instance.GetItemIdForTag(details.name);
                IQueryable<SaveAuction> select = CreateSelect(details, context, itemId);
                IEnumerable<AveragePrice> response = await AvgFromAuctions(itemId, select, details.Start > DateTime.Now.Subtract(TimeSpan.FromDays(1.1)));

                return FromList(response.ToList(), details.name);
            }
        }

        private IQueryable<SaveAuction> CreateSelect(ItemSearchQuery details, HypixelContext context, int itemId, int limit = 0, IQueryable<SaveAuction> select = null)
        {
            var min = DateTime.Now - TimeSpan.FromDays(35);
            if (details.Filter != null && details.Start < min)
                throw new CoflnetException("filter_to_large", $"You are only allowed to filter for the last month, please set 'start' to a value greater than {min.AddHours(1).ToUnix()}");
            if (select == null)
                select = AuctionSelect(details.Start, details.End, context, itemId);

            if (details.Filter != null && details.Filter.Count > 0)
            {
                details.Filter["ItemId"] = itemId.ToString();
                return AddFilters(select, details.Filter);//FilterEngine.AddFilters(select, details.Filter);
            }

            if (details.Enchantments != null && details.Enchantments.Any())
                select = AddEnchantmentWhere(details.Enchantments, select, context, itemId, limit);

            if (details.Reforge != ItemReferences.Reforge.Any)
                select = select.Where(auction => auction.Reforge == details.Reforge);


            if (details.Tier != Tier.UNKNOWN)
                select = select.Where(a => a.Tier == details.Tier);
            /*
            if(details.Data != null && details.Data.Count > 0)
            {
                var kv = details.Data.First();
                select = select.Where(a=>(a.NbtData.Data as Dictionary<string,object>)[kv.Key].ToString() == kv.Value);
            }*/

            return select;
        }

        private static IQueryable<SaveAuction> AuctionSelect(DateTime start, DateTime end, HypixelContext context, int itemId)
        {
            return context.Auctions
                        .Where(auction => auction.ItemId == itemId)
                        .Where(auction => auction.End > start && auction.End < end)
                        .Where(auction => auction.HighestBidAmount > 1);
        }

        public async Task FillHours(System.Threading.CancellationToken token)
        {
            await Task.Delay(1000);

            using (var context = new HypixelContext())
            {
                context.Database.SetCommandTimeout(3600);
                foreach (var itemId in ItemDetails.Instance.TagLookup.Values.ToList())
                {
                    var select = AuctionSelect(DateTime.Now - TimeSpan.FromDays(1), DateTime.Now, context, itemId);
                    await UpdateAuctionsInRedis(itemId, select);

                    if (token.IsCancellationRequested)
                        return;
                }

            /*
                DateTime start;
                var bucket = await GetLookupForToday(ItemDetails.Instance.GetItemIdForTag("ENCHANTED_LAVA_BUCKET"));
                Console.WriteLine("----------\nyoungest lava bucket is " + bucket.Youngest.Date);
                var end = DateTime.Now - TimeSpan.FromDays(1);
                if (bucket != null)
                    end = bucket.Youngest.Date.RoundDown(TimeSpan.FromHours(1)) + TimeSpan.FromHours(1);
                var removeBefore = end - TimeSpan.FromHours(1);
                while (end < DateTime.Now)
                {
                    start = end;
                    end = start + TimeSpan.FromHours(1);
                    Console.WriteLine($"Caching bazaar for {start}");
                    await UpdateBazaarFor(start, end, removeBefore);

                    if (token.IsCancellationRequested)
                        return;
                }*/
            }
            if (token.IsCancellationRequested)
                return;
        }

        public static async Task FillLastHourIfDue()
        {
            // determine if due
            if (DateTime.Now.Minute != 0)
                return;
            const string Key = "lasthourly";
            var time = await CacheService.Instance.GetFromRedis<DateTime>(Key);
            if (time > DateTime.Now - TimeSpan.FromMinutes(5))
                return;

            // is due
            try
            {
                await CacheService.Instance.SaveInRedis<DateTime>(Key, DateTime.Now);
                await FillLastHour();

            }
            catch (Exception e)
            {
                // allow another try
                CacheService.Instance.RedisConnection.GetDatabase().KeyDelete(Key);
                Logger.Instance.Error($"FillLastHour got exception {e.Message} {e.StackTrace}");
            }

        }

        private static async Task FillLastHour()
        {
            var end = DateTime.Now.RoundDown(TimeSpan.FromHours(1));
            var start = end - TimeSpan.FromMinutes(60);
            var removeBefore = start - TimeSpan.FromDays(1);
            using (var context = new HypixelContext())
            {
                foreach (var itemId in ItemDetails.Instance.TagLookup.Values)
                {
                    var select = AuctionSelect(start, end, context, itemId);
                    await UpdateAuctionsInRedis(itemId, select, removeBefore);

                }
            }
            await UpdateBazaarFor(start, end, removeBefore);
        }

        private static async Task UpdateAuctionsInRedis(int itemId, IQueryable<SaveAuction> select, DateTime removeBefore = default(DateTime))
        {
            var result = await AvgFromAuctions(itemId, select, true);
            await CacheService.Instance.ModifyInRedis<ItemLookup>(GetIntradayKey(itemId), hoursList =>
            {
                if (hoursList == null)
                    hoursList = new ItemLookup();
                foreach (var item in result)
                {
                    if (hoursList.Youngest == null || item.Date > hoursList.Youngest.Date)
                        hoursList.AddNew(item);
                }
                hoursList.Discard(removeBefore);
                return hoursList;
            });
        }

        private static async Task UpdateBazaarFor(DateTime start, DateTime end, DateTime removeBefore)
        {
            return; // handled by bazaar service
        }

        public async Task BackfillPrices()
        {
            Console.WriteLine("starting to backfill item prices :)");
            using (var context = new HypixelContext())
            {
                context.Database.SetCommandTimeout(3600);
                
                var startAt = new DateTime(2024, 4, 1);
                foreach (var itemId in ItemDetails.Instance.TagLookup.Values.ToList())
                {
                    Console.WriteLine($"backfilling {itemId}");
                    await BackfillAuctions(context, startAt, itemId);
                }
            }
            Console.WriteLine("## Backfill completed :)");
            await FillYesterDayForever();
        }

        private static async Task BackfillAuctions(HypixelContext context, DateTime skyblockStart, int itemId)
        {
            DateTime start = skyblockStart;
            var updateDaySize = 7;
            var end = skyblockStart;
            while (
                end < DateTime.Now - TimeSpan.FromDays(updateDaySize))
            {

                start = end;
                end = start + TimeSpan.FromDays(updateDaySize);
                if (context.Prices.Where(p => p.ItemId == itemId && p.Date < end.AddMinutes(1) && p.Date > start.AddMinutes(-1)).Any())
                    continue;
                var select = AuctionSelect(start, end, context, itemId);
                var result = await AvgFromAuctions(itemId, select);

                await context.Prices.AddRangeAsync(result);
                await context.SaveChangesAsync();
                if (result.Count() != 0)
                    Console.Write($"SIP {itemId} ");

                await Task.Delay(100);
            }
        }

        private async Task FillYesterDayForever()
        {
            while (true)
            {
                try
                {
                    var start = (DateTime.Now - TimeSpan.FromDays(1)).Date;
                    var end = start + TimeSpan.FromDays(1);
                    using (var context = new HypixelContext())
                    {
                        context.Database.SetCommandTimeout(3600);
                        /*var idOfLava = ItemDetails.Instance.GetItemIdForTag("ENCHANTED_LAVA_BUCKET");
                        if (!context.Prices.Where(p => p.Date >= start && p.Date <= end && p.ItemId == idOfLava).Any())
                        {
                            var interval = (end - start) / 4;
                            for (DateTime bstart = start; bstart < end; bstart += interval)
                            {
                                await context.Prices.AddRangeAsync(await AvgBazzarHistory(bstart, bstart + interval));
                                await Task.Delay(5000);
                                await context.SaveChangesAsync();
                            }
                        }*/


                        foreach (var itemId in ItemDetails.Instance.TagLookup.Values)
                        {
                            await Task.Delay(200);
                            if (context.Prices.Where(p => p.Date >= start && p.Date <= end && p.ItemId == itemId).Any())
                                continue;
                            var select = AuctionSelect(start, end, context, itemId);
                            var result = await AvgFromAuctions(itemId, select);
                            await context.Prices.AddRangeAsync(result);
                            await context.SaveChangesAsync();
                        }
                    }
                    // wait for tomorrow (only when no exception)
                    await Task.Delay(DateTime.Now.Date + TimeSpan.FromDays(1.0001) - DateTime.Now);
                }
                catch (Exception e)
                {
                    Logger.Instance.Error($"Daily prices failed: {e.Message} \n {e.StackTrace}");
                    await Task.Delay(TimeSpan.FromMinutes(2));
                }
            }
        }

        public static async Task<List<AveragePrice>> AvgBazzarHistory(DateTime start, DateTime end)
        {
            var hours = (end - start).TotalHours;
            using (var context = new HypixelContext())
            {
                if (!Program.FullServerMode)
                    Console.WriteLine($"Queryig between {start} and {end}");
                var result =
                    (await context.BazaarPull.Where(item => item.Timestamp >= start && item.Timestamp <= end)
                    .SelectMany(pull => pull.Products)
                    .Include(p => p.QuickStatus)
                    .ToListAsync())
                    .AsParallel()
                    .GroupBy(item => item.ProductId)
                    .Select(item =>
                    {
                        return new AveragePrice()
                        {
                            Volume = (int)(item.Average(a => a.QuickStatus.BuyMovingWeek) / 7 / 24 * hours),
                            Avg = (float)item.Average(a => a.QuickStatus.BuyPrice + a.QuickStatus.SellPrice) / 2,
                            Max = (float)item.Max(a => a.QuickStatus.BuyPrice),
                            Min = (float)item.Min(a => a.QuickStatus.SellPrice),
                            Date = start,
                            ItemId = ItemDetails.Instance.GetOrCreateItemByTag(item.Key)
                        };
                    }).ToList();

                return result;
            }
        }

        private static async Task<IEnumerable<AveragePrice>> AvgFromAuctions(int itemId, IQueryable<SaveAuction> select, bool detailed = false)
        {
            var groupedSelect = select.GroupBy(item => new { item.End.Date, Hour = 0 });
            if (detailed)
                groupedSelect = select.GroupBy(item => new { item.End.Date, item.End.Hour });

            var dbResult = await groupedSelect
                .Select(item =>
                    new
                    {
                        End = item.Key,
                        Avg = item.Average(a => (a.HighestBidAmount) / a.Count),
                        Max = item.Max(a => (a.HighestBidAmount) / a.Count),
                        Min = item.Min(a => (a.HighestBidAmount) / a.Count),
                        Count = item.Sum(a => a.Count)
                    }).ToListAsync();

            return dbResult
                .Select(i => new AveragePrice()
                {
                    Volume = i.Count,
                    Avg = i.Avg,
                    Max = i.Max,
                    Min = i.Min,
                    Date = i.End.Date.Add(TimeSpan.FromHours(i.End.Hour)),
                    ItemId = itemId
                });
        }

        private static IQueryable<SaveAuction> AddEnchantmentWhere(List<Enchantment> enchantments, IQueryable<SaveAuction> moreThanOneBidQuery, HypixelContext context, int itemId, int limit = 0)
        {
            Console.WriteLine("adding enchantments filter");

            var query = context.Enchantment.Where(e => e.ItemType == itemId);


            if (enchantments.First().Type == Enchantment.EnchantmentType.None)
            {
                return SelectAuctionsWithoutEnchantments(ref moreThanOneBidQuery, query);
            }

            IEnumerable<int> ids = query.Where(e => e.ItemType == itemId && e.Type == enchantments.First().Type && e.Level == enchantments.First().Level)
                        .OrderByDescending(e => e.Id)
                        .Select(e => e.SaveAuctionId);

            if (enchantments.Count() > 1)
            {
                // first two
                ids = query.Where(
                    e => (e.ItemType == itemId && e.Type == enchantments.First().Type && e.Level == enchantments.First().Level)
                        || e.ItemType == itemId && e.Type == enchantments[1].Type && e.Level == enchantments[1].Level)
                        .GroupBy(e => e.SaveAuctionId)
                        .Where(e => e.Count() > 1)
                        .Select(e => e.Key);
            }

            if (limit > 0)
                ids = ids.Take(limit).ToList();

            moreThanOneBidQuery = moreThanOneBidQuery
            .Include(auction => auction.Enchantments)
            .Where(auction => ids.Contains(auction.Id));



            return moreThanOneBidQuery;
        }

        private static IQueryable<SaveAuction> SelectAuctionsWithoutEnchantments(ref IQueryable<SaveAuction> moreThanOneBidQuery, IQueryable<Enchantment> query)
        {
            IQueryable<int> ids = query.GroupBy(e => e.SaveAuctionId)
                                    .Where(e => e.Count() > 1)
                                    .Select(e => e.Key);
            return moreThanOneBidQuery
                .Include(auction => auction.Enchantments)
                .Where(auction => !ids.Contains(auction.Id));
        }

        public IEnumerable<ItemIndexElement> ItemsForDay(string itemName, DateTime date)
        {
            return null;
        }


        public IEnumerable<AuctionPreview> GetRecentAuctions(ItemSearchQuery query, int amount = 12)
        {
            using (var context = new HypixelContext())
            {
                var itemId = ItemDetails.Instance.GetItemIdForTag(query.name);

                var result = CreateSelect(query, context, itemId, amount)
                            .OrderByDescending(a => a.End).Take(amount).Select(a => new
                            {
                                a.End,
                                Price = a.HighestBidAmount,
                                a.AuctioneerId,
                                a.Uuid
                            }).ToList();
                return result.Select(async a => new AuctionPreview()
                {
                    End = a.End,
                    Price = a.Price,
                    Seller = a.AuctioneerId,
                    Uuid = a.Uuid,
                    PlayerName = await PlayerSearch.Instance.GetNameWithCacheAsync(a.AuctioneerId)
                }).Select(a => a.Result).ToList();
            }
        }


        public async Task<List<AuctionPreview>> GetActiveAuctions(ActiveItemSearchQuery query, int amount = 24)
        {
            query.Start = DateTime.Now.Subtract(TimeSpan.FromDays(14)).RoundDown(TimeSpan.FromDays(1));
            using (var context = new HypixelContext())
            {
                var itemId = ItemDetails.Instance.GetItemIdForTag(query.name);
                var dbselect = context.Auctions.Where(a => a.ItemId == itemId && a.End > DateTime.Now && (!a.Bin || a.Bids.Count == 0));

                var select = CreateSelect(query, context, itemId, amount, dbselect)
                            .Select(a => new
                            {
                                a.End,
                                Price = a.HighestBidAmount == 0 ? a.StartingBid : a.HighestBidAmount,
                                a.AuctioneerId,
                                a.Uuid
                            });
                switch (query.Order)
                {
                    case ActiveItemSearchQuery.SortOrder.ENDING_SOON:
                        select = select.OrderBy(a => a.End);
                        break;
                    case ActiveItemSearchQuery.SortOrder.LOWEST_PRICE:
                        select = select.OrderBy(a => a.Price);
                        break;
                    default:
                        select = select.OrderByDescending(a => a.Price);
                        break;
                }
                return (await select.Take(amount).ToListAsync()).Select(async a => new AuctionPreview()
                {
                    End = a.End,
                    Price = a.Price,
                    Seller = a.AuctioneerId,
                    Uuid = a.Uuid,
                    PlayerName = await PlayerSearch.Instance.GetNameWithCacheAsync(a.AuctioneerId)
                }).Select(a => a.Result).ToList();
            }
        }

        public static Task<List<AuctionPreview>> GetLowestBin(string itemTag, Dictionary<string, string> filter, int limit = 2)
        {
            filter["Bin"] = "true";
            var query = new ActiveItemSearchQuery()
            {
                Order = ActiveItemSearchQuery.SortOrder.LOWEST_PRICE,
                Limit = limit,
                Filter = filter,
                name = itemTag
            };
            var lowestBin = CoreServer.ExecuteCommandWithCache<ActiveItemSearchQuery, List<AuctionPreview>>("activeAuctions", query);
            return lowestBin;
        }

        public static Task<List<AuctionPreview>> GetLowestBin(string itemTag, Tier tier = Tier.UNKNOWN)
        {
            var filter = new Dictionary<string, string>();
            if (tier != Tier.UNCOMMON)
                filter["Rarity"] = tier.ToString();

            return GetLowestBin(itemTag, filter);
        }


        [DataContract]
        public class Resonse
        {
            [DataMember(Name = "filterable")]
            public bool Filterable;
            [DataMember(Name = "bazaar")]
            public bool Bazaar;
            [DataMember(Name = "filters")]
            public IEnumerable<string> Filters;
            [DataMember(Name = "prices")]
            public List<AveragePrice> Prices = new List<AveragePrice>();
        }

        [DataContract]
        public class AuctionPreview
        {
            [DataMember(Name = "seller")]
            public string Seller;
            [DataMember(Name = "price")]
            public long Price;
            [DataMember(Name = "end")]
            public DateTime End;
            [DataMember(Name = "uuid")]
            public string Uuid;
            [DataMember(Name = "playerName")]
            public string PlayerName;
        }
    }
}
