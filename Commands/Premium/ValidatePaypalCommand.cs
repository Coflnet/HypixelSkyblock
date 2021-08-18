using System;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Coflnet;
using PayPalCheckoutSdk.Core;
using PayPalCheckoutSdk.Orders;

namespace hypixel
{
    public class ValidatePaypalCommand : Command
    {
        static string clientId = SimplerConfig.Config.Instance["PAYPAL_ID"];
        static string clientSecret = SimplerConfig.Config.Instance["PAYPAL_SECRET"];

        private ConcurrentCollections.ConcurrentHashSet<string> UsedIds = new ConcurrentCollections.ConcurrentHashSet<string>();

        public override Task Execute(MessageData data)
        {        
            Console.WriteLine($"PayPal attempt {data.Data}");
            var args = data.GetAs<Params>();
    
            Console.WriteLine($" from {data.UserId}");
            OrdersGetRequest request = new OrdersGetRequest(args.OrderId);
            if (string.IsNullOrEmpty(clientId))
                throw new CoflnetException("unavailable", "checkout via paypal has not yet been enabled, please contact an admin");
            var client = new PayPalHttpClient(new LiveEnvironment(clientId, clientSecret));
            data.Log($"User: {data.UserId}");
            //3. Call PayPal to get the transaction
            PayPalHttp.HttpResponse response;
            try
            {
                response = client.Execute(request).Result;
            }
            catch (Exception e)
            {
                data.LogError(e, "payPalPayment");
                throw new CoflnetException("payment_failed", "The provided orderId has not vaid payment asociated");
            }
            //4. Save the transaction in your database. Implement logic to save transaction to your database for future reference.
            var result = response.Result<Order>();
            Console.WriteLine(JSON.Stringify(result));
            Console.WriteLine("Retrieved Order Status");
            AmountWithBreakdown amount = result.PurchaseUnits[0].AmountWithBreakdown;
            Console.WriteLine("Total Amount: {0} {1}", amount.CurrencyCode, amount.Value);
            Console.WriteLine("user with id " + data.UserId);
            if (result.Status != "COMPLETED")
                throw new CoflnetException("order_incomplete", "The order is not yet completed");

            Console.WriteLine("Status: {0}", result.Status);
            if (UsedIds.Contains(args.OrderId))
                throw new CoflnetException("payment_timeout", "the provied order id was already used");

            Console.WriteLine("Order Id: {0}", result.Id);
            if (DateTime.Parse(result.PurchaseUnits[0].Payments.Captures[0].UpdateTime) < DateTime.Now.Subtract(TimeSpan.FromHours(1)))
                throw new CoflnetException("payment_timeout", "the provied order id is too old, please contact support for manual review");
            var user = data.User;
            var days = args.Days;
            var transactionId = result.Id;
            Console.WriteLine($"user {user.Id} purchased via PayPal {transactionId}");
            UserService.Instance.SavePurchase(user, days, transactionId);

            UsedIds.Add(args.OrderId);
            FileController.AppendLineAs("purchases", JSON.Stringify(result));
            return data.Ok();
        }

        [DataContract]
        public class Params
        {
            [DataMember(Name = "orderId")]
            public string OrderId;
            [DataMember(Name = "days")]
            public int Days;
        }
    }
}
