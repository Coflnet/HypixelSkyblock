using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hypixel.NET;

namespace hypixel
{
    public class BinUpdater
    {
        private List<string> apiKeys = new List<string>();

        /// <summary>
        /// Dictionary of minecraft player ids and how many auctions they had to block pulling their whole history twice
        /// </summary>
        /// <typeparam name="string"></typeparam>
        /// <typeparam name="short"></typeparam>
        /// <returns></returns>
        private static ConcurrentDictionary<uint, short> PulledAlready = new ConcurrentDictionary<uint, short>();

        public BinUpdater(IEnumerable<string> apiKeys)
        {
            this.apiKeys.AddRange(apiKeys);
            historyLimit = DateTime.Now - TimeSpan.FromHours(1);
        }

        private DateTime historyLimit;

        public static void GrabAuctions(HypixelApi hypixelApi)
        {
            var expired = hypixelApi.getAuctionsEnded();
            var auctions = expired.Auctions.Select(item =>
            {
                var a = new SaveAuction()
                {
                    Uuid = item.Uuid,
                    AuctioneerId = item.Seller,
                    Bids = new List<SaveBids>()
                        {
                            new SaveBids()
                            {
                                Amount = item.Price,
                                    Bidder = item.Buyer,
                                    Timestamp = item.TimeStamp,
                                    ProfileId = "unknown"
                            }
                        },
                    HighestBidAmount = item.Price,
                    Bin = item.BuyItemNow,
                    End = DateTime.Now
                };

                NBT.FillDetails(a, item.ItemBytes);
                return a;
            });
            Indexer.AddToQueue(auctions);

            Task.Run(async () =>
            {
                foreach (var item in auctions)
                {
                    // has to be faster
                    SubscribeEngine.Instance.BinSold(item);
                }
                await Task.Delay(10000);
                ItemPrices.Instance.AddNewAuctions(auctions);
            });
            Console.WriteLine($"Updated {expired.Auctions.Count} bin sells eg {expired.Auctions.First().Uuid}");
        }
    }
}