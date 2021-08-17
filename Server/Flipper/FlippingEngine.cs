using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MessagePack;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using static hypixel.GetActiveAuctionsCommand;

namespace hypixel.Flipper
{
    public class FlipperEngine
    {
        public static FlipperEngine Instance { get; }

        private const string FoundFlippsKey = "foundFlipps";
        private static int MIN_PRICE_POINT = 1000000;
        public static bool diabled;
        public ConcurrentQueue<FlipInstance> Flipps = new ConcurrentQueue<FlipInstance>();
        private ConcurrentQueue<FlipInstance> SlowFlips = new ConcurrentQueue<FlipInstance>();
        /// <summary>
        /// List of ultimate enchantments
        /// </summary>
        public static ConcurrentDictionary<Enchantment.EnchantmentType, bool> UltimateEnchants = new ConcurrentDictionary<Enchantment.EnchantmentType, bool>();

        private ConcurrentDictionary<long, int> Subs = new ConcurrentDictionary<long, int>();
        private ConcurrentDictionary<long, int> SlowSubs = new ConcurrentDictionary<long, int>();

        private ConcurrentDictionary<int, bool> AlreadyChecked = new ConcurrentDictionary<int, bool>();

        private ConcurrentQueue<SaveAuction> PotetialFlipps = new ConcurrentQueue<SaveAuction>();
        private ConcurrentQueue<SaveAuction> LowPriceQueue = new ConcurrentQueue<SaveAuction>();
        CancellationTokenSource TempWorkersStopSource = new CancellationTokenSource();

        public int QueueSize => PotetialFlipps.Count + LowPriceQueue.Count * 10000;
        private ConcurrentDictionary<long, DateTime> SoldAuctions = new ConcurrentDictionary<long, DateTime>();
        /// <summary>
        /// Wherether or not a given <see cref="SaveAuction.UId"/> was a flip or not
        /// </summary>
        private ConcurrentDictionary<long, bool> FlipIdLookup = new ConcurrentDictionary<long, bool>();
        static private List<Enchantment.EnchantmentType> UltiEnchantList = new List<Enchantment.EnchantmentType>();

        /// <summary>
        /// Special load burst queue that will send out 5 flips at load
        /// </summary>
        private Queue<FlipInstance> LoadBurst = new Queue<FlipInstance>();


        Prometheus.Counter foundFlipCount = Prometheus.Metrics
                    .CreateCounter("flips_found", "Number of flips found");
        Prometheus.Counter alreadySold = Prometheus.Metrics
                    .CreateCounter("already_sold_flips", "Flips that were already sold for premium users for some reason");
        Prometheus.Histogram time = Prometheus.Metrics.CreateHistogram("time_to_find_flip", "How long did it take to find a flip");

        static FlipperEngine()
        {
            Instance = new FlipperEngine();
            foreach (var item in Enum.GetValues(typeof(Enchantment.EnchantmentType)).Cast<Enchantment.EnchantmentType>())
            {
                if (item.ToString().StartsWith("ultimate_", true, null))
                {
                    UltimateEnchants.TryAdd(item, true);
                    UltiEnchantList.Add(item);
                }
            }
            Task.Run(async () =>
            {
                while (Program.updater == null)
                    await Task.Delay(TimeSpan.FromSeconds(10));
                Console.WriteLine("booting flipper");
                Program.updater.OnNewUpdateStart += Instance.OnUpdateStart;
                Program.updater.OnNewUpdateEnd += Instance.OnUpdateEnd;
                while (true)
                    await Instance.ProcessSlowQueue();
            }).ConfigureAwait(false);
        }

        private async Task ProcessSlowQueue()
        {
            try
            {
                if (SlowFlips.TryDequeue(out FlipInstance flip))
                {
                    if (SoldAuctions.ContainsKey(flip.UId))
                        flip.Sold = true;
                    var message = CreateDataFromFlip(flip);
                    NotifyAll(message, SlowSubs);
                    LoadBurst.Enqueue(flip);
                    if (LoadBurst.Count > 5)
                        LoadBurst.Dequeue();
                }

                await Task.Delay(DelayTimeFor(SlowFlips.Count) * 4 / 5);
            }
            catch (Exception e)
            {
                dev.Logger.Instance.Error(e, "slow queue processor");
            }
        }

