using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Coflnet;
using Confluent.Kafka;
using dev;
using Hypixel.NET;
using Hypixel.NET.SkyblockApi;

namespace hypixel
{
    public class Updater
    {
        private const string LAST_UPDATE_KEY = "lastUpdate";
        private string apiKey;
        private bool abort;
        private static bool minimumOutput;

        private static string MissingAuctionsTopic = SimplerConfig.Config.Instance["TOPICS:MISSING_AUCTION"];
        private static string SoldAuctionsTopic = SimplerConfig.Config.Instance["TOPICS:SOLD_AUCTION"];
        private static string NewAuctionsTopic = SimplerConfig.Config.Instance["TOPICS:NEW_AUCTION"];
        private static string AuctionEndedTopic = SimplerConfig.Config.Instance["TOPICS:AUCTION_ENDED"];
        private static string NewBidsTopic = SimplerConfig.Config.Instance["TOPICS:NEW_BID"];

        private static bool doFullUpdate = false;
        Prometheus.Counter auctionUpdateCount = Prometheus.Metrics.CreateCounter("auction_update", "How many auctions were updated");

        public event Action OnNewUpdateStart;
        /// <summary>
        /// Gets invoked when an update is done
        /// </summary>
        public event Action OnNewUpdateEnd;

        public static DateTime LastPull { get; internal set; }
        public static int UpdateSize { get; internal set; }

        private static ConcurrentDictionary<string, BinInfo> LastUpdateBins = new ConcurrentDictionary<string, BinInfo>();

        private static ConcurrentDictionary<string, bool> ActiveAuctions = new ConcurrentDictionary<string, bool>();
        private static ConcurrentDictionary<string, DateTime> MissingSince = new ConcurrentDictionary<string, DateTime>();

        ConcurrentDictionary<string, int> AuctionCount;
        public static ConcurrentDictionary<string, int> LastAuctionCount;

        /// <summary>
        /// Limited task factory
        /// </summary>
        TaskFactory taskFactory;
        private HypixelApi hypixel;

        public Updater(string apiKey)
        {
            this.apiKey = apiKey;

            var scheduler = new LimitedConcurrencyLevelTaskScheduler(2);
            taskFactory = new TaskFactory(scheduler);
        }

        /// <summary>
        /// Downloads all auctions and save the ones that changed since the last update
        /// </summary>
        public async Task<DateTime> Update(bool updateAll = false)
        {
            doFullUpdate = updateAll;
            if (!minimumOutput)
                Console.WriteLine($"Usage bevore update {System.GC.GetTotalMemory(false)}");
            var updateStartTime = DateTime.UtcNow.ToLocalTime();

            try
            {
                if (hypixel == null)
                    hypixel = new HypixelApi(apiKey, 50);

                if (lastUpdateDone == default(DateTime))
                    lastUpdateDone = await CacheService.Instance.GetFromRedis<DateTime>(LAST_UPDATE_KEY);

                if (lastUpdateDone == default(DateTime))
                    lastUpdateDone = new DateTime(2017, 1, 1);
                lastUpdateDone = await RunUpdate(lastUpdateDone);
                FileController.SaveAs(LAST_UPDATE_KEY, lastUpdateDone);
                await CacheService.Instance.SaveInRedis(LAST_UPDATE_KEY, lastUpdateDone);
                FileController.Delete("lastUpdateStart");
            }
            catch (Exception e)
            {
                Logger.Instance.Error($"Updating stopped because of {e.Message} {e.StackTrace}  {e.InnerException?.Message} {e.InnerException?.StackTrace}");
                FileController.Delete("lastUpdateStart");
                throw e;
            }

            ItemDetails.Instance.Save();

            await StorageManager.Save();
            return lastUpdateDone;
        }

        DateTime lastUpdateDone = default(DateTime);

