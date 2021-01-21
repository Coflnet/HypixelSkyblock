using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Coflnet;
using ConcurrentCollections;
using dev;
using MessagePack;
using Microsoft.EntityFrameworkCore;

namespace hypixel
{
    public partial class ItemPrices
    {
        public static ItemPrices Instance;

        private ConcurrentDictionary<int, ItemLookup> Hours = new ConcurrentDictionary<int, ItemLookup>();
        private ConcurrentDictionary<int, ItemLookup> IntraHour = new ConcurrentDictionary<int, ItemLookup>();

        private ConcurrentHashSet<string> pathsToSave = new ConcurrentHashSet<string>();

        private Dictionary<int, bool> BazzarItem = new Dictionary<int, bool>();

        private bool IsFilterable(int itemId)
        {
            // TODO: find historic enchantments in the db to determine filterability by enchants and reforges
            return !IsBazaar(itemId);
        }

        private bool IsBazaar(int itemId)
        {
            return BazzarItem.ContainsKey(itemId);
        }

        internal async Task<Resonse> GetPriceFor(ItemSearchQuery details)
        {
            var itemId = ItemDetails.Instance.GetItemIdForName(details.name);

            Console.WriteLine("got request for " + details.name);

            if (details.Reforge != ItemReferences.Reforge.Any || (details.Enchantments != null && details.Enchantments.Count != 0))
                return await QueryDB(details);


            if (details.Start > DateTime.Now - TimeSpan.FromHours(2) && IntraHour.TryGetValue(itemId, out ItemLookup value))
                return FromItemLookup(value);


            if (details.Start > DateTime.Now - TimeSpan.FromDays(1.01) && Hours.TryGetValue(itemId, out ItemLookup hourValue))
                return FromItemLookup(hourValue, IntraHour.GetValueOrDefault(itemId)?.CombineIntoOne(default(DateTime), DateTime.Now));

            using (var context = new HypixelContext())
            {
                Console.WriteLine("query prices db ");
                var response = await context.Prices
                                .Where(p => p.ItemId == itemId && p.Date > details.Start && p.Date <= details.End).ToListAsync();

                return FromList(response, itemId);
            }
        }

        private Resonse FromItemLookup(ItemLookup value, AveragePrice additional = null)
        {
            return FromList(additional == null || additional.Volume == 0 ? value.Prices : value.Prices.Append(additional), value.ItemId);
        }

        private Resonse FromList(IEnumerable<AveragePrice> prices, int itemId)
        {
            return new Resonse()
            {
                Filterable = IsFilterable(itemId),
                Bazaar = IsBazaar(itemId),
                Prices = prices.ToList()
            };
        }

        static ItemPrices()
        {
            Instance = new ItemPrices();
        }

        public void AddNewAuction(SaveAuction auction)
        {
            TimeSpan aDay, oneHour;
            DateTime lastHour, startYesterday;
            ComputeTimes(out aDay, out oneHour, out lastHour, out startYesterday);

            var id = ItemDetails.Instance.GetItemIdForName(auction.Tag);
            var res = IntraHour.GetOrAdd(id, (id) => new ItemLookup());
            res.AddNew(auction);
            DropYesterDay(aDay, oneHour, lastHour, startYesterday, id, res);

        }

