using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Coflnet;
using hypixel;
using Hypixel.NET;
using Hypixel.NET.SkyblockApi.Bazaar;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using RestSharp;

namespace dev {

    public class BazaarPull {
        public BazaarPull() {}
        public BazaarPull(GetBazaarProducts result)
        {
            this.Timestamp = result.LastUpdated;
            this.Products = result.Products.Select(p=>new ProductInfo(p.Value,this)).ToList();
        }

        public int Id { get; set; }
        public List<ProductInfo> Products { get; set; }

        public DateTime Timestamp { get; set; }
    }
    public class BazaarUpdater {
        private bool abort;

        public static DateTime LastUpdate { get; internal set; }

        public static Dictionary<string,QuickStatus> LastStats = new Dictionary<string, QuickStatus>();

        public static void NewUpdate (string apiKey) {
            Console.WriteLine ($"Started at {DateTime.Now}");

            var api = new HypixelApi (apiKey, 2);

            for (int i = 0; i < 1; i++)
            {
                var start = DateTime.Now;
                PullAndSave(api, i);
                WaitForServerCacheRefresh(i, start);
            }

            Console.WriteLine ($"done {DateTime.Now}");

        }

        private static void PullAndSave(HypixelApi api, int i)
        {
            var result = api.GetBazaarProducts();
                var pull = new BazaarPull(result);
            using (var context = new HypixelContext())
            {
                context.Database.EnsureCreated();

                var lastMinPulls = context.BazaarPull
                            .Include(b => b.Products)
                            .ThenInclude(p => p.QuickStatus)
                            .OrderByDescending(b=>b.Timestamp)
                            .Take(8).ToList();
                            //.Where(b => b.Timestamp == context.BazaarPull.Max(ba => ba.Timestamp))
                            //.FirstOrDefault();

                //Console.WriteLine($"Got {lastMinPulls.Count} pulls total of {lastMinPulls.Sum(p=>p.Products.Count)} products ");

                //var lastKnownPrices = lastMinPulls.ToDictionary(p=>p)


                if (lastMinPulls.Any())
                {
                    var lastPull = lastMinPulls.First();
                    var lastPullDic = lastPull
                            .Products.ToDictionary(p => p.ProductId);

                    var sellChange = 0;
                    var buyChange = 0;
                    var productCount = pull.Products.Count;

                    var toRemove = new List<ProductInfo>();

                    for (int index = 0; index < productCount; index++)
                    {
                        var currentProduct = pull.Products[index];
                        var currentStatus = currentProduct.QuickStatus;
                        var lastProduct = lastMinPulls.SelectMany(p=>p.Products)
                                        .Where(p=>p.ProductId ==currentStatus.ProductId)
                                        .OrderByDescending(p=>p.Id)
                                        .FirstOrDefault();

                        var lastStatus =  new QuickStatus();            
                        if(lastProduct != null)
                        {
                            lastStatus =lastProduct.QuickStatus;
                        } 
                        // = lastPullDic[currentStatus.ProductId].QuickStatus;

                        var takeFactor = i % 60 == 0 ? 30 : 3;

                        if (currentStatus.BuyOrders == lastStatus.BuyOrders)
                        {
                            // nothing changed
                            currentProduct.BuySummery = null;
                            buyChange++;
                        }
                        else
                        {
                            currentProduct.BuySummery = currentProduct.BuySummery.Take(takeFactor).ToList();
                        }
                        if (currentStatus.SellOrders == lastStatus.SellOrders)
                        {
                            // nothing changed
                            currentProduct.SellSummary = null;
                            sellChange++;
                        }
                        else
                        {
                            currentProduct.SellSummary = currentProduct.SellSummary.Take(takeFactor).ToList();
                        }
                        if(currentProduct.BuySummery == null && currentProduct.SellSummary == null)
                        {
                            toRemove.Add(currentProduct);
                        }
                    }
                    //Console.WriteLine($"Not saving {toRemove.Count}");

                    foreach (var item in toRemove)
                    {
                        pull.Products.Remove(item);
                    }

                    Console.WriteLine($"BuyChange: {productCount - buyChange}  SellChange: {productCount - sellChange}");
                    context.Update(lastPull);
                }

                context.BazaarPull.Add(pull);
                context.SaveChanges();
                Console.Write("\r" + i);
            }

            LastStats = pull.Products.Select(p=>p.QuickStatus).ToDictionary(qs=>qs.ProductId);
            LastUpdate = DateTime.Now;
        }

        private static void WaitForServerCacheRefresh (int i, DateTime start) {
            var timeToSleep = start.Add (new TimeSpan (0,0, 0,10)) - DateTime.Now;
            Console.Write ($"\r {i} {timeToSleep}");
            if (timeToSleep.Seconds > 0)
                Thread.Sleep (timeToSleep);
        }

        public void UpdateForEver(string apiKey)
        {
            System.Threading.Tasks.Task.Run(()=>{
                int i = 0;
                while(!abort)
                {
                    try {
                        var api = new HypixelApi(apiKey,9);
                        var start = DateTime.Now;
                        PullAndSave(api, i);
                        WaitForServerCacheRefresh(i, start);
                        i++;
                    } catch(Exception e)
                    {
                        Logger.Instance.Error($"\nBazaar update failed {e.Message} \n{e.StackTrace} \n{e.InnerException?.Message}");
                        Console.WriteLine($"\nBazaar update failed {e.Message} \n{e.InnerException?.Message}");
                        Thread.Sleep(5000);
                    }
                }
                Console.WriteLine("Stopped Bazaar :/");
            });
        }

        static string Path (ProductInfo product) {
            return $"product/{product.ProductId}/{product.Timestamp.ToFileTimeUtc()}";
        }

        internal void Stop()
        {
            abort = true;
        }
    }

    public class BazzarResponse {
        [JsonProperty ("success")]
        public bool WasSuccessful { get; private set; }

        [JsonProperty ("product_info")]
        public ProductInfo ProductInfo { get; private set; }
    }

    public class BazaarController {
        public static BazaarController Instance = new BazaarController ();

        public IEnumerable<ProductInfo> GetInfo (string id) {
            return StorageManager.GetFileContents<ProductInfo> ($"product/{id.Trim('"')}");
        }
    }
}