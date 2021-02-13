using Newtonsoft.Json;
using Stripe;

namespace hypixel
{
    public class GetProductsCommand : Command
    {
        public override void Execute(MessageData data)
        {
            var options = new ProductListOptions
            {
                Limit = 10
            };
            var service = new ProductService();
            StripeList<Product> products = service.List(
              options
            );

            data.SendBack(new MessageData("productsResponse", JsonConvert.SerializeObject(products), A_HOUR));
        }
    }


}