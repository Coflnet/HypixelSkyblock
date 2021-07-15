using System.Security.Cryptography.X509Certificates;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Newtonsoft.Json;
using Stripe;
using Google.Apis.AndroidPublisher.v3;
using System;
using MessagePack;
using dev;
using System.Linq;
using System.Threading.Tasks;

namespace hypixel
{
    public class GooglePurchaseCommand : Command
    {
        public override async Task Execute(MessageData data)
        {
            var args = data.GetAs<Arguments>();
            string serviceAccountEmail = "verification-admin@skyblock-300817.iam.gserviceaccount.com";

            var certificate = new X509Certificate2(@"keyfile.p12", "notasecret", X509KeyStorageFlags.Exportable);

            ServiceAccountCredential credential = new ServiceAccountCredential(
               new ServiceAccountCredential.Initializer(serviceAccountEmail)
               {
                   Scopes = new[] { "https://www.googleapis.com/auth/androidpublisher" }
               }.FromCertificate(certificate));



            var service = new AndroidPublisherService(
            new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = "GooglePlay API Sample",
            });
            // try catch this function because if you input wrong params ( wrong token) google will return error.
            //var request = service.Inappproducts.List("de.flou.hypixel.skyblock");
            var request = service.Purchases.Products.Get(
                        args.PackageName,
                        args.ProductId,
                        args.Token);
            try
            {
                Console.WriteLine($"Purchasing Product with id: {args.ProductId}");
                var purchaseState = await request.ExecuteAsync();

                //get purchase'status


                Console.WriteLine(JsonConvert.SerializeObject(purchaseState));

                if (((long)purchaseState.PurchaseTimeMillis / 1000).ThisIsNowATimeStamp() < DateTime.Now - TimeSpan.FromDays(7))
                    throw new CoflnetException("purchase_expired", "This purchase is older than a week and thus expired");

                var days = int.Parse(args.ProductId.Split('_')[1]);
                UserService.Instance.SavePurchase(data.User, days, purchaseState.OrderId);

            }
            catch (Exception e)
            {
                Logger.Instance.Error("Purchase failure " + e.Message);
                throw new CoflnetException("purchase_failed", "Purchase failed, please contact the admin");
            }



            await data.SendBack(data.Create("accepted", "payment was accepted enjoy your premium", A_WEEK));
        }

        [MessagePackObject]
        public class Arguments
        {
            [Key("productId")]
            public string ProductId {get;set;}
            [Key("token")]
            public string Token {get;set;}
            [Key("packageName")]
            public string PackageName {get;set;}
        }
    }


}