using System;
using System.Threading.Tasks;
using static hypixel.ItemReferences;

namespace hypixel
{
    public class PricerDicerCommand : Command
    {
        public override Task Execute(MessageData data)
        {
            ItemSearchQuery details = ItemPricesCommand.GetQuery(data);
            // temporary map none (0) to any
            if (details.Reforge == Reforge.None)
                details.Reforge = Reforge.Any;


            if (Program.LightClient && details.Start < DateTime.Now - TimeSpan.FromDays(7))
            {
                return ClientProxy.Instance.Proxy(data);
            }

            var thread = ItemPrices.Instance.GetPriceFor(details);
            var res = thread.Result;

            var maxAge = A_MINUTE;
            if (IsDayRange(details))
            {
                maxAge = A_DAY;
            }
            Console.WriteLine("made response");

            return data.SendBack(data.Create("itemResponse", res, maxAge));
        }

        private static bool IsDayRange(ItemSearchQuery details)
        {
            return details.Start < DateTime.Now - TimeSpan.FromDays(2);
        }
    }
}