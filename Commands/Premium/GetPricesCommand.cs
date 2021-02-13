using Newtonsoft.Json;
using Stripe;

namespace hypixel
{
    public class GetPricesCommand : Command
    {
        public override void Execute(MessageData data)
        {
            var options = new PriceListOptions { Limit = 10 };
            var service = new PriceService();
            StripeList<Price> prices = service.List(options);

            data.SendBack(new MessageData("pricesResponse", JsonConvert.SerializeObject(prices), A_HOUR));
        }
    }


}