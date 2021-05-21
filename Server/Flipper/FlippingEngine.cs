using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using MessagePack;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace hypixel.Flipper
{
    public class FlipperEngine
    {
        public static FlipperEngine Instance { get; }


        public ConcurrentQueue<FlipInstance> Flipps = new ConcurrentQueue<FlipInstance>();
        private static ConcurrentDictionary<Enchantment.EnchantmentType, bool> UltimateEnchants = new ConcurrentDictionary<Enchantment.EnchantmentType, bool>();

        private ConcurrentDictionary<long,bool> Subs = new ConcurrentDictionary<long, bool>();

        private ConcurrentDictionary<int,bool> AlreadyChecked = new ConcurrentDictionary<int, bool>();

        static FlipperEngine()
        {
            Instance = new FlipperEngine();
            foreach (var item in Enum.GetValues(typeof(Enchantment.EnchantmentType)))
            {
                if (item.ToString().StartsWith("ultimate_", true, null))
                    UltimateEnchants.TryAdd((Enchantment.EnchantmentType)item, true);
            }
        }

        public void AddConnection(SkyblockBackEnd con)
        {
            Subs.TryAdd(con.Id,true);
        }

        public void Test()
        {

        }

        public async void NewAuctions(IEnumerable<SaveAuction> auctions)
        {
            try {
                using(var context = new HypixelContext())
                {
                    foreach (var auction in auctions)
                    {
                        await NewAuction(auction,context);
                    }
                }
            } catch(Exception e)
            {
                dev.Logger.Instance.Error($"Flipper threw an exception {e.Message} {e.StackTrace}");
            }

        }



        public async System.Threading.Tasks.Task NewAuction(SaveAuction auction, HypixelContext context)
        {

            // determine flippability
            var price = auction.HighestBidAmount == 0 ? auction.StartingBid : (auction.HighestBidAmount * 1.1);
            if (price < 300000 || auction.Tag.Contains("RUNE"))
                return; // unflipable

            if (AlreadyChecked.ContainsKey(auction.Uuid.GetHashCode()))
                return;

            if (AlreadyChecked.Count > 20_000)
                AlreadyChecked.Clear();
            AlreadyChecked.TryAdd(auction.Uuid.GetHashCode(), true);


            var itemData = auction.NbtData.Data;
            var clearedName = auction.Reforge != ItemReferences.Reforge.None ? ItemReferences.RemoveReforge(auction.ItemName) : auction.ItemName;
            var itemId = ItemDetails.Instance.GetItemIdForName(auction.Tag, false);
            var youngest = DateTime.Now;
            var relevantEnchants = auction.Enchantments?.Where(e => UltimateEnchants.ContainsKey(e.Type) || e.Level >= 6).ToList();
            var matchingCount = relevantEnchants.Count > 2 ? relevantEnchants.Count / 2 : relevantEnchants.Count;
            var ulti = relevantEnchants.Where(e => UltimateEnchants.ContainsKey(e.Type)).FirstOrDefault();
            var ultiList = UltimateEnchants.Select(u => u.Key).ToList();
            var highLvlEnchantList = relevantEnchants.Where(e => !UltimateEnchants.ContainsKey(e.Type)).Select(a => a.Type).ToList();
            var oldest = DateTime.Now - TimeSpan.FromDays(1);

            IQueryable<SaveAuction> select = GetSelect(auction, context, clearedName, itemId, youngest, matchingCount, ulti, ultiList, highLvlEnchantList, oldest);

            var relevantAuctions = await select
                .ToListAsync();

            if(relevantAuctions.Count < 50)
            {
                // to few auctions in a day, query a week
                oldest = DateTime.Now - TimeSpan.FromDays(8);
                relevantAuctions = await GetSelect(auction, context, clearedName, itemId, youngest, matchingCount, ulti, ultiList, highLvlEnchantList, oldest,120)
                .ToListAsync();
            }

            if(relevantAuctions.Count < 3)
            {
                oldest = DateTime.Now - TimeSpan.FromDays(25);
                relevantAuctions = await GetSelect(auction, context, clearedName, itemId, youngest, matchingCount, ulti, ultiList, highLvlEnchantList, oldest)
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
                MedianPrice = (int)recomendedBuyUnder,
                Name = auction.ItemName,
                Uuid = auction.Uuid,
                LastKnownCost = (int)price,
                Volume = (float)(relevantAuctions.Count / (DateTime.Now - oldest).TotalDays)
            };
            Flipps.Enqueue(flip);
            if (Flipps.Count > 200)
                Flipps.TryDequeue(out FlipInstance result);


            FlippFound(flip);
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


            if (auction.ItemName != clearedName)
                select = select.Where(a => EF.Functions.Like(a.ItemName, "%" + clearedName));
            if (auction.Tag.StartsWith("PET"))
            {
                var sb = new StringBuilder(auction.ItemName);
                if(sb[6] == ']')
                    sb[5] = '_';
                else
                    sb[6] = '_';
                select = select.Where(a => EF.Functions.Like(a.ItemName, sb.ToString()));
            }
                
            select = AddEnchantmentSubselect(auction, matchingCount, ultiList, highLvlEnchantList, select, ultiLevel, ultiType);
            return select
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
            var message = new MessageData("flip",JSON.Stringify(flip),60);
            foreach (var item in Subs.Keys)
            {
                if(!SkyblockBackEnd.SendTo(message,item))
                    Subs.TryRemove(item,out bool value);
            }
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
        }
    }

}