using static hypixel.ItemReferences;

namespace hypixel
{
    public class GetRecentAuctionsCommand : Command
    {
        public override void Execute(MessageData data)
        {
            ItemSearchQuery details = ItemPricesCommand.GetQuery(data);
            if (Program.LightClient && details.Start < DateTime.Now - TimeSpan.FromDays(7))
            {
                ClientProxy.Instance.Proxy(data);
                return;
            }
            // temporary map none (0) to any
            if (details.Reforge == Reforge.None)
                details.Reforge = Reforge.Any;

            var res = ItemPrices.Instance.GetRecentAuctions(details);

            data.SendBack(data.Create("auctionResponse", res, A_MINUTE * 2));
        }
    }
}