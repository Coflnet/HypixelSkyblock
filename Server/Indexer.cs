using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Coflnet;
using dev;
using Microsoft.EntityFrameworkCore;

namespace hypixel
{
    public class Indexer
    {
        private const int MAX_QUEUE_SIZE = 10000;
        private const int AUCTION_CHUNK_SIZE = 1000;
        private static bool abort;
        private static bool minimumOutput;
        public static int IndexedAmount => count;
        public static int QueueCount => auctionsQueue.Count;

        private static int count;
        public static DateTime LastFinish { get; internal set; }

        private static ConcurrentQueue<SaveAuction> auctionsQueue = new ConcurrentQueue<SaveAuction>();

        private static void AddToQueue(SaveAuction auction)
        {
            auctionsQueue.Enqueue(auction);

            if (QueueCount > MAX_QUEUE_SIZE)
                PersistQueueBatch();
        }

        public static void AddToQueue(IEnumerable<SaveAuction> auctionsToAdd)
        {
            //Console.WriteLine($"Adding {auctionsToAdd.Count()} todoauctions");
            foreach (var item in auctionsToAdd)
            {
                AddToQueue(item);
            }
            SubscribeEngine.Instance.PushOrIgnore(auctionsToAdd);
        }

        private static void PersistQueueBatch()
        {
            FileController.SaveAs($"apull/{DateTime.Now.Ticks + 1}", TakeBatch(AUCTION_CHUNK_SIZE));
        }

        public static async Task LastHourIndex()
        {
            DateTime indexStart;
            string targetTmp, pullPath;
            VariableSetup(out indexStart, out targetTmp, out pullPath);
            //DeleteDir(targetTmp);
            if (!Directory.Exists(pullPath) && !Directory.Exists(targetTmp))
            {
                // update first
                if (!Program.FullServerMode)
                    Console.WriteLine("nothing to build indexes from, run again with option u first");
                return;
            }
            // only copy the pull path if there is no temp work path yet
            if (!Directory.Exists(targetTmp))
                Directory.Move(pullPath, targetTmp);
            else
                Console.WriteLine("Resuming work");

            try
            {
                Console.WriteLine("working");

                var work = PullData();
                var earlybreak = 100;
                foreach (var item in work)
                {
                    await ToDb(item);
                    if (earlybreak-- <= 0)
                        break;
                }

                Console.WriteLine($"Indexing done, Indexed: {count} Saved: {StorageManager.SavedOnDisc} \tcache: {StorageManager.CacheItems}  NameRequests: {Program.RequestsSinceStart}");

                if (!abort)
                    // successful made this index save the startTime
                    FileController.SaveAs("lastIndex", indexStart);
            }
            catch (System.AggregateException e)
            {
                // oh no an error occured
                Logger.Instance.Error($"An error occured while indexing, abording: {e.Message} {e.StackTrace}");
                return;
                //FileController.DeleteFolder("auctionpull");

                //Directory.Move(FileController.GetAbsolutePath("awork"),FileController.GetAbsolutePath("auctionpull"));

            }
            var saveTask = StorageManager.Save();
            saveTask.Wait();
            LastFinish = DateTime.Now;

            DeleteDir(targetTmp);
        }

        public static async void ProcessQueue()
        {
            var chuckCount = 1000;
            for (int i = 0; i < auctionsQueue.Count / 1000 + 1; i++)
            {
                List<SaveAuction> batch = TakeBatch(chuckCount);
                try
                {
                    await ToDb(batch);
                }
                catch (Exception e)
                {
                    AddToQueue(batch);
                    throw e;
                }
            }
            LastFinish = DateTime.Now;
        }

        private static List<SaveAuction> TakeBatch(int chuckCount)
        {
            var batch = new List<SaveAuction>();
            for (int index = 0; index < chuckCount; index++)
            {
                if (auctionsQueue.TryDequeue(out SaveAuction a))
                    batch.Add(a);
            }

            return batch;
        }

        private static void VariableSetup(out DateTime indexStart, out string targetTmp, out string pullPath)
        {
            indexStart = DateTime.Now;
            if (!Program.FullServerMode)
                Console.WriteLine($"{indexStart}");
            var lastIndexStart = new DateTime(2020, 4, 25);
            if (FileController.Exists("lastIndex"))
                lastIndexStart = FileController.LoadAs<DateTime>("lastIndex");
            lastIndexStart = lastIndexStart - new TimeSpan(0, 20, 0);
            targetTmp = FileController.GetAbsolutePath("awork");
            pullPath = FileController.GetAbsolutePath("apull");
        }

