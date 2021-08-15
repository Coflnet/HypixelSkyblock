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

        public static List<SaveAuction> SoldLastMin 
        {
            get 
            {
                return CacheService.Instance.GetFromRedis<List<SaveAuction>>("endedAuctions").Result;
            }
            set 
            {
                CacheService.Instance.SaveInRedis("endedAuctions", value).Wait(5000);
            }
        }

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
                    End = DateTime.Now,
                    UId = AuctionService.Instance.GetId(item.Uuid)
                };

                NBT.FillDetails(a, item.ItemBytes);
                return a;
            }).ToList();
            SoldLastMin = auctions;
            Updater.AddToIndexerQueue(auctions);

            Task.Run(() =>
            {
                foreach (var item in auctions)
                {
                    SubscribeEngine.Instance.BinSold(item);
                    Flipper.FlipperEngine.Instance.AuctionSold(item);
                }
            }).ConfigureAwait(false);
            Console.WriteLine($"Updated {expired.Auctions.Count} bin sells eg {expired.Auctions.FirstOrDefault()?.Uuid}");
        }
    }
}