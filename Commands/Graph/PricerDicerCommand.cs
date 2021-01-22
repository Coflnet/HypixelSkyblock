using static hypixel.ItemReferences;

namespace hypixel
{
    public class PricerDicerCommand : Command
    {
        public override void Execute(MessageData data)
        {
            ItemSearchQuery details = ItemPricesCommand.GetQuery(data);
            // temporary map none (0) to any
            if(details.Reforge == Reforge.None)
                details.Reforge = Reforge.Any;

            var res = ItemPrices.Instance.GetPriceFor(details);

            data.SendBack(MessageData.Create("itemResponse", res, A_MINUTE));
        }
    }
}