        async Task<DateTime> RunUpdate(DateTime updateStartTime)
        {
            var binupdate = Task.Run(()
                => BinUpdater.GrabAuctions(hypixel)).ConfigureAwait(false);

            long max = 1;
            var lastUpdate = lastUpdateDone; // new DateTime (1970, 1, 1);
            //if (FileController.Exists ("lastUpdate"))
            //    lastUpdate = FileController.LoadAs<DateTime> ("lastUpdate").ToLocalTime ();

            var lastUpdateStart = new DateTime(0);
            if (FileController.Exists("lastUpdateStart"))
                lastUpdateStart = FileController.LoadAs<DateTime>("lastUpdateStart").ToLocalTime();

            if (!minimumOutput)
                Console.WriteLine($"{lastUpdateStart > lastUpdate} {DateTime.Now - lastUpdateStart}");
            FileController.SaveAs("lastUpdateStart", DateTime.Now);

            TimeSpan timeEst = new TimeSpan(0, 1, 1);
            Console.WriteLine($"Updating Data {DateTime.Now}");

            // add extra miniute to start to catch lost auctions
            lastUpdate = updateStartTime - new TimeSpan(0, 1, 0);
            DateTime lastHypixelCache = lastUpdate;

            var tasks = new List<Task>();
            int sum = 0;
            int doneCont = 0;
            object sumloc = new object();
            var firstPage = await hypixel?.GetAuctionPageAsync(0);
            max = firstPage.TotalPages;
            if (firstPage.LastUpdated == updateStartTime)
            {
                // wait for the server cache to refresh
                await Task.Delay(5000);
                return updateStartTime;
            }
            OnNewUpdateStart?.Invoke();

            var cancelToken = new CancellationToken();
            AuctionCount = new ConcurrentDictionary<string, int>();

            var activeUuids = new ConcurrentDictionary<string, bool>();
            for (int i = 0; i < max; i++)
            {
                var index = i;
                await Task.Delay(100);
                tasks.Add(taskFactory.StartNew(async () =>
                {
                    try
                    {
                        var res = index != 0 ? await hypixel?.GetAuctionPageAsync(index) : firstPage;
                        if (res == null)
                            return;

                        max = res.TotalPages;

                        if (index == 0)
                        {
                            lastHypixelCache = res.LastUpdated;
                            // correct update time
                            Console.WriteLine($"Updating difference {lastUpdate} {res.LastUpdated}\n");
                        }

                        var val = await Save(res, lastUpdate, activeUuids);
                        lock (sumloc)
                        {
                            sum += val;
                            // process done
                            doneCont++;
                        }
                        PrintUpdateEstimate(index, doneCont, sum, updateStartTime, max);
                    }
                    catch (Exception e)
                    {
                        try // again
                        {
                            var res = await hypixel?.GetAuctionPageAsync(index);
                            var val = await Save(res, lastUpdate, activeUuids);
                        }
                        catch (System.Exception)
                        {
                            Logger.Instance.Error($"Single page ({index}) could not be loaded twice because of {e.Message} {e.StackTrace} {e.InnerException?.Message}");
                        }
                    }

                }, cancelToken).Unwrap());
                PrintUpdateEstimate(i, doneCont, sum, updateStartTime, max);

                // try to stay under 600MB
                if (System.GC.GetTotalMemory(false) > 500000000)
                {
                    Console.Write("\t mem: " + System.GC.GetTotalMemory(false));
                    System.GC.Collect();
                }
                //await Task.Delay(100);
            }

            foreach (var item in tasks)
            {
                //Console.Write($"\r {index++}/{updateEstimation} \t({index}) {timeEst:mm\\:ss}");
                if (item != null)
                    await item;
                PrintUpdateEstimate(max, doneCont, sum, updateStartTime, max);
            }

            if (AuctionCount.Count > 2)
                LastAuctionCount = AuctionCount;

            //BinUpdateSold(currentUpdateBins);
            var lastUuids = ActiveAuctions;
            ActiveAuctions = activeUuids;
            var canceledTask = Task.Run(() =>
            {
                RemoveCanceled(lastUuids);
            }).ConfigureAwait(false);

            if (sum > 10)
                LastPull = DateTime.Now;

            Console.WriteLine($"Updated {sum} auctions {doneCont} pages");
            UpdateSize = sum;

            doFullUpdate = false;
            OnNewUpdateEnd?.Invoke();

            return lastHypixelCache;
        }