        static IEnumerable<List<SaveAuction>> PullData()
        {
            var path = "awork";
            foreach (var item in FileController.FileNames("*", path))
            {
                if (abort)
                {
                    Console.WriteLine("Stopped indexer");
                    yield break;
                }
                var fullPath = $"{path}/{item}";
                List<SaveAuction> data = null;
                try
                {
                    data = FileController.LoadAs<List<SaveAuction>>(fullPath);
                }
                catch (Exception)
                {
                    Console.WriteLine("could not load downloaded auction-buffer");
                    FileController.Move(fullPath, "correupted/" + fullPath);
                }
                if (data != null)
                {
                    yield return data;
                    FileController.Delete(fullPath);
                }
            }
        }

        public static int highestPlayerId = 1;

        private static async Task ToDb(List<SaveAuction> auctions)
        {

            auctions = auctions.Distinct(new AuctionComparer()).ToList();

            using (var context = new HypixelContext())
            {
                List<string> playerIds = new List<string>();
                Dictionary<string, SaveAuction> inDb = await GetExistingAuctions(auctions, context);

                var comparer = new BidComparer();

                foreach (var auction in auctions)
                {
                    ProcessAuction(context, playerIds, inDb, comparer, auction);

                }

                foreach (var player in playerIds)
                {
                    Program.AddPlayer(context, player, ref highestPlayerId);
                }
                //Program.AddPlayers (context, playerIds);

                await context.SaveChangesAsync();
                context.Dispose();
            }
        }

        private static void ProcessAuction(HypixelContext context, List<string> playerIds, Dictionary<string, SaveAuction> inDb, BidComparer comparer, SaveAuction auction)
        {
            try
            {
                AddPlayerId(playerIds, auction);

                var id = auction.Uuid;
                MigrateAuction(auction);

                if (inDb.TryGetValue(id, out SaveAuction dbauction))
                {
                    UpdateAuction(context, comparer, auction, dbauction);
                }
                else
                {
                    context.Auctions.Add(auction);
                }

                count++;
                if (!minimumOutput && count % 5 == 0)
                    Console.Write($"\r         Indexed: {count} Saved: {StorageManager.SavedOnDisc} \tcache: {StorageManager.CacheItems}  NameRequests: {Program.RequestsSinceStart}");

            }
            catch (Exception e)
            {
                Logger.Instance.Error($"Error {e.Message} on {auction.ItemName} {auction.Uuid} from {auction.AuctioneerId}");
                Logger.Instance.Error(e.StackTrace);
            }
        }

        private static void MigrateAuction(SaveAuction auction)
        {
            if (auction.Reforge == ItemReferences.Reforge.Migration)
                auction.Reforge = ItemReferences.Reforge.Unknown;
        }

        private static void UpdateAuction(HypixelContext context, BidComparer comparer, SaveAuction auction, SaveAuction dbauction)
        {
            foreach (var bid in auction.Bids)
            {

                bid.Auction = dbauction;
                if (!dbauction.Bids.Contains(bid, comparer))
                {
                    context.Bids.Add(bid);
                    dbauction.HighestBidAmount = auction.HighestBidAmount;
                }
            }
            if (auction.Bin)
            {
                dbauction.Bin = true;
            }
            if (dbauction.ItemName == null)
                dbauction.ItemName = auction.ItemName;
            if (dbauction.ProfileId == null)
                dbauction.ProfileId = auction.ProfileId;
            if (dbauction.Start == default(DateTime))
                dbauction.Start = auction.Start;
            if (dbauction.End == default(DateTime))
                dbauction.End = auction.End;
            if (dbauction.Category == Category.UNKNOWN)
                dbauction.Category = auction.Category;

            // update
            context.Auctions.Update(dbauction);
        }

        private static async Task<Dictionary<string, SaveAuction>> GetExistingAuctions(List<SaveAuction> auctions, HypixelContext context)
        {
            // preload
            return (await context.Auctions.Where(a => auctions.Select(oa => oa.Uuid)
                .Contains(a.Uuid)).Include(a => a.Bids).ToListAsync())
                .ToDictionary(a => a.Uuid);
        }

        private static void AddPlayerId(List<string> playerIds, SaveAuction auction)
        {
            if (auction?.AuctioneerId == null)
                return;

            playerIds?.Add(auction?.AuctioneerId);
            if (auction?.Bids == null)
                return;
            foreach (var bid in auction?.Bids)
            {
                //Program.AddPlayer (context, bid.Bidder);
                playerIds.Add(bid.Bidder);
            }
        }

        internal static void Stop()
        {
            abort = true;
            while (QueueCount > 1)
                PersistQueueBatch();
        }