        public void AddBazaarData(BazaarPull pull)
        {
            TimeSpan aDay, oneHour;
            DateTime lastHour, startYesterday;
            ComputeTimes(out aDay, out oneHour, out lastHour, out startYesterday);
            foreach (var item in pull.Products)
            {
                var id = ItemDetails.Instance.GetOrCreateItemByTag(item.ProductId);
                var res = IntraHour.GetOrAdd(id, (id) => new ItemLookup());
                res.AddNew(item, pull.Timestamp);
                DropYesterDay(aDay, oneHour, lastHour, startYesterday, id, res);
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
            lastHour = RoundDown(DateTime.Now - oneHour, oneHour);
            startYesterday = RoundDown(DateTime.Now - aDay, aDay);
        }

        private void DropYesterDay(TimeSpan aDay, TimeSpan oneHour, DateTime lastHour, DateTime startYesterday, int id, ItemLookup res)
        {
            if (res.Oldest.Date != default(DateTime) && res.Oldest.Date < lastHour)
            {
                Console.WriteLine("combining " + id);
                // move the intrahour to hour
                var hourly = Hours.GetOrAdd(id, id => new ItemLookup());
                var beginOfHour = RoundDown(DateTime.Now, oneHour);
                var oneHourRecord = res.CombineIntoOne(lastHour, beginOfHour);
                if (oneHourRecord.Date != default(DateTime))
                    hourly.AddNew(oneHourRecord);
                res.Discard(beginOfHour);

                if (hourly.Oldest.Date < startYesterday)
                {
                    hourly.Discard(DateTime.Now - aDay);
                    //ComputeBazaarPriceFor(id);
                }
            }
        }

        private async Task<Resonse> QueryDB(ItemSearchQuery details)
        {
            Console.WriteLine("from db");
            using (var context = new HypixelContext())
            {
                var itemId = ItemDetails.Instance.GetItemIdForName(details.name);
                var select = AuctionSelect(details.Start, details.End, context, itemId);


                if (details.Enchantments != null && details.Enchantments.Any())
                    select = AddEnchantmentWhere(details.Enchantments, select, context, itemId);

                if (details.Reforge != ItemReferences.Reforge.Any)
                    select = select.Where(auction => auction.Reforge == details.Reforge);
                IEnumerable<AveragePrice> response = await AvgFromAuctions(itemId, select);

                return FromList(response.ToList(), itemId);

                // cache result

            }
        }

        private static IQueryable<SaveAuction> AuctionSelect(DateTime start, DateTime end, HypixelContext context, int itemId)
        {
            return context.Auctions
                        .Where(auction => auction.ItemId == itemId)
                        .Where(auction => auction.End > start && auction.End < end)
                        .Where(auction => auction.HighestBidAmount > 1);
        }

        public async Task FillHours()
        {
            await Task.Delay(10000);

            using (var context = new HypixelContext())
            {
                context.Database.SetCommandTimeout(3600);
                foreach (var itemId in ItemDetails.Instance.TagLookup.Values)
                {
                    var select = AuctionSelect(DateTime.Now - TimeSpan.FromDays(1), DateTime.Now, context, itemId);
                    var result = await AvgFromAuctions(itemId, select, true);
                    var hoursList = Hours.GetOrAdd(itemId, id => new ItemLookup());
                    foreach (var item in result)
                    {
                        hoursList.AddNew(item);
                    }
                }

                DateTime start;
                var end = DateTime.Now - TimeSpan.FromDays(1);
                for (int i = 0; i < 24; i++)
                {
                    start = end;
                    end = start + TimeSpan.FromHours(1);

                    foreach (var item in await AvgBazzarHistory(start, end))
                    {
                        var hoursList = Hours.GetOrAdd(item.ItemId, id => new ItemLookup());
                        hoursList.AddNew(item);
                    }
                }
            }
            await BackfillPrices();
        }

        private async Task BackfillPrices()
        {
            Console.WriteLine("starting to backfill item prices :)");
            using (var context = new HypixelContext())
            {
                context.Database.SetCommandTimeout(3600);
                // bazzar
                DateTime start = new DateTime();
                var idOfLava = ItemDetails.Instance.GetItemIdForName("ENCHANTED_LAVA_BUCKET");
                var end = new DateTime(2020, 5, 1);
                while (
                    end < DateTime.Now - TimeSpan.FromDays(1))
                {
                    start = end;
                    var size = 1d;

                    // a lot of data in between these
                    if (start >= new DateTime(2020, 05, 25) && start < new DateTime(2020, 6, 1))
                        size = 0.25;

                    end = start + TimeSpan.FromDays(size);
                    // only requery if lava has no price
                    if (context.Prices.Where(p => p.Date >= start - TimeSpan.FromSeconds(1) && p.Date <= end && p.ItemId == idOfLava).Any())
                        continue;

                    try
                    {
                        var data = await AvgBazzarHistory(start, end);
                        if (data.Count() == 0)
                            continue;
                        await context.Prices.AddRangeAsync(data);
                        await context.SaveChangesAsync();
                        if (data.Count() != 0)
                            Console.WriteLine("Saved bazaar prices for day " + start);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Backfill failed :( for day {start} \n{e.Message}\n {e.InnerException?.Message} {e.StackTrace}");
                    }
                    await Task.Delay(1000);
                }

                var skyblockStart = new DateTime(2019, 5, 1);
                foreach (var itemId in ItemDetails.Instance.TagLookup.Values.ToList())
                {
                    await BackfillAuctions(context, skyblockStart, itemId);
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
                if (context.Prices.Where(p => p.ItemId == itemId && p.Date < end + TimeSpan.FromMinutes(1) && p.Date > start - TimeSpan.FromMinutes(1)).Any())
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
                        var idOfLava = ItemDetails.Instance.GetItemIdForName("ENCHANTED_LAVA_BUCKET");
                        if (!context.Prices.Where(p => p.Date >= start && p.Date <= end && p.ItemId == idOfLava).Any())
                            await context.Prices.AddRangeAsync(await AvgBazzarHistory(start, end));

                        await context.SaveChangesAsync();

                        foreach (var itemId in ItemDetails.Instance.TagLookup.Values)
                        {
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


            return (await groupedSelect
                .Select(item =>
                    new
                    {
                        End = item.Key,
                        Avg = (int)item.Average(a => ((int)a.HighestBidAmount) / a.Count),
                        Max = (int)item.Max(a => ((int)a.HighestBidAmount) / a.Count),
                        Min = (int)item.Min(a => ((int)a.HighestBidAmount) / a.Count),
                        Count = item.Sum(a => a.Count)
                    }).ToListAsync())
                .Select(i => new AveragePrice()
                {
                    Volume = i.Count,
                    Avg = i.Avg,
                    Max = i.Max,
                    Min = i.Min,
                    Date = i.End.Date + TimeSpan.FromHours(i.End.Hour),
                    ItemId = itemId
                });
        }

        private static IQueryable<SaveAuction> AddEnchantmentWhere(List<Enchantment> enchantments, IQueryable<SaveAuction> moreThanOneBidQuery, HypixelContext context, int itemId)
        {
            Console.WriteLine("adding enchantments filter");

            var query = context.Enchantment.Where(e => e.ItemType == itemId);


            if (enchantments.First().Type == Enchantment.EnchantmentType.None)
            {
                return SelectAuctionsWithoutEnchantments(ref moreThanOneBidQuery, query);
            }
            
            var ids = query.Where(e => e.ItemType == itemId && e.Type == enchantments.First().Type && e.Level == enchantments.First().Level)
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




        public static DateTime RoundDown(DateTime date, TimeSpan span)
        {
            return new DateTime(date.Ticks / span.Ticks * span.Ticks);
        }

        [MessagePackObject]
        public class Resonse
        {
            [Key("filterable")]
            public bool Filterable;
            [Key("bazaar")]
            public bool Bazaar;
            [Key("prices")]
            public List<AveragePrice> Prices = new List<AveragePrice>();
        }
    }
}
