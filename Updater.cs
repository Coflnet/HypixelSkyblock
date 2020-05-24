using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Coflnet;
using dev;
using Hypixel.NET;
using Hypixel.NET.SkyblockApi;
using Microsoft.EntityFrameworkCore;

namespace hypixel {
    public class Updater {
        private string apiKey;
        private bool abort;
        private static bool minimumOutput;

        public Updater (string apiKey) {
            this.apiKey = apiKey;
        }

        /// <summary>
        /// Downloads all auctions and save the ones that changed since the last update
        /// </summary>
        public void Update () {
            if (!minimumOutput)
                Console.WriteLine ($"Usage bevore update {System.GC.GetTotalMemory(false)}");
            var updateStartTime = DateTime.UtcNow.ToLocalTime ();

            try {
                RunUpdate (updateStartTime);
                FileController.SaveAs ("lastUpdate", updateStartTime);
                FileController.Delete ("lastUpdateStart");
            } catch (Exception e) {
                Logger.Instance.Error ($"Updating stopped because of {e.Message} {e.StackTrace}  {e.InnerException?.Message} {e.InnerException?.StackTrace}");
                FileController.Delete ("lastUpdateStart");
            }

            ItemDetails.Instance.Save ();

            StorageManager.Save ().Wait ();
            Console.WriteLine ($"Done in {DateTime.Now.ToLocalTime()}");
        }

        void RunUpdate (DateTime updateStartTime) {
            var hypixel = new HypixelApi (apiKey, 50);
            long max = 1;
            var lastUpdate = new DateTime (1970, 1, 1);
            if (FileController.Exists ("lastUpdate"))
                lastUpdate = FileController.LoadAs<DateTime> ("lastUpdate").ToLocalTime ();

            var lastUpdateStart = new DateTime (0);
            if (FileController.Exists ("lastUpdateStart"))
                lastUpdateStart = FileController.LoadAs<DateTime> ("lastUpdateStart").ToLocalTime ();

            if (!minimumOutput)
                Console.WriteLine ($"{lastUpdateStart > lastUpdate} {DateTime.Now - lastUpdateStart}");
            FileController.SaveAs ("lastUpdateStart", DateTime.Now);

            Console.WriteLine (updateStartTime);

            TimeSpan timeEst = new TimeSpan (0, 1, 1);
            Console.WriteLine ("Updating Data");

            // add extra miniute to start to catch lost auctions
            lastUpdate = lastUpdate - new TimeSpan (0, 1, 0);

            var tasks = new List<Task> ();
            int sum = 0;
            int doneCont = 0;
            object sumloc = new object ();

            for (int i = 0; i < max; i++) {
                var res = hypixel?.GetAuctionPage (i);
                if (res == null)
                    continue;
                if (i == 0) {
                    // correct update time
                    Console.WriteLine ($"Updating difference {lastUpdate} {res.LastUpdated}");
                    //lastUpdate = res.LastUpdated;
                }
                max = res.TotalPages;

                tasks.Add (Task.Run (() => {
                    var val = Save (res, lastUpdate);
                    lock (sumloc) {
                        sum += val;
                        // process done
                        doneCont++;
                    }
                    PrintUpdateEstimate (i, doneCont, sum, updateStartTime, max);
                }));
                PrintUpdateEstimate (i, doneCont, sum, updateStartTime, max);

                // try to stay under 100MB
                if (System.GC.GetTotalMemory (false) > 100000000) {
                    Console.Write ("\t\t mem: " + System.GC.GetTotalMemory (false));
                    // to much memory wait on a thread
                    //tasks[i/2].Wait();
                    //tasks[i/2].Dispose();
                    System.GC.Collect ();
                }
            }

            foreach (var item in tasks) {
                //Console.Write($"\r {index++}/{updateEstimation} \t({index}) {timeEst:mm\\:ss}");
                item?.Wait ();
                PrintUpdateEstimate (max, doneCont, sum, updateStartTime, max);
            }

        }

        internal void UpdateForEver () {
            Task.Run (() => {
                minimumOutput = true;
                while (true) {
                    var start = DateTime.Now;
                    Update ();
                    if (abort) {
                        Console.WriteLine ("Stopped updater");
                        break;
                    }
                    WaitForServerCacheRefresh (start);
                }
            });
        }

        private static void WaitForServerCacheRefresh (DateTime start) {
            var timeToSleep = start.Add (new TimeSpan (0, 1, 0)) - DateTime.Now;
            Console.WriteLine ($"Time to next Update {timeToSleep}");
            if (timeToSleep.Seconds > 0)
                Thread.Sleep (timeToSleep);
        }

        static void PrintUpdateEstimate (long i, long doneCont, long sum, DateTime updateStartTime, long max) {
            var index = sum;
            // max is doubled since it is counted twice (download and done)
            var updateEstimation = index * max * 2 / (i + 1 + doneCont) + 1;
            var ticksPassed = (DateTime.Now.ToLocalTime ().Ticks - updateStartTime.Ticks);
            var timeEst = new TimeSpan (ticksPassed / (index + 1) * updateEstimation - ticksPassed);
            if (!minimumOutput)
                Console.Write ($"\r Loading: ({i}/{max}) Done With: {doneCont} Total:{sum} {timeEst:mm\\:ss}");
        }

        // builds the index for all auctions in the last hour

        static int Save (GetAuctionPage res, DateTime lastUpdate) {
            int count = 0;
            FileController.SaveAs ($"apull/{DateTime.Now.Ticks}", res.Auctions.Where (item => {
                    ItemDetails.Instance.AddOrIgnoreDetails (item);

                    // nothing changed if the last bid is older than the last update
                    return !(item.Bids.Count > 0 && item.Bids[item.Bids.Count - 1].Timestamp < lastUpdate ||
                        item.Bids.Count == 0 && item.Start < lastUpdate);
                })
                .Select (a => {
                    count++;
                    return new SaveAuction (a);
                }));

            return count;
        }

        internal void Stop () {
            abort = true;
        }
    }
}