        /// <summary>
        /// Takes care of removing canceled auctions
        /// Will check 5 updates to make sure there wasn't just a page missing
        /// </summary>
        /// <param name="lastUuids"></param>
        private static void RemoveCanceled(ConcurrentDictionary<string, bool> lastUuids)
        {
            foreach (var item in ActiveAuctions.Keys)
            {
                lastUuids.TryRemove(item, out bool val);
                MissingSince.TryRemove(item, out DateTime value);
            }

            foreach (var item in BinUpdater.SoldLastMin)
            {
                lastUuids.TryRemove(item.Uuid, out bool val);
            }

            foreach (var item in lastUuids)
            {
                MissingSince[item.Key] = DateTime.Now;
                // its less important if items are removed from the flipper than globally
                // the flipper should not display inactive auctions at all
                Flipper.FlipperEngine.Instance.AuctionInactive(item.Key);
            }
            var removed = new HashSet<string>();
            foreach (var item in MissingSince)
            {
                if (item.Value < DateTime.Now - TimeSpan.FromMinutes(5))
                    removed.Add(item.Key);
            }
            ProduceIntoTopic(removed.Select(id => new SaveAuction(id)), MissingAuctionsTopic);
            foreach (var item in removed)
            {
                MissingSince.TryRemove(item, out DateTime since);
            }
            Console.WriteLine($"Canceled last min: {removed.Count} {removed.FirstOrDefault()}");
        }

        internal void UpdateForEver()
        {
            CancellationTokenSource source = new CancellationTokenSource();
            // Fail save
            Task.Run(async () =>
            {
                while (true)
                {
                    await Task.Delay(TimeSpan.FromMinutes(5));
                    if (lastUpdateDone > DateTime.Now.Subtract(TimeSpan.FromMinutes(6)))
                        continue;
                    dev.Logger.Instance.Error("Restarting updater");
                    source.Cancel();
                    source = new CancellationTokenSource();
                    StartNewUpdater(source.Token);
                }
            }).ConfigureAwait(false);
            StartNewUpdater(source.Token);
        }

        private void StartNewUpdater(CancellationToken token)
        {
            Task.Run(async () =>
            {
                minimumOutput = true;
                var updaterStart = DateTime.Now.RoundDown(TimeSpan.FromMinutes(1));
                while (true)
                {
                    try
                    {
                        var start = DateTime.Now;
                        // do a full update 6 min after start
                        var shouldDoFullUpdate = DateTime.Now.Subtract(TimeSpan.FromMinutes(6)).RoundDown(TimeSpan.FromMinutes(1)) == updaterStart;
                        var lastCache = await Update(shouldDoFullUpdate);
                        if (abort || token.IsCancellationRequested)
                        {
                            Console.WriteLine("Stopped updater");
                            break;
                        }
                        Console.WriteLine($"--> started updating {start} cache says {lastCache} now its {DateTime.Now}");
                        await WaitForServerCacheRefresh(lastCache);
                    }
                    catch (Exception e)
                    {
                        Logger.Instance.Error("Updater encountered an outside error: " + e.Message);
                        await Task.Delay(15000);
                    }

                }
            }, token).ConfigureAwait(false);
        }

        private static async Task WaitForServerCacheRefresh(DateTime hypixelCacheTime)
        {
            // cache refreshes every 60 seconds, 2 seconds extra to fix timing issues
            var timeToSleep = hypixelCacheTime.Add(TimeSpan.FromSeconds(62)) - DateTime.Now;
            if (timeToSleep.Seconds > 0)
                await Task.Delay(timeToSleep);
        }

        static void PrintUpdateEstimate(long i, long doneCont, long sum, DateTime updateStartTime, long max)
        {
            var index = sum;
            // max is doubled since it is counted twice (download and done)
            var updateEstimation = index * max * 2 / (i + 1 + doneCont) + 1;
            var ticksPassed = (DateTime.Now.ToLocalTime().Ticks - updateStartTime.Ticks);
            var timeEst = new TimeSpan(ticksPassed / (index + 1) * updateEstimation - ticksPassed);
            if (!minimumOutput)
                Console.Write($"\r Loading: ({i}/{max}) Done With: {doneCont} Total:{sum} {timeEst:mm\\:ss}");
        }

