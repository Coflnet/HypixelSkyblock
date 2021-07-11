using System;
using System.IO;
using WebSocketSharp.Server;
using Stripe;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace hypixel
{
    public class StripeRequests
    {
        public async Task ProcessStripe(HttpRequestEventArgs e)
        {
            Console.WriteLine("received callback from stripe --");
            string json = "";
            try
            {
                Console.WriteLine("reading json");
                json = new StreamReader(e.Request.InputStream).ReadToEnd();
                //Console.WriteLine(e.)

                var stripeEvent = EventUtility.ConstructEvent(
                  json,
                  e.Request.Headers["Stripe-Signature"],
                  Program.StripeSigningSecret
                );
                Console.WriteLine("stripe valiadted");
                Console.WriteLine(json);

                if (stripeEvent.Type == Events.CheckoutSessionCompleted)
                {
                    Console.WriteLine("stripe checkout completed");
                    var session = stripeEvent.Data.Object as Stripe.Checkout.Session;

                    // Fulfill the purchase...
                    await this.FulfillOrder(session);
                }
                else
                {
                    Console.WriteLine("sripe is not comlete type of " + stripeEvent.Type);
                }

                e.Response.StatusCode = 200;
            }
            catch (StripeException ex)
            {
                Console.WriteLine($"Ran into exception for stripe callback {ex.Message} \n{ex.StackTrace} {json}");
                e.Response.StatusCode = 400;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ran into an unknown error :/ {ex.Message} {ex.StackTrace}");
            }
        }

        private async Task FulfillOrder(Stripe.Checkout.Session session)
        {
            Console.WriteLine("Furfilling order");
            var googleId = Int32.Parse(session.ClientReferenceId);
            var id = session.CustomerId;
            //var email = session.CustomerEmail;
            var days = Int32.Parse(session.Metadata["days"]);
            Console.WriteLine("STRIPE");
            using (var context = new HypixelContext())
            {
                var user = await context.Users.Where(u => u.Id == googleId).FirstAsync();
                
                UserService.Instance.SavePurchase(user, days, session.Id);
                await context.SaveChangesAsync();
                Console.WriteLine("order completed");
            }
        }
    }
}
