using System;
using System.Collections.Generic;
using System.Linq;
using dev;
using MessagePack;

namespace Coflnet.Sky.Core
{
    public partial class ItemPrices
    {
        [MessagePackObject]
        public class ItemLookup
        {
            [IgnoreMember]
            public int ItemId => (int)(Prices.FirstOrDefault() == null ? 0 : Prices.FirstOrDefault().ItemId);
            [IgnoreMember]
            public AveragePrice Oldest => Prices.FirstOrDefault();
            [IgnoreMember]
            public AveragePrice Youngest => Prices.LastOrDefault();
            [Key("p")]
            public List<AveragePrice> Prices = new List<AveragePrice>();

            public ItemLookup()
            {

            }

            public ItemLookup(IEnumerable<SaveAuction> auctions)
            {
                Prices = auctions.Select(a => AverageFromAuction(a)).ToList();
            }

            public void AddNew(AveragePrice price)
            {
                // only add if the date is newer
                if(Youngest == null || Youngest.Date < price.Date)
                    Prices.Add(price);
                else
                    throw new Exception("to early");//Console.Write("ta - " + price.ItemId);
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
                    .Where(p => p.Date >= start && p.Date <= end && p.Avg > 0)
                    .OrderBy(p => p.Date);
                if (matchingSelection.Count() == 0 || matchingSelection.First().Date.Ticks == 0)
                    return complete;

                complete.Min = Int32.MaxValue;
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
                complete.Date = matchingSelection.Where(p=>p.Date.Ticks > 0).First().Date;
                return complete;
            }

            internal void AddNew(ProductInfo item, DateTime time)
            {
                AddNew(new AveragePrice()
                {
                    ItemId = ItemDetails.Instance.GetItemIdForTag(item.ProductId, false),
                    Max = (float)item.QuickStatus.BuyPrice,
                    Min = (float)item.QuickStatus.SellPrice,
                    Avg = (float)(item.QuickStatus.BuyPrice + item.QuickStatus.SellPrice) / 2,
                    Date = time,
                    Volume = (int)item.QuickStatus.SellMovingWeek / 7 / 24 / 60 / 6
                });
            }

            internal void Discard(DateTime allBefore)
            {
                Prices = Prices.Where(p => p.Date >= allBefore).ToList();
            }
        }
    }
}