        // builds the index for all auctions in the last hour

        Task<int> Save(GetAuctionPage res, DateTime lastUpdate, ConcurrentDictionary<string, bool> activeUuids)
        {
            int count = 0;

            var processed = res.Auctions.Where(item =>
                {
                    activeUuids[item.Uuid] = true;
                    // nothing changed if the last bid is older than the last update
                    return !(item.Bids.Count > 0 && item.Bids[item.Bids.Count - 1].Timestamp < lastUpdate ||
                        item.Bids.Count == 0 && item.Start < lastUpdate) || doFullUpdate;
                })
                .Select(a =>
                {
                    if (Program.Migrated)
                        ItemDetails.Instance.AddOrIgnoreDetails(a);
                    count++;
                    var auction = new SaveAuction(a);
                    return auction;
                }).ToList();

            // prioritise the flipper
            var started = processed.Where(a => a.Start > lastUpdate).ToList();
            var min = DateTime.Now - TimeSpan.FromMinutes(15);
            //AddToFlipperCheckQueue(started.Where(a => a.Start > min));

            ProduceIntoTopic(processed.Where(a=>a.Start > lastUpdate), NewAuctionsTopic);
            ProduceIntoTopic(processed.Where(item=>item.Bids.Count > 0 && item.Bids[item.Bids.Count - 1].Timestamp > lastUpdate), NewBidsTopic);


            if (DateTime.Now.Minute % 30 == 7)
                foreach (var a in res.Auctions)
                {
                    var auction = new SaveAuction(a);
                    AuctionCount.AddOrUpdate(auction.Tag, k =>
                    {
                        return DetermineWorth(0, auction);
                    }, (k, c) =>
                    {
                        return DetermineWorth(c, auction);
                    });
                }

            var ended = res.Auctions.Where(a => a.End < DateTime.Now).Select(a => new SaveAuction(a));
            ProduceIntoTopic(ended, AuctionEndedTopic);

            auctionUpdateCount.Inc(count);

            return Task.FromResult(count);
        }

        private class Serializer : ISerializer<SaveAuction>
        {
            public static Serializer Instance = new Serializer();
            public byte[] Serialize(SaveAuction data, SerializationContext context)
            {
                return MessagePack.MessagePackSerializer.Serialize(data);
            }
        }

        private static ProducerConfig producerConfig = new ProducerConfig { BootstrapServers = SimplerConfig.Config.Instance["KAFKA_HOST"] };

        static Action<DeliveryReport<string, SaveAuction>> handler = r =>
            {
                if (r.Error.IsError || r.TopicPartitionOffset.Offset % 1000 == 10)
                    Console.WriteLine(!r.Error.IsError
                        ? $"Delivered {r.Topic} {r.Offset} "
                        : $"\nDelivery Error {r.Topic}: {r.Error.Reason}");
            };

        public static void AddSoldAuctions(IEnumerable<SaveAuction> auctionsToAdd)
        {
            ProduceIntoTopic(auctionsToAdd, SoldAuctionsTopic);
        }

        private static void ProduceIntoTopic(IEnumerable<SaveAuction> auctionsToAdd, string targetTopic)
        {
            using (var p = new ProducerBuilder<string, SaveAuction>(producerConfig).SetValueSerializer(Serializer.Instance).Build())
            {
                foreach (var item in auctionsToAdd)
                {
                    p.Produce(targetTopic, new Message<string, SaveAuction> { Value = item, Key = $"{item.UId.ToString()}{item.Bids.Count}{item.End}" }, handler);
                }

                // wait for up to 10 seconds for any inflight messages to be delivered.
                p.Flush(TimeSpan.FromSeconds(10));
            }
        }

        public static void AddToFlipperCheckQueue(IEnumerable<SaveAuction> auctionsToAdd)
        {
            ProduceIntoTopic(auctionsToAdd,"sky-flipper");
        }

        private static int DetermineWorth(int c, SaveAuction auction)
        {
            var price = auction.HighestBidAmount == 0 ? auction.StartingBid : auction.HighestBidAmount;
            if (price > 500_000)
                return c + 1;
            return c - 20;
        }

        internal void Stop()
        {
            abort = true;
        }
    }
}
