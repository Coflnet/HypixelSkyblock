using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Stripe;
using Stripe.Checkout;

namespace hypixel
{
    public class CreatePaymentCommand : Command
    {
        public override Task Execute(MessageData data)
        {

            string productId;
            try
            {
                productId = data.GetAs<string>();
            }
            catch (Exception)
            {
                throw new CoflnetException("invaild_data", "Data should contain a product id as string");
            }
            var product = GetProduct(productId);
            var price = GetPrice(productId);

            var domain = "https://sky.coflnet.com";
            var options = new SessionCreateOptions
            {
                PaymentMethodTypes = new List<string>
                {
                  "card",
                },
                LineItems = new List<SessionLineItemOptions>
                {
                  new SessionLineItemOptions
                  {

                    PriceData = new SessionLineItemPriceDataOptions
                    {
                      UnitAmount = price.UnitAmount,

                      Currency = "eur",
                      Product=productId
                    },

                   // Description = "Unlocks premium features: Subscribe to 100 Thrings, Search with multiple filters and you support the project :)",
                    Quantity = 1,
                  },
                },
                Metadata = product.Metadata,
                Mode = "payment",
                SuccessUrl = domain + "/success",
                CancelUrl = domain + "/cancel",
                ClientReferenceId = data.UserId.ToString()
            };
            var service = new SessionService();
            Session session;
            try
            {
                session = service.Create(options);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                throw new CoflnetException("internal_error", "service not working");
            }
            using (var context = new HypixelContext())
            {
                var user = data.User;
                context.Update(user);
                context.SaveChanges();
            }

            return data.SendBack(data.Create("checkoutSession", session.Id), false);
            //return Json(new { id = session.Id });
        }

        Dictionary<string,Price> priceCache = null;

        public Price GetPrice(string productId)
        {
            var service = new PriceService();
            if(priceCache == null)
            {
                priceCache = service.List().ToDictionary(e=>e.ProductId);
                Console.WriteLine(Newtonsoft.Json.JsonConvert.SerializeObject(priceCache));
            }
            if(priceCache.TryGetValue(productId,out Price value))
                return value;

            throw new CoflnetException("unkown_product",$"The price for id {productId} was not found");
        }
        Dictionary<string,Product> productCache = null;

        public Product  GetProduct(string productId)
        {
            var service = new ProductService();
            if(priceCache == null)
            {
                productCache = service.List().ToDictionary(e=>e.Id);
                Console.WriteLine(Newtonsoft.Json.JsonConvert.SerializeObject(priceCache));
            }
            if(productCache.TryGetValue(productId,out Product value))
                return value;

            throw new CoflnetException("unkown_product",$"The product with id {productId} was not found");
        }

    }


}
