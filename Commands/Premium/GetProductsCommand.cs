using System.Threading.Tasks;
using Newtonsoft.Json;
using Stripe;

namespace hypixel
{
    public class GetProductsCommand : Command
    {
        public override Task Execute(MessageData data)
        {
            var options = new ProductListOptions
            {
                Limit = 10
            };
            var service = new ProductService();
            StripeList<Product> products = service.List(
              options
            );

            return data.SendBack(new MessageData("productsResponse", JsonConvert.SerializeObject(products), A_HOUR));
        }
    }


}