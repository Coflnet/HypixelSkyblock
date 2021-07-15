using static hypixel.ItemReferences;
using System;
using System.Threading.Tasks;

namespace hypixel
{
    public class GetRecentAuctionsCommand : Command
    {
        public override Task Execute(MessageData data)
        {
            ItemSearchQuery details = ItemPricesCommand.GetQuery(data);
            if (Program.LightClient && details.Start < DateTime.Now - TimeSpan.FromDays(7))
            {
                return ClientProxy.Instance.Proxy(data);
            }
            // temporary map none (0) to any
            if (details.Reforge == Reforge.None)
                details.Reforge = Reforge.Any;

            var res = ItemPrices.Instance.GetRecentAuctions(details);

            return data.SendBack(data.Create("auctionResponse", res, A_MINUTE * 2));
        }
    }
}