        public static int DelayTimeFor(int queueSize)
        {
            return (int)Math.Min((TimeSpan.FromMinutes(5) / (Math.Max(queueSize, 1))).TotalMilliseconds, 10000);
        }

        private void OnUpdateStart()
        {
            TempWorkersStopSource.Cancel();
            TempWorkersStopSource = new CancellationTokenSource();
            var skippCount = Instance.LowPriceQueue.Count * 4 / 5 - 100;
            if (skippCount <= 0)
            {
                Console.WriteLine("got through all/most auctions :)");
                return;
            }
            Console.WriteLine($"flipper skipping {skippCount} auctions");
            Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(5));
                for (int i = 0; i < skippCount; i++)
                {
                    Instance.LowPriceQueue.TryDequeue(out SaveAuction removed);
                }
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets called when the updater has finished an update
        /// </summary>
        private void OnUpdateEnd()
        {
            var cancleToken = TempWorkersStopSource.Token;
            var workerCount = 3;
            Console.WriteLine($"Starting {workerCount} temp flip workers");
            for (int i = 0; i < workerCount; i++)
            {
                var worker = Task.Run(async () =>
                {
                    await DoFlipWork(cancleToken);

                }, cancleToken).ConfigureAwait(false);
            }
            ClearSoldBuffer();
        }

        /// <summary>
        /// Removes old <see cref="SoldAuctions"/>
        /// </summary>
        private void ClearSoldBuffer()
        {
            var toRemove = new List<long>();
            var oldestTime = DateTime.Now - TimeSpan.FromMinutes(10);
            foreach (var item in SoldAuctions)
            {
                if (item.Value < oldestTime)
                    toRemove.Add(item.Key);
            }
            foreach (var item in toRemove)
            {
                SoldAuctions.TryRemove(item, out DateTime deleted);
            }
        }