        private static void DeleteDir(string path)
        {
            if (!Directory.Exists(path))
            {
                // nothing to do
                return;
            }

            System.IO.DirectoryInfo di = new DirectoryInfo(path);

            foreach (FileInfo file in di.GetFiles())
            {
                file.Delete();
            }
            foreach (DirectoryInfo dir in di.GetDirectories())
            {
                dir.Delete(true);
            }
            Directory.Delete(path);
        }

        internal static void MiniumOutput()
        {
            minimumOutput = true;
        }

        public static void AvgPriceHistory()
        {
            using (var context = new HypixelContext())
            {
                var itemId = 1;

                context.Prices.AddRange(
                    context.Auctions.Where(item => item.ItemId == itemId && item.HighestBidAmount > 0).GroupBy(item => new { item.End.Year, item.End.Month, item.End.Day })
                    .Select(item =>
                        new
                        {
                            End = new DateTime(item.Key.Year, item.Key.Month, item.Key.Day, 0, 0, 0),
                            Avg = (int)item.Average(a => ((int)a.HighestBidAmount) / a.Count),
                            Max = (int)item.Max(a => ((int)a.HighestBidAmount) / a.Count),
                            Min = (int)item.Min(a => ((int)a.HighestBidAmount) / a.Count),
                            Count = item.Sum(a => a.Count)
                        }).ToList()
                    .Select(i => new AveragePrice()
                    {
                        Volume = i.Count,
                        Avg = i.Avg,
                        Max = i.Max,
                        Min = i.Min,
                        Date = i.End,
                        ItemId = itemId
                    }));

                context.SaveChanges();
            }
        }

        internal static void LoadFromDB()
        {
            using (var context = new HypixelContext())
            {
                if (context.Players.Any())
                    highestPlayerId = context.Players.Max(p => p.Id) + 1;
            }
        }

        internal static async Task NumberUsers()
        {

            Task bidNumberTask = null;
            using (var context = new HypixelContext())
            {
                var unindexedPlayers = await context.Players.Where(p => p.Id == 0).Take(2000).ToListAsync();
                if (unindexedPlayers.Any())
                {
                    Console.Write($"  numbering: {unindexedPlayers.Count()} ");
                    foreach (var player in unindexedPlayers)
                    {
                        player.Id = System.Threading.Interlocked.Increment(ref highestPlayerId);
                        context.Players.Update(player);
                    }
                }
                else
                {
                    // all players in the db have an id now
                    bidNumberTask = Task.Run(NumberBids);
                    var auctionsWithoutSellerId = await context
                        .Auctions.Where(a => a.SellerId == 0)
                        .Include(a => a.Enchantments)
                        .OrderByDescending(a => a.Id)
                        .Take(5000).ToListAsync();
                    if (auctionsWithoutSellerId.Count() > 0)
                        Console.Write(" -#-");
                    foreach (var auction in auctionsWithoutSellerId)
                    {

                        try
                        {
                            auction.SellerId = GetOrCreatePlayerId(context, auction.AuctioneerId); // context.Players.Where(p => p.UuId == auction.AuctioneerId).Select(p => p.Id).FirstOrDefault();
                            var id = ItemDetails.Instance.GetOrCreateItemIdForAuction(auction, context);
                            auction.ItemId = id;


                            foreach (var enchant in auction.Enchantments)
                            {
                                enchant.ItemType = auction.ItemId;
                            }
                            context.Auctions.Update(auction);

                        }
                        catch (Exception e)
                        {
                            Console.WriteLine($"Problem with item {Newtonsoft.Json.JsonConvert.SerializeObject(auction)}");
                            Console.WriteLine($"Error occured while userIndexing: {e.Message} {e.StackTrace}\n {e.InnerException?.Message} {e.InnerException?.StackTrace}");
                        }
                    }
                }
                await context.SaveChangesAsync();
            }
            if (bidNumberTask != null)
                await bidNumberTask;
        }

        static int batchSize = 10000;

        private static async void NumberBids()
        {
            using (var context = new HypixelContext())
            {
                var bidsWithoutSellerId = await context.Bids.Where(a => a.BidderId == 0).Take(batchSize).ToListAsync();
                foreach (var bid in bidsWithoutSellerId)
                {

                    bid.BidderId = GetOrCreatePlayerId(context, bid.Bidder);
                    context.Bids.Update(bid);
                }

                await context.SaveChangesAsync();
            }
        }

        private static int GetOrCreatePlayerId(HypixelContext context, string uuid)
        {
            var id = context.Players.Where(p => p.UuId == uuid).Select(p => p.Id).FirstOrDefault();
            if (id == 0)
            {
                id = Program.AddPlayer(context, uuid, ref highestPlayerId);
                Console.WriteLine($"Adding player {id} ");
            }
            return id;
        }
    }
}