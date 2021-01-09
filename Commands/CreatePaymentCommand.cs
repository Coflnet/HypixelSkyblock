using System.Collections.Generic;
using System.Linq;
using Stripe.Checkout;

namespace hypixel
{
    public class CreatePaymentCommand : Command
    {
        public override void Execute(MessageData data)
        {
            var googleId = data.GetAs<string>();
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
                      UnitAmount = 99,
                      Currency = "eur",
                      ProductData = new SessionLineItemPriceDataProductDataOptions
                      {
                        Name = "Premium Features",
                      },
                    },
                    Quantity = 1,
                  },
                },
                Mode = "payment",
                SuccessUrl = domain + "/success",
                CancelUrl = domain + "/cancel",
                ClientReferenceId = googleId
            };
            var service = new SessionService();
            Session session = service.Create(options);
            using (var context = new HypixelContext())
            {
                var user = UserService.Instance.GetUser(googleId);
                user.SessionId = session.Id;
                context.Update(user);
                context.SaveChanges();
            }

            data.SendBack(MessageData.Create("checkoutSession", session.Id), false);
            //return Json(new { id = session.Id });
        }
    }
}
