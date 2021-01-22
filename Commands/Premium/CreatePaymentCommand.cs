using System.Collections.Generic;
using System.Linq;
using Stripe.Checkout;

namespace hypixel
{
    public class CreatePaymentCommand : Command
    {
        public override void Execute(MessageData data)
        {
            var productId = data.GetAs<string>();
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
                    //  UnitAmount = 149,

                    //  Currency = "eur",
                      Product=productId
                    },

                    Description = "Unlocks premium features: Subscribe to 100 Thrings, Search with multiple filters and you support the project :)",
                    Quantity = 1,
                  },
                },
                Mode = "payment",
                SuccessUrl = domain + "/success",
                CancelUrl = domain + "/cancel",
                ClientReferenceId = data.Connection.UserId.ToString()
            };
            var service = new SessionService();
            Session session = service.Create(options);
            using (var context = new HypixelContext())
            {
                var user = data.User;
                context.Update(user);
                context.SaveChanges();
            }

            data.SendBack(MessageData.Create("checkoutSession", session.Id), false);
            //return Json(new { id = session.Id });
        }
    }
}
