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

        public static void NewUpdate (string apiKey) {
            Console.WriteLine ($"Started at {DateTime.Now}");

            var api = new HypixelApi (apiKey, 2);

            for (int i = 0; i < 3000; i++) {
                var start = DateTime.Now;
                var result = api.GetBazaarProducts ();
                using (var context = new HypixelContext ()) {
                    context.Database.EnsureCreated ();

                    var lastPull = context.BazaarPull
                                .Include(b=>b.Products)
                                .ThenInclude(p=>p.QuickStatus)
                                .Where(b=>b.Timestamp == context.BazaarPull.Max(ba=>ba.Timestamp))
                                .FirstOrDefault();


                    var pull = new BazaarPull(result);

                    if(lastPull!=null)
                    {
                        var lastPullDic = lastPull
                                .Products.ToDictionary(p=>p.ProductId);

                        var sellChange = 0;
                        var buyChange = 0;
                        var productCount = pull.Products.Count;


                        for (int index = 0; index < productCount; index++)
                        {
                            var currentProduct = pull.Products[index];
                            var currentStatus = currentProduct.QuickStatus;
                            var lastStatus = lastPullDic[currentStatus.ProductId].QuickStatus;

                            var takeFactor = i % 30 == 0 ? 30 : 3;

                            if(currentStatus.BuyOrders == lastStatus.BuyOrders)
                            {
                                // nothing changed
                                currentProduct.BuySummery = null;
                                buyChange++;
                            } else {
                                currentProduct.BuySummery = currentProduct.BuySummery.Take(takeFactor).ToList();
                            }
                            if(currentStatus.SellOrders == lastStatus.SellOrders)
                            {
                                // nothing changed
                                currentProduct.SellSummary = null;
                                sellChange++;
                            } else {
                                currentProduct.SellSummary = currentProduct.SellSummary.Take(takeFactor).ToList();
                            }
                        }

                        Console.WriteLine($"BuyChange: {productCount-buyChange}  SellChange: {productCount-sellChange}");
                        context.Update(lastPull);
                    }

                    context.BazaarPull.Add(pull);
                    context.SaveChanges ();
                    Console.Write ("\r" + i);
                    WaitForServerCacheRefresh (i, start);
                }
            }

            Console.WriteLine ($"done {DateTime.Now}");

        }

        private static void WaitForServerCacheRefresh (int i, DateTime start) {
            var timeToSleep = start.Add (new TimeSpan (0,0, 0, 9,980)) - DateTime.Now;
            Console.Write ($"\r {i} {timeToSleep}");
            if (timeToSleep.Seconds > 0)
                Thread.Sleep (timeToSleep);
        }

        public static void Update (string apiKey) {
            var client = new RestClient ("https://api.hypixel.net/skyblock/bazaar");

            var productsReq = new RestRequest ("products").AddParameter ("key", apiKey);

            var listResult = client.Get (productsReq).Content;
            dynamic deserialized = JsonConvert.DeserializeObject (listResult);

            var ids = deserialized.productIds;

            int index = 0;
            Console.WriteLine (apiKey);
            Console.WriteLine (listResult);

            foreach (var item in ids) {

                var detailReq = new RestRequest ("product", Method.GET)
                    .AddParameter ("key", apiKey)
                    .AddParameter ("productId", item);

                var result = client.Execute (detailReq);
                var infoRespnse = JsonConvert.DeserializeObject<BazzarResponse> (result.Content);

                if (infoRespnse.WasSuccessful) {

                    var info = (ProductInfo) infoRespnse.ProductInfo;
                    info.Timestamp = DateTime.Now;
                    //FileController.SaveAs(Path(info),info);
                    using (var context = new HypixelContext ()) {
                        context.BazaarPrices.Add (info);
                        context.SaveChanges ();
                    }
                } else {
                    Console.WriteLine (result.Content);
                    throw new Exception ("request failed");
                }
                Console.Write ($"\r{index++}/{ids.Count}");
                // stay under limit
                Thread.Sleep (300);
            }

        }

        static string Path (ProductInfo product) {
            return $"product/{product.ProductId}/{product.Timestamp.ToFileTimeUtc()}";
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