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
            var auctions = expired.Auctions.Select(item=>{
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
                        Bin = item.BuyItemNow
                };

                NBT.FillDetails(a, item.ItemBytes);
                return a;
            });
            Indexer.AddToQueue(auctions);
            Console.WriteLine($"Updated {expired.Auctions.Count} bin sells eg {expired.Auctions.First().Uuid}");
        }

        public void GrabAuctionsWithIds(IEnumerable<BinInfo> info)
        {
            using(var context = new HypixelContext())
            {
                /*var auctioneerIdsBasic = context.Auctions
                    .Where(a => uuids.Contains(a.Uuid))
                    .Select(a => a.AuctioneerId)
                    .Distinct()
                    .ToList();*/
                var auctioneerIdsBasic = info.Select(i => i.Auctioneer).Distinct();

                ConcurrentQueue<string> sellerIds = new ConcurrentQueue<string>();
                foreach (var item in auctioneerIdsBasic)
                {
                    sellerIds.Enqueue(item);
                }

                Console.WriteLine($"Updating {info.Count()} binauctions from {auctioneerIdsBasic.Count()} Players");

                List<Task> tasks = new List<Task>();
                var updatedCount = 0;
                var totalCount = 0;

                foreach (var key in apiKeys)
                {

                    tasks.Add(Task.Run(() =>
                    {
                        RunWorker(sellerIds, ref updatedCount, ref totalCount, key);
                    }));
                }

                foreach (var item in tasks)
                {
                    item.Wait();
                }
                Console.WriteLine($"+++ Updated {updatedCount} bin auctions of {auctioneerIdsBasic.Count()} ({totalCount} total) +++");
                context.SaveChanges();
            }
            if (PulledAlready.Count() > 50000)
                PulledAlready.Clear();
        }

        private void RunWorker(ConcurrentQueue<string> sellerIds, ref int updatedCount, ref int totalCount, string key)
        {
            try
            {
                var hypixelApi = new HypixelApi(key, 10);
                var index = 0;
                while (index++ < 60 && sellerIds.TryDequeue(out string uuid))
                {
                    var playerPage = hypixelApi.GetAuctionsByPlayerUuid(uuid);
                    var shortId = Convert.ToUInt32(uuid.Substring(24, 8), 16); //uuid.Substring(24);
                    var ignoreHistory = false;
                    if (PulledAlready.TryGetValue(shortId, out short value))
                    {
                        ignoreHistory = true;
                    }
                    foreach (var item in playerPage.Auctions)
                    {
                        // exclude items we certainly already have
                        if ((!item.BuyItNow || item.Bids.Count == 0) &&
                            item.End > DateTime.Now ||
                            ignoreHistory && item.End < historyLimit)
                            continue;
                        //Indexer.AddToQueue(new SaveAuction(item));
                        Interlocked.Increment(ref totalCount);
                        throw new Exception("enque was deprecated");


                    }
                    Interlocked.Increment(ref updatedCount);
                    if (playerPage.Auctions.Count > 2)
                    {
                        var amount = (short) playerPage.Auctions.Count;
                        //if(value == 0 && index %50 == 0)
                        //    Console.WriteLine($"Ignoring player {uuid} {PulledAlready.Count()}");
                        PulledAlready.AddOrUpdate(shortId, amount, (key, value) => amount);
                    }
                }
            }
            catch (ApplicationException e)
            {
                Console.Write($" xx {e.Message} for {key} xx ");
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error on BinUpdate: {e.Message} {e.StackTrace} {e.InnerException?.Message} ");
            }
        }

    }
}