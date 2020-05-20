using System;
using System.Collections.Generic;
using System.Threading;
using Coflnet;
using hypixel;
using Newtonsoft.Json;
using RestSharp;

namespace dev
{
    public class BazaarUpdater
    {
        public static void  Update(string apiKey) 
        {
            var client = new RestClient("https://api.hypixel.net/skyblock/bazaar");

            var productsReq = new RestRequest("products").AddParameter("key",apiKey);

            var listResult = client.Get(productsReq).Content;
            dynamic deserialized = JsonConvert.DeserializeObject(listResult);

            var ids = deserialized.productIds;

            int index = 0;

            foreach (var item in ids)
            {

                var detailReq = new RestRequest("product",Method.GET)
                    .AddParameter("key",apiKey)
                    .AddParameter("productId",item);

                var result = client.Execute(detailReq);
                var infoRespnse = JsonConvert.DeserializeObject<BazzarResponse>(result.Content);
                

                if(infoRespnse.WasSuccessful)
                {

                    var info = (ProductInfo) infoRespnse.ProductInfo;
                    info.Timestamp = DateTime.Now;
                    FileController.SaveAs(Path(info),info);
                } else {
                    Console.WriteLine(result.Content);
                    throw new Exception("request failed");
                }
                Console.Write($"\r{index++}/{ids.Count}");
                // stay under limit
                Thread.Sleep(700);
            }

            
        }

        static string Path(ProductInfo product)
        {
            return $"product/{product.ProductId}/{product.Timestamp.ToFileTimeUtc()}";
        }
    }

    public class BazzarResponse
    {
        [JsonProperty("success")]
        public bool WasSuccessful { get; private set; }
        [JsonProperty("product_info")]
        public ProductInfo ProductInfo { get; private set; }
    }


    public class BazaarController
    {
        public static BazaarController Instance = new BazaarController();


        public IEnumerable<ProductInfo> GetInfo(string id)
        {
            return StorageManager.GetFileContents<ProductInfo>($"product/{id.Trim('"')}");
        }
    }
}