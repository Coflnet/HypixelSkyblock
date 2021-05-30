using System;
using static hypixel.ItemReferences;

namespace hypixel
{
    public class PricerDicerCommand : Command
    {
        public override void Execute(MessageData data)
        {
            ItemSearchQuery details = ItemPricesCommand.GetQuery(data);
            // temporary map none (0) to any
            if (details.Reforge == Reforge.None)
                details.Reforge = Reforge.Any;

            var thread = ItemPrices.Instance.GetPriceFor(details);
            thread.Wait();
            var res = thread.Result;

            var maxAge = A_MINUTE;
            if (IsDayRange(details))
            {
                maxAge = A_DAY;
            }

            data.SendBack(data.Create("itemResponse", res, maxAge));
        }

        private static bool IsDayRange(ItemSearchQuery details)
        {
            return details.Start < DateTime.Now - TimeSpan.FromDays(2);
        }
    }
}