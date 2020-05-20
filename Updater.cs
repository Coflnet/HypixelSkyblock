using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Coflnet;
using dev;
using Hypixel.NET;
using Hypixel.NET.SkyblockApi;

namespace hypixel
{
    public class Updater
    {
        private string apiKey;

        public Updater(string apiKey)
        {
            this.apiKey = apiKey;
        }


        /// <summary>
        /// Downloads all auctions and save the ones that changed since the last update
        /// </summary>
        public void Update () {
            Console.WriteLine($"Usage bevore update {System.GC.GetTotalMemory(false)}");
            var updateStartTime = DateTime.UtcNow.ToLocalTime ();
            
            try {
                RunUpdate(updateStartTime);
                FileController.SaveAs ("lastUpdate", updateStartTime);
            } catch(Exception e)
            {
                Logger.Instance.Error($"Updating stopped because of {e.Message} {e.StackTrace}  {e.InnerException?.StackTrace}");
                FileController.Delete("lastUpdateStart");
            }

            ItemDetails.Instance.Save();


            StorageManager.Save ().Wait();
            Console.WriteLine($"Done in {DateTime.Now.ToLocalTime()}");
        }

        void RunUpdate(DateTime updateStartTime)
        {
            var hypixel = new HypixelApi (apiKey, 50);
            long max = 1;
            var lastUpdate = new DateTime (1970,1,1);
            if (FileController.Exists ("lastUpdate"))
                lastUpdate = FileController.LoadAs<DateTime> ("lastUpdate").ToLocalTime ();

            var lastUpdateStart = new DateTime (0);
            if (FileController.Exists ("lastUpdateStart"))
                lastUpdateStart = FileController.LoadAs<DateTime> ("lastUpdateStart").ToLocalTime ();

            if(lastUpdateStart > lastUpdate && DateTime.Now - lastUpdateStart  < new TimeSpan(0,5,0))
            {
                Console.WriteLine("Last update start was to recent");
                return;
            }
            Console.WriteLine($"{lastUpdateStart > lastUpdate} {DateTime.Now - lastUpdateStart}");
            FileController.SaveAs("lastUpdateStart",DateTime.Now);


            Console.WriteLine (updateStartTime);

            TimeSpan timeEst = new TimeSpan(0,1,1);
            Console.WriteLine ("Updating Data");

            // add extra miniute to start to catch lost auctions
            lastUpdate = lastUpdate - new TimeSpan(0,1,0);

            var tasks = new List<Task>();
            int sum = 0;
            int doneCont=0;
            object sumloc = new object();

            for (int i = 0; i < max; i++) {
                var res = hypixel?.GetAuctionPage (i);
                if(res == null)
                    continue;
                if(i == 0)
                {
                    // correct update time
                    Console.WriteLine($"Updating difference {lastUpdate} {res.LastUpdated}");
                    //lastUpdate = res.LastUpdated;
                }
                max = res.TotalPages;
                
                tasks.Add(Task.Run(()=>{
                     var val = Save(res,lastUpdate);
                     lock(sumloc)
                     {
                         sum += val;
                         // process done
                         doneCont++;
                     }
                    PrintUpdateEstimate(i,doneCont,sum,updateStartTime,max);
                }));
                PrintUpdateEstimate(i,doneCont,sum,updateStartTime,max);

                
                // try to stay under 100MB
                if(System.GC.GetTotalMemory(false) > 100000000)
                {
                    Console.Write("\t\t mem: " + System.GC.GetTotalMemory(false));
                    // to much memory wait on a thread
                    //tasks[i/2].Wait();
                    //tasks[i/2].Dispose();
                    System.GC.Collect();
                }
            }

            foreach (var item in tasks)
            {
                //Console.Write($"\r {index++}/{updateEstimation} \t({index}) {timeEst:mm\\:ss}");
                item?.Wait();
                PrintUpdateEstimate(max,doneCont,sum,updateStartTime,max);
            }


        }

        static void PrintUpdateEstimate(long i,long doneCont,long sum,DateTime updateStartTime, long max)
        {
            var index = sum;
            // max is doubled since it is counted twice (download and done)
            var updateEstimation = index*max*2/(i+1+doneCont)+1;
            var ticksPassed = (DateTime.Now.ToLocalTime().Ticks-updateStartTime.Ticks);
            var timeEst = new TimeSpan(ticksPassed/(index+1)*updateEstimation-ticksPassed) ;
            Console.Write($"\r Loading: ({i}/{max}) Done With: {doneCont} Total:{sum} {timeEst:mm\\:ss}");
        }

        // builds the index for all auctions in the last hour
        

        static int Save(GetAuctionPage res,DateTime lastUpdate)
        {
            int count = 0;
            foreach (var item in res.Auctions) {

                ItemDetails.Instance.AddOrIgnoreDetails(item);


     
                // nothing changed if the last bid is older than the last update
                if (item.Bids.Count > 0 && item.Bids[item.Bids.Count - 1].Timestamp < lastUpdate ||
                    item.Bids.Count == 0 && item.Start < lastUpdate) {
                    continue;
                }

                try{
                    //var a = StorageManager.GetOrCreateAuction(item.Uuid,new SaveAuction(item));
                    var auction = new SaveAuction(item);
                    FileController.ReplaceLine<SaveAuction> ("auctionpull/"+auction.Uuid.Substring(0,4),(a)=>a.Uuid == auction.Uuid, auction);
                    //CreateIndex(a);
                } catch(Exception e)
                {
                    Logger.Instance.Error($"Error {e.Message} on {item.ItemName} {item.Uuid} from {item.Auctioneer}");
                    Logger.Instance.Error(e.StackTrace);
                }

                count++;
            }

            return count;
        }

    }
}
