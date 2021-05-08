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
        public override async void Execute(MessageData data)
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
            var price = await GetPrice(productId);
            Console.WriteLine(price);

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
                      UnitAmount = price,

                      Currency = "eur",
                      Product=productId
                    },

                   // Description = "Unlocks premium features: Subscribe to 100 Thrings, Search with multiple filters and you support the project :)",
                    Quantity = 1,
                  },
                },
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

            data.SendBack(data.Create("checkoutSession", session.Id), false);
            //return Json(new { id = session.Id });
        }

        Dictionary<string,long?> priceCache = null;

        public async Task<long?>  GetPrice(string productId)
        {
            var service = new PriceService();
            if(priceCache == null)
            {
                priceCache = (await service.ListAsync()).ToDictionary(e=>e.ProductId,e=>e.UnitAmount);
            }
            return priceCache.GetValueOrDefault(productId,1000);
        }
    }


}
