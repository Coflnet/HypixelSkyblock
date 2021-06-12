using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using MessagePack;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace hypixel.Flipper
{
    public class FlipperEngine
    {
        public static FlipperEngine Instance { get; }

        private const string FoundFlippsKey = "foundFlipps";
        private static int MIN_PRICE_POINT = 1000000;
        public ConcurrentQueue<FlipInstance> Flipps = new ConcurrentQueue<FlipInstance>();
        private static ConcurrentDictionary<Enchantment.EnchantmentType, bool> UltimateEnchants = new ConcurrentDictionary<Enchantment.EnchantmentType, bool>();

        private ConcurrentDictionary<long, int> Subs = new ConcurrentDictionary<long, int>();
        private ConcurrentDictionary<long, int> SlowSubs = new ConcurrentDictionary<long, int>();

        private ConcurrentDictionary<int, bool> AlreadyChecked = new ConcurrentDictionary<int, bool>();

        private ConcurrentQueue<SaveAuction> PotetialFlipps = new ConcurrentQueue<SaveAuction>();
        private ConcurrentQueue<SaveAuction> LowPriceQueue = new ConcurrentQueue<SaveAuction>();

        public int QueueSize => PotetialFlipps.Count + LowPriceQueue.Count * 10000;

        static FlipperEngine()
        {
            Instance = new FlipperEngine();
            foreach (var item in Enum.GetValues(typeof(Enchantment.EnchantmentType)))
            {
                if (item.ToString().StartsWith("ultimate_", true, null))
                    UltimateEnchants.TryAdd((Enchantment.EnchantmentType)item, true);
            }
            Task.Run(async () =>
            {
                while (Program.updater == null)
                    await Task.Delay(TimeSpan.FromSeconds(10));
                Console.WriteLine("booting flipper");
                Program.updater.OnNewUpdateStart += () =>
                {
                    Instance.LowPriceQueue.Clear();
                    if (Instance.PotetialFlipps.Count < 100)
                        return;
                    var boostCount = Instance.PotetialFlipps.Count / 12;
                    Console.WriteLine($"boosting flipper with {boostCount}");
                    // the flipper didn't get thorough all items in the last min increase the worker count
                    for (int i = 0; i < boostCount; i++)
                    {
                        Task.Run(async () =>
                        {
                            await Task.Delay(TimeSpan.FromSeconds(10 + i));
                            await Instance.ProcessPotentialFlipps();
                        });
                    }

                };
            });
        }


        public void AddConnection(SkyblockBackEnd con, int id = 0)
        {
            Subs.AddOrUpdate(con.Id, cid => id, (cid, oldMId) => id);
            var toSendFlips = Flipps.Reverse().Take(3);
            SendFlipHistory(con, id, toSendFlips);
        }

        public void AddNonConnection(SkyblockBackEnd con, int id = 0)
        {
            SlowSubs.AddOrUpdate(con.Id, cid => id, (cid, oldMId) => id);
            var toSendFlips = Flipps.Take(23);
            SendFlipHistory(con, id, toSendFlips);
        }

        private static void SendFlipHistory(SkyblockBackEnd con, int id, IEnumerable<FlipInstance> toSendFlips)
        {
            Task.Run(async () =>
            {
                foreach (var item in toSendFlips)
                {
                    await Task.Delay(5000);
                    var data = CreateDataFromFlip(item);
                    data.mId = id;
                    con.SendBack(data);
                }
            });
        }

        public void Test()
        {

        }

        public void NewAuctions(IEnumerable<SaveAuction> auctions)
        {
            var minPricePointAdopted = PotetialFlipps.Count > 120 ? MIN_PRICE_POINT : MIN_PRICE_POINT / 2;
            if (PotetialFlipps.Count < 60)
                minPricePointAdopted = MIN_PRICE_POINT / 5;
            foreach (var auction in auctions)
            {
                // determine flippability
                var price = auction.HighestBidAmount == 0 ? auction.StartingBid : (auction.HighestBidAmount * 1.1);
                if (price < minPricePointAdopted || !auction.Bin)
                {
                    if (price > minPricePointAdopted / 10)
                        LowPriceQueue.Enqueue(auction);
                    continue; // unflipable
                }

                if (AlreadyChecked.ContainsKey(auction.Uuid.GetHashCode()))
                    continue;

                if (AlreadyChecked.Count > 10_000)
                    AlreadyChecked.Clear();
                AlreadyChecked.TryAdd(auction.Uuid.GetHashCode(), true);


                PotetialFlipps.Enqueue(auction);
            }

        }

        public async Task ProcessPotentialFlipps()
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
                        SaveAuction auction;
                        if (GetAuctionToCheckFlipability(out auction))
                            await NewAuction(auction, context);
                    }
                }
                await Task.Delay(TimeSpan.FromSeconds(2));
            }
            catch (Exception e)
            {
                dev.Logger.Instance.Error($"Flipper threw an exception {e.Message} {e.StackTrace}");
            }
        }

        private bool GetAuctionToCheckFlipability(out SaveAuction auction)
        {
            if (!PotetialFlipps.TryDequeue(out auction))
                return LowPriceQueue.TryDequeue(out auction);
            return true;
        }

        private async Task TryLoadFromCache()
        {
            if (Flipps.Count == 0)
            {
                // try to get from redis

                var fromCache = await CacheService.Instance.GetFromRedis<ConcurrentQueue<FlipInstance>>(FoundFlippsKey);
                if (fromCache != default(ConcurrentQueue<FlipInstance>))
                    Flipps = fromCache;
            }
        }

        public async System.Threading.Tasks.Task NewAuction(SaveAuction auction, HypixelContext context)
        {


            var price = auction.HighestBidAmount == 0 ? auction.StartingBid : (auction.HighestBidAmount * 1.1);

            // if(auction.Enchantments.Count == 0 && auction.Reforge == ItemReferences.Reforge.None)
            //    Console.WriteLine("easy item");


            var itemData = auction.NbtData.Data;
            var clearedName = auction.Reforge != ItemReferences.Reforge.None ? ItemReferences.RemoveReforge(auction.ItemName) : auction.ItemName;
            var itemId = ItemDetails.Instance.GetItemIdForName(auction.Tag, false);
            var youngest = DateTime.Now;
            var relevantEnchants = auction.Enchantments?.Where(e => UltimateEnchants.ContainsKey(e.Type) || e.Level >= 6).ToList();
            var matchingCount = relevantEnchants.Count > 2 ? relevantEnchants.Count / 2 : relevantEnchants.Count;
            var ulti = relevantEnchants.Where(e => UltimateEnchants.ContainsKey(e.Type)).FirstOrDefault();
            var ultiList = UltimateEnchants.Select(u => u.Key).ToList();
            var highLvlEnchantList = relevantEnchants.Where(e => !UltimateEnchants.ContainsKey(e.Type)).Select(a => a.Type).ToList();
            var oldest = DateTime.Now - TimeSpan.FromHours(1);

            IQueryable<SaveAuction> select = GetSelect(auction, context, clearedName, itemId, youngest, matchingCount, ulti, ultiList, highLvlEnchantList, oldest, 10);

            var relevantAuctions = await select
                .ToListAsync();

            if (relevantAuctions.Count < 9)
            {
                // to few auctions in last hour, try a whole day
                oldest = DateTime.Now - TimeSpan.FromDays(1);
                relevantAuctions = await GetSelect(auction, context, clearedName, itemId, youngest, matchingCount, ulti, ultiList, highLvlEnchantList, oldest)
                .ToListAsync();

                if (relevantAuctions.Count < 50)
                {
                    // to few auctions in a day, query a week
                    oldest = DateTime.Now - TimeSpan.FromDays(8);
                    relevantAuctions = await GetSelect(auction, context, clearedName.Replace("✪", ""), itemId, youngest, matchingCount, ulti, ultiList, highLvlEnchantList, oldest, 120)
                    .ToListAsync();
                }
            }


            if (relevantAuctions.Count < 3)
            {
                oldest = DateTime.Now - TimeSpan.FromDays(25);
                relevantAuctions = await GetSelect(auction, context, null, itemId, youngest, matchingCount, ulti, ultiList, highLvlEnchantList, oldest)
                        .ToListAsync();
            }


            // client filter
            //               if (matchingCount > 0)
            //                    relevantAuctions = relevantAuctions.Where(a => a.Enchantments.Where(e => relevantEnchants.Where(r => r.Type == e.Type).First().Level == e.Level).Any()).ToList();
            if (relevantAuctions.Count < 2)
            {
                Console.WriteLine($"Could not find enough relevant auctions for {auction.ItemName} {auction.Uuid} ({clearedName} {auction.Enchantments.Count} {matchingCount}");
                return;
            }

            var medianPrice = relevantAuctions
                .OrderByDescending(a => a.HighestBidAmount).Select(a => a.HighestBidAmount).Skip(relevantAuctions.Count / 2).First();


            var recomendedBuyUnder = medianPrice * 0.8;
            if (price > recomendedBuyUnder) // at least 20% profit
            {
                return; // not a good flip
            }



            var flip = new FlipInstance()
            {
                MedianPrice = (int)medianPrice,
                Name = auction.ItemName,
                Uuid = auction.Uuid,
                LastKnownCost = (int)price,
                Volume = (float)(relevantAuctions.Count / (DateTime.Now - oldest).TotalDays),
                Tag = auction.Tag,
                Bin = auction.Bin
            };
            Flipps.Enqueue(flip);
            if (Flipps.Count > 200)
                Flipps.TryDequeue(out FlipInstance result);

            FlippFound(flip);
            await CacheService.Instance.SaveInRedis(FoundFlippsKey, Flipps);
        }

        private static IQueryable<SaveAuction> GetSelect(SaveAuction auction, HypixelContext context, string clearedName, int itemId, DateTime youngest, int matchingCount, Enchantment ulti, List<Enchantment.EnchantmentType> ultiList, List<Enchantment.EnchantmentType> highLvlEnchantList, DateTime oldest, int limit = 60)
        {
            var select = context.Auctions
                .Where(a => a.ItemId == itemId)
                .Where(a => a.End > oldest && a.End < youngest)
                .Where(a => a.HighestBidAmount > 0)
                .Where(a => a.Tier == auction.Tier);

            byte ultiLevel = 127;
            Enchantment.EnchantmentType ultiType = Enchantment.EnchantmentType.unknown;
            if (ulti != null)
            {
                ultiLevel = ulti.Level;
                ultiType = ulti.Type;
            }


            if (auction.ItemName != clearedName && clearedName != null)
                select = select.Where(a => EF.Functions.Like(a.ItemName, "%" + clearedName));
            if (auction.Tag.StartsWith("PET"))
            {
                var sb = new StringBuilder(auction.ItemName);
                if (sb[6] == ']')
                    sb[5] = '_';
                else
                    sb[6] = '_';
                select = select.Where(a => EF.Functions.Like(a.ItemName, sb.ToString()));
            }

            select = AddEnchantmentSubselect(auction, matchingCount, ultiList, highLvlEnchantList, select, ultiLevel, ultiType);
            return select
                //.OrderByDescending(a=>a.Id)
                .Include(a => a.NbtData)
                .Take(limit);
        }

        private static IQueryable<SaveAuction> AddEnchantmentSubselect(SaveAuction auction, int matchingCount, List<Enchantment.EnchantmentType> ultiList, List<Enchantment.EnchantmentType> highLvlEnchantList, IQueryable<SaveAuction> select, byte ultiLevel, Enchantment.EnchantmentType ultiType)
        {
            if (matchingCount > 0)
                select = select.Where(a => a.Enchantments
                        .Where(e => (e.Level > 5 && highLvlEnchantList.Contains(e.Type)
                                    || e.Type == ultiType && e.Level == ultiLevel)).Count() >= matchingCount);
            else if (auction.Enchantments?.Count == 1)
                select = select.Where(a => a.Enchantments != null && a.Enchantments.First().Type == auction.Enchantments.First().Type && a.Enchantments.First().Level == auction.Enchantments.First().Level);
            // make sure we exclude special enchants to get a reasonable price
            else if (auction.Enchantments.Any())
                select = select.Where(a => !a.Enchantments.Where(e => ultiList.Contains(e.Type) || e.Level > 5).Any());
            else if (auction.Category == Category.WEAPON || auction.Category == Category.ARMOR || auction.Tag == "ENCHANTED_BOOK")
                select = select.Where(a => !a.Enchantments.Any());
            return select;
        }

        private void FlippFound(FlipInstance flip)
        {
            MessageData message = CreateDataFromFlip(flip);
            var subscribers = Subs;
            NotifyAll(message, Subs);
            Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromMinutes(3));
                NotifyAll(message, SlowSubs);
            });
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
            [DataMember(Name = "volume")]
            public float Volume;
            [DataMember(Name = "tag")]
            public string Tag;
            [DataMember(Name = "bin")]
            public bool Bin;
        }
    }

}