        private async Task DoFlipWork(CancellationToken cancleToken)
        {
            try
            {
                while (LowPriceQueue.Count > 10)
                {
                    await ProcessPotentialFlipps(cancleToken);
                    if (cancleToken.IsCancellationRequested)
                    {
                        Console.Write(" canceled temp worker :/ ");
                        return;
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Temp flip worker got exception {e.Message} {e.StackTrace}");
            }
        }

        public void AddConnection(SkyblockBackEnd con, int id = 0)
        {
            Subs.AddOrUpdate(con.Id, cid => id, (cid, oldMId) => id);
            var toSendFlips = Flipps.Reverse().Take(5);
            SendFlipHistory(con, id, toSendFlips);
        }

        public void AddNonConnection(SkyblockBackEnd con, int id = 0)
        {
            SlowSubs.AddOrUpdate(con.Id, cid => id, (cid, oldMId) => id);
            SendFlipHistory(con, id, LoadBurst, 0);
        }

        public void RemoveNonConnection(SkyblockBackEnd con)
        {
            SlowSubs.TryRemove(con.Id, out int value);
        }

        public void RemoveConnection(SkyblockBackEnd con)
        {
            Subs.TryRemove(con.Id, out int value);
            RemoveNonConnection(con);
        }




        private static void SendFlipHistory(SkyblockBackEnd con, int id, IEnumerable<FlipInstance> toSendFlips, int delay = 5000)
        {
            Task.Run(async () =>
            {
                foreach (var item in toSendFlips)
                {
                    var data = CreateDataFromFlip(item);
                    data.mId = id;
                    con.SendBack(data);
                    await Task.Delay(delay);
                }
            }).ConfigureAwait(false);
        }

        public void Test()
        {

        }

        public void NewAuctions(IEnumerable<SaveAuction> auctions)
        {
            if (diabled)
                return;
            foreach (var auction in auctions)
            {
                // determine flippability
                var price = auction.HighestBidAmount == 0 ? auction.StartingBid : (auction.HighestBidAmount * 1.1);

                if (AlreadyChecked.ContainsKey(auction.Uuid.GetHashCode()))
                    continue;

                if (AlreadyChecked.Count > 10_000)
                    AlreadyChecked.Clear();
                AlreadyChecked.TryAdd(auction.Uuid.GetHashCode(), true);

                if (price < MIN_PRICE_POINT || !auction.Bin)
                {
                    if (price > 10) // we only care about auctions worth more than the fee
                        LowPriceQueue.Enqueue(auction);
                    continue;
                }

                PotetialFlipps.Enqueue(auction);
            }

        }

        public Task ProcessPotentialFlipps()
        {
            return ProcessPotentialFlipps(CancellationToken.None);
        }

        public async Task ProcessPotentialFlipps(CancellationToken cancleToken)
        {
            try
            {
                await TryLoadFromCache();
                using (var context = new HypixelContext())
                {
                    var batchSize = 5;
                    if (PotetialFlipps.Count > 200)
                        batchSize = 10;
                    if (PotetialFlipps.Count > 400)
                        batchSize = 20;
                    for (int i = 0; i < batchSize; i++)
                    {
                        if (cancleToken.IsCancellationRequested)
                            return;
                        SaveAuction auction;
                        if (GetAuctionToCheckFlipability(out auction))
                            await NewAuction(auction, context);
                        else
                            // no auctions, sleep
                            await Task.Delay(1500);
                    }
                }
            }
            catch (Exception e)
            {
                dev.Logger.Instance.Error($"Flipper threw an exception {e.Message} {e.StackTrace}");
            }
        }

        private uint _auctionCounter = 0;
        private bool GetAuctionToCheckFlipability(out SaveAuction auction)
        {
            // mix in lowerPrice
            if (_auctionCounter++ % 3 != 0)
                if (PotetialFlipps.TryDequeue(out auction))
                    return true;
            return LowPriceQueue.TryDequeue(out auction);
        }

        private async Task TryLoadFromCache()
        {
            if (Flipps.Count == 0)
            {
                // try to get from redis

                var fromCache = await CacheService.Instance.GetFromRedis<ConcurrentQueue<FlipInstance>>(FoundFlippsKey);
                if (fromCache != default(ConcurrentQueue<FlipInstance>))
                {
                    Flipps = fromCache;
                    foreach (var item in Flipps)
                    {
                        FlipIdLookup[item.UId] = true;
                    }
                }
            }
        }

        public ConcurrentDictionary<long, List<long>> relevantAuctionIds = new ConcurrentDictionary<long, List<long>>();

        public async System.Threading.Tasks.Task NewAuction(SaveAuction auction, HypixelContext context)
        {
            if (!Program.Migrated)
            {
                if (auction.UId % 20 == 0) // don't spam the log
                    Console.WriteLine("not yet migrated skiping flip");
                return;
            }
            if (diabled && auction.UId % 5 != 0)
                return; // don't run on full cap on my dev machine :D
            
            // blacklist
            if(auction.ItemName == "null")
                return;

            var price = (auction.HighestBidAmount == 0 ? auction.StartingBid : (auction.HighestBidAmount * 1.1)) / auction.Count;

            if (auction.NBTLookup == null || auction.NBTLookup.Count() == 0)
                auction.NBTLookup = NBT.CreateLookup(auction);

            var (relevantAuctions, oldest) = await GetRelevantAuctionsCache(auction, context);

            long medianPrice = 0;
            if (relevantAuctions.Count < 2)
            {
                Console.WriteLine($"Could not find enough relevant auctions for {auction.ItemName} {auction.Uuid} ({auction.Enchantments.Count} {relevantAuctions.Count})");
                var itemId = ItemDetails.Instance.GetItemIdForName(auction.Tag, false);
                medianPrice = (long)((await ItemPrices.GetLookupForToday(itemId))?.Prices?.Average(p => p.Avg * 0.8 + p.Min * 0.2) ?? 0);
            }
            else
            {
                // the overall median was deemed to inaccurate
                return;
                medianPrice = relevantAuctions
                                .OrderByDescending(a => a.HighestBidAmount)
                                .Select(a => a.HighestBidAmount / a.Count)
                                .Skip(relevantAuctions.Count / 2)
                                .FirstOrDefault();
            }




            var recomendedBuyUnder = medianPrice * 0.8;
            if (price > recomendedBuyUnder) // at least 20% profit
            {
                return; // not a good flip
            }

            relevantAuctionIds[auction.UId] = relevantAuctions.Select(a => a.UId == 0 ? AuctionService.Instance.GetId(a.Uuid) : a.UId).ToList();
            if (relevantAuctionIds.Count > 10000)
            {
                relevantAuctionIds.Clear();
            }
            var itemTag = auction.Tag;
            List<ItemPrices.AuctionPreview> lowestBin = await GetLowestBin(itemTag,auction.Tier);

            var flip = new FlipInstance()
            {
                MedianPrice = (int)medianPrice,
                Name = auction.ItemName,
                Uuid = auction.Uuid,
                LastKnownCost = (int)price,
                Volume = (float)(relevantAuctions.Count / (DateTime.Now - oldest).TotalDays),
                Tag = auction.Tag,
                Bin = auction.Bin,
                UId = auction.UId,
                Rarity = auction.Tier,
                Interesting = PropertiesSelector.GetProperties(auction).OrderByDescending(a => a.Rating).Select(a => a.Value).ToList(),
                SellerName = await PlayerSearch.Instance.GetNameWithCacheAsync(auction.AuctioneerId),
                LowestBin = lowestBin.FirstOrDefault()?.Price,
                SecondLowestBin = lowestBin.Count >= 2 ? lowestBin[1].Price : 0L
            };

            FlipFound(flip,auction);
            if (auction.Uuid[0] == 'a') // reduce saves
                await CacheService.Instance.SaveInRedis(FoundFlippsKey, Flipps);
        }

        public static Task<List<ItemPrices.AuctionPreview>> GetLowestBin(string itemTag, Tier tier = Tier.UNKNOWN)
        {
            var filter = new Dictionary<string, string>() { { "Bin", "true" } };
            if(tier != Tier.UNCOMMON)
                filter["Rarity"] = tier.ToString();

            var query = new ActiveItemSearchQuery()
            {
                Order = SortOrder.LOWEST_PRICE,
                Limit = 2,
                Filter = filter,
                name = itemTag
            };
            var lowestBin = Server.ExecuteCommandWithCache<ActiveItemSearchQuery, List<ItemPrices.AuctionPreview>>("activeAuctions", query);
            return lowestBin;
        }

        /// <summary>
        /// Gets relevant items for an auction, checks cache first
        /// </summary>
        /// <param name="auction"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task<(List<SaveAuction>, DateTime)> GetRelevantAuctionsCache(SaveAuction auction, HypixelContext context)
        {
            var key = $"{auction.ItemId}{auction.ItemName}{auction.Tier}{auction.Bin}{auction.Count}";
            key += String.Concat(auction.Enchantments.Select(a => $"{a.Type}{a.Level}"));
            key += String.Concat(auction.FlatenedNBT.Where(d => !new string[]{"uid","spawnedFor","bossId"}.Contains(d.Key)));
            try
            {
                var fromCache = await CacheService.Instance.GetFromRedis<(List<SaveAuction>, DateTime)>(key);
                if (fromCache != default((List<SaveAuction>, DateTime)))
                {
                    //Console.WriteLine("flip cache hit");
                    return fromCache;
                }
            }
            catch (Exception e)
            {
                dev.Logger.Instance.Error(e, "cache flip");
            }

            var referenceAuctions = await GetRelevantAuctions(auction, context);
            // this could be shifted to another thread
            await CacheService.Instance.SaveInRedis<(List<SaveAuction>, DateTime)>(key, referenceAuctions, TimeSpan.FromHours(1));
            return referenceAuctions;
        }

        private async Task<(List<SaveAuction>, DateTime)> GetRelevantAuctions(SaveAuction auction, HypixelContext context)
        {
            var itemData = auction.NbtData.Data;
            var clearedName = auction.Reforge != ItemReferences.Reforge.None ? ItemReferences.RemoveReforge(auction.ItemName) : auction.ItemName;
            var itemId = ItemDetails.Instance.GetItemIdForName(auction.Tag, false);
            var youngest = DateTime.Now;
            var relevantEnchants = auction.Enchantments?.Where(e => UltimateEnchants.ContainsKey(e.Type) || e.Level >= 6).ToList();
            var matchingCount = relevantEnchants.Count > 2 ? relevantEnchants.Count / 2 : relevantEnchants.Count;
            var ulti = relevantEnchants.Where(e => UltimateEnchants.ContainsKey(e.Type)).FirstOrDefault();
            var highLvlEnchantList = relevantEnchants.Where(e => !UltimateEnchants.ContainsKey(e.Type)).Select(a => a.Type).ToList();
            var oldest = DateTime.Now - TimeSpan.FromHours(1);

            IQueryable<SaveAuction> select = GetSelect(auction, context, clearedName, itemId, youngest, matchingCount, ulti, highLvlEnchantList, oldest, auction.Reforge, 10);

            var relevantAuctions = await select
                .ToListAsync();

            if (relevantAuctions.Count < 9)
            {
                // to few auctions in last hour, try a whole day
                oldest = DateTime.Now - TimeSpan.FromDays(1.5);
                relevantAuctions = await GetSelect(auction, context, clearedName, itemId, youngest, matchingCount, ulti, highLvlEnchantList, oldest, auction.Reforge, 90)
                .ToListAsync();

                if (relevantAuctions.Count < 50 && PotetialFlipps.Count < 2000)
                {
                    // to few auctions in a day, query a week
                    oldest = DateTime.Now - TimeSpan.FromDays(8);
                    relevantAuctions = await GetSelect(auction, context, clearedName, itemId, youngest, matchingCount, ulti, highLvlEnchantList, oldest, auction.Reforge, 120)
                    .ToListAsync();
                    if (relevantAuctions.Count < 10 && clearedName.Contains("✪"))
                    {
                        clearedName = clearedName.Replace("✪", "").Trim();
                        relevantAuctions = await GetSelect(auction, context, clearedName, itemId, youngest, matchingCount, ulti, highLvlEnchantList, oldest, auction.Reforge, 120)
                        .ToListAsync();
                    }
                }
            }

            /* got replaced with average overall lookup
            if (relevantAuctions.Count < 3 && PotetialFlipps.Count < 100)
            {
                oldest = DateTime.Now - TimeSpan.FromDays(25);
                relevantAuctions = await GetSelect(auction, context, null, itemId, youngest, matchingCount, ulti, ultiList, highLvlEnchantList, oldest)
                        .ToListAsync();
            } */


            return (relevantAuctions, oldest);
        }

        private readonly static HashSet<ItemReferences.Reforge> relevantReforges = new HashSet<ItemReferences.Reforge>()
        {
            ItemReferences.Reforge.ancient,
            ItemReferences.Reforge.Necrotic,
            ItemReferences.Reforge.Giant
        };

        private static IQueryable<SaveAuction> GetSelect(
            SaveAuction auction,
            HypixelContext context,
            string clearedName,
            int itemId,
            DateTime youngest,
            int matchingCount,
            Enchantment ulti,
            List<Enchantment.EnchantmentType> highLvlEnchantList,
            DateTime oldest,
            ItemReferences.Reforge reforge,
            int limit = 60)
        {
            var select = context.Auctions
                .Where(a => a.ItemId == itemId)
                .Where(a => a.HighestBidAmount > 0)
                .Where(a => a.Tier == auction.Tier);

            byte ultiLevel = 127;
            var flatNbt = auction.FlatenedNBT ?? new Dictionary<string, string>();
            Enchantment.EnchantmentType ultiType = Enchantment.EnchantmentType.unknown;
            if (ulti != null)
            {
                ultiLevel = ulti.Level;
                ultiType = ulti.Type;
            }

            if (relevantReforges.Contains(reforge))
                select = select.Where(a => a.Reforge == reforge);


            if (auction.ItemName != clearedName && clearedName != null)
                select = select.Where(a => EF.Functions.Like(a.ItemName, "%" + clearedName));
            else if (auction.Tag.StartsWith("PET"))
            {
                select = AddPetLvlSelect(auction, select);
            }
            else
            {
                select = select.Where(a => a.ItemName == clearedName);
            }

            if (auction.Tag == "MIDAS_STAFF" || auction.Tag == "MIDAS_SWORD")
            {
                try
                {
                    var keyValue = "winning_bid";
                    select = AddMidasSelect(select, flatNbt, keyValue);
                    oldest -= TimeSpan.FromDays(10);
                }
                catch (Exception e)
                {
                    dev.Logger.Instance.Error(e, "trying filter flip midas item");
                }
            }
            if(auction.Tag.Contains("HOE") || flatNbt.ContainsKey("farming_for_dummies_count"))
                select = AddNBTSelect(select, flatNbt, "farming_for_dummies_count");


            select = AddEnchantmentSubselect(auction, matchingCount, highLvlEnchantList, select, ultiLevel, ultiType);
            if (limit == 0)
                return select;

            return select
                .Where(a => a.End > oldest && a.End < youngest)
                //.OrderByDescending(a=>a.Id)
                //.Include(a => a.NbtData)
                .Take(limit);
        }

        private static IQueryable<SaveAuction> AddNBTSelect(IQueryable<SaveAuction> select, Dictionary<string, string> flatNbt, string keyValue)
        {
            var keyId = NBT.GetLookupKey(keyValue);
            if(!flatNbt.ContainsKey(keyValue))
                return select.Where(a => !a.NBTLookup.Where(n => n.KeyId == keyId).Any());
            var val = long.Parse(flatNbt[keyValue]);
            select = select.Where(a => a.NBTLookup.Where(n => n.KeyId == keyId && n.Value == val).Any());
            return select;
        }

        private static IQueryable<SaveAuction> AddMidasSelect(IQueryable<SaveAuction> select, Dictionary<string, string> flatNbt, string keyValue)
        {
            var val = long.Parse(flatNbt[keyValue]);
            var keyId = NBT.GetLookupKey(keyValue);
            select = select.Where(a => a.NBTLookup.Where(n => n.KeyId == keyId && n.Value > val - 2_000_000 && n.Value < val + 2_000_000).Any());
            return select;
        }

        private static IQueryable<SaveAuction> AddPetLvlSelect(SaveAuction auction, IQueryable<SaveAuction> select)
        {
            var sb = new StringBuilder(auction.ItemName);
            if (sb[6] == ']')
                sb[5] = '_';
            else
                sb[6] = '_';
            select = select.Where(a => EF.Functions.Like(a.ItemName, sb.ToString()));
            return select;
        }

        private static IQueryable<SaveAuction> AddEnchantmentSubselect(SaveAuction auction, int matchingCount, List<Enchantment.EnchantmentType> highLvlEnchantList, IQueryable<SaveAuction> select, byte ultiLevel, Enchantment.EnchantmentType ultiType)
        {
            var maxImportantEnchants = highLvlEnchantList.Count() + 1 + (ultiType == Enchantment.EnchantmentType.unknown ? 0 : 1);
            if (matchingCount > 0)
                select = select.Where(a => a.Enchantments
                        .Where(e => (e.Level > 5 && highLvlEnchantList.Contains(e.Type)
                                    || e.Type == ultiType && e.Level == ultiLevel)).Count() >= matchingCount
                                    && a.Enchantments.Where(e => UltiEnchantList.Contains(e.Type) || e.Level > 5).Count() <= maxImportantEnchants);
            else if (auction.Enchantments?.Count == 1)
                select = select.Where(a => a.Enchantments != null && a.Enchantments.Any()
                        && a.Enchantments.First().Type == auction.Enchantments.First().Type
                        && a.Enchantments.First().Level == auction.Enchantments.First().Level);
            else if (auction.Enchantments?.Count == 2)
            {
                select = select.Where(a => a.Enchantments != null && a.Enchantments.Count() == 2
                        && a.Enchantments.Where(e =>
                            e.Type == auction.Enchantments[0].Type && e.Level == auction.Enchantments[0].Level
                            || e.Type == auction.Enchantments[1].Type && e.Level == auction.Enchantments[1].Level).Count() == 2);
            }

            // make sure we exclude special enchants to get a reasonable price
            else if (auction.Enchantments.Any())
                select = select.Where(a => !a.Enchantments.Where(e => UltiEnchantList.Contains(e.Type) || e.Level > 5).Any());
            else
                select = select.Where(a => !a.Enchantments.Any());
            return select;
        }

        private void FlipFound(FlipInstance flip, SaveAuction auction)
        {
            MessageData message = CreateDataFromFlip(flip);
            NotifyAll(message, Subs);
            SlowFlips.Enqueue(flip);

            foundFlipCount.Inc();
            time.Observe((DateTime.Now - auction.Start).TotalSeconds);
            if(flip.Sold)
                alreadySold.Inc();

            Flipps.Enqueue(flip);
            FlipIdLookup[flip.UId] = true;
            if (Flipps.Count > 1500)
            {
                if (Flipps.TryDequeue(out FlipInstance result))
                {
                    FlipIdLookup.Remove(result.UId, out bool value);
                }
            }
        }

        /// <summary>
        /// Tell the flipper that an auction was sold
        /// </summary>
        /// <param name="auction"></param>
        public void AuctionSold(SaveAuction auction)
        {
            if (!FlipIdLookup.ContainsKey(auction.UId))
                return;
            SoldAuctions[auction.UId] = auction.End;
            var auctionUUid = auction.Uuid;
            NotifySubsInactiveAuction(auctionUUid);
        }

        private void NotifySubsInactiveAuction(string auctionUUid)
        {
            var message = new MessageData("sold", auctionUUid);
            NotifyAll(message, Subs);
            NotifyAll(message, SlowSubs);
        }

        /// <summary>
        /// Auction is no longer active for some reason
        /// </summary>
        /// <param name="uuid"></param>
        public void AuctionInactive(string uuid)
        {
            NotifySubsInactiveAuction(uuid);
            var uid = AuctionService.Instance.GetId(uuid);
            SoldAuctions[uid] = DateTime.Now;
        }

        private static void NotifyAll(MessageData message, ConcurrentDictionary<long, int> subscribers)
        {
            foreach (var item in subscribers.Keys)
            {
                var m = MessageData.Copy(message);
                m.mId = subscribers[item];
                try
                {
                    if (!SkyblockBackEnd.SendTo(m, item))
                        subscribers.TryRemove(item, out int value);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Failed to send flip {e.Message} {e.StackTrace}");
                    subscribers.TryRemove(item, out int value);
                }
            }
        }

        private static MessageData CreateDataFromFlip(FlipInstance flip)
        {
            return new MessageData("flip", JSON.Stringify(flip), 60);
        }

        /*
1 Enchantments
2 Dungon Stars
3 Skins
4 Rarity
5 Reforge
6 Flumming potato books
7 Hot Potato Books

        */

        [DataContract]
        public class FlipInstance
        {
            [DataMember(Name = "median")]
            public int MedianPrice;
            [DataMember(Name = "cost")]
            public int LastKnownCost;
            [DataMember(Name = "uuid")]
            public string Uuid;
            [DataMember(Name = "name")]
            public string Name;
            [DataMember(Name = "sellerName")]
            public string SellerName;
            [DataMember(Name = "volume")]
            public float Volume;
            [DataMember(Name = "tag")]
            public string Tag;
            [DataMember(Name = "bin")]
            public bool Bin;
            [DataMember(Name = "sold")]
            public bool Sold { get; internal set; }
            [DataMember(Name = "tier")]
            public Tier Rarity { get; internal set; }
            [DataMember(Name = "prop")]
            public List<string> Interesting { get; internal set; }
            [DataMember(Name = "secondLowestBin")]
            public long? SecondLowestBin { get; internal set; }

            [DataMember(Name = "lowestBin")]
            public long? LowestBin;
            [IgnoreDataMember]
            public long UId;
        }
    }
}