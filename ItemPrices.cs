using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Coflnet;
using ConcurrentCollections;
using dev;

namespace hypixel
{
    public class ItemPrices
    {
        public static ItemPrices Instance;

        private class ItemDayLookup
        {
            public int ItemId => (int)Prices.FirstOrDefault()?.ItemId;
            public AveragePrice Oldest => Prices.FirstOrDefault();
            public List<AveragePrice> Prices;

            public ItemDayLookup()
            {

            }

            public ItemDayLookup(IEnumerable<SaveAuction> auctions)
            {
                Prices = auctions.Select(a => AverageFromAuction(a)).ToList();
            }

            public void AddNew(AveragePrice price)
            {
                Prices.Add(price);
            }

            public void AddNew(SaveAuction auction)
            {
                AddNew(AverageFromAuction(auction));
            }

            private static AveragePrice AverageFromAuction(SaveAuction auction)
            {
                return new AveragePrice()
                {
                    Avg = auction.HighestBidAmount,
                    Date = auction.End,
                    Volume = auction.Count,
                    Min = auction.HighestBidAmount,
                    Max = auction.HighestBidAmount,
                    ItemId = auction.ItemId
                };
            }

            public AveragePrice CombineIntoOne(DateTime start, DateTime end)
            {
                var complete = new AveragePrice();
                var matchingSelection = Prices
                    .Where(p => p.Date >= start && p.Date <= end)
                    .OrderBy(p => p.Date);
                if (Prices.Count() == 0)
                    return complete;
                foreach (var item in matchingSelection)
                {
                    complete.Avg += item.Avg;
                    complete.Volume += item.Volume;
                    if (complete.Max < item.Max)
                        complete.Max = item.Max;
                    if (complete.Min > item.Min)
                        complete.Min = item.Min;

                }
                complete.Avg /= Prices.Count();
                complete.Date = matchingSelection.First().Date;
                return complete;
            }

            internal void AddNew(ProductInfo item, DateTime time)
            {
                AddNew(new AveragePrice()
                {
                    ItemId = ItemDetails.Instance.GetItemIdForName(item.ProductId),
                    Max = (float)item.QuickStatus.BuyPrice,
                    Min = (float)item.QuickStatus.SellPrice,
                    Avg = (float)(item.QuickStatus.BuyPrice+item.QuickStatus.SellPrice)/2,
                    Date = time,
                    Volume = (int)item.QuickStatus.SellMovingWeek
                });
            }
        }

        private ConcurrentDictionary<int, ItemDayLookup> Hours = new ConcurrentDictionary<int, ItemDayLookup>();
        private ConcurrentDictionary<int, ItemDayLookup> IntraHour = new ConcurrentDictionary<int, ItemDayLookup>();

        private ConcurrentHashSet<string> pathsToSave = new ConcurrentHashSet<string>();

        static ItemPrices()
        {
            Instance = new ItemPrices();
        }

        public void AddNewAuction(SaveAuction auction)
        {
            if(auction.ItemId == 0)
                throw new Exception("item id is not yet set");
            

        }

        public void AddBazaarData(BazaarPull pull)
        {
            foreach (var item in pull.Products)
            {
                var res = new ItemDayLookup();
                res.AddNew(item,pull.Timestamp);
            }
        
        }


        public void ProcessHourAndDay()
        {
            foreach (var item in IntraHour)
            {
                
            }
        }



        public IEnumerable<ItemIndexElement> Search(ItemSearchQuery search)
        {
            // round to full date in 2019
            if (search.Start < new DateTime(2019, 6, 6))
                search.Start = new DateTime(2019, 6, 6);
            search.Start = RoundDown(search.Start, TimeSpan.FromDays(1));


            // by day
            for (DateTime i = search.Start; i < search.End; i = i.AddDays(1))
            {
                foreach (var item in ItemsForDay(search.name, i))
                {
                    if (item.End < search.End
                        && item.End > search.Start
                        && (search.Reforge == item.Reforge || search.Reforge == ItemReferences.Reforge.None)
                        && (search.Enchantments == null
                            || item.Enchantments != null && !search.Enchantments.Except(item.Enchantments).Any()))
                        yield return item;
                }
            }
        }

        public IEnumerable<ItemIndexElement> ItemsForDay(string itemName, DateTime date)
        {
            return null;
        }

       


        public static DateTime RoundDown(DateTime date, TimeSpan span)
        {
            return new DateTime(date.Ticks / span.Ticks * span.Ticks);
        }
    }
}
