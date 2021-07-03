using System.Linq;

namespace hypixel
{
    public class NewAuctionsCommand : Command
    {
        public override void Execute(MessageData data)
        {
            using (var context = new HypixelContext())
            {
                var pages = context.Auctions.OrderByDescending(a => a.Id)
                    .Take(30)
                    .Select(p=>new PlayerAuctionsCommand.AuctionResult(p))
                    .ToList()
                    .Select(AuctionService.Instance.GuessMissingProperties)
                    .ToList();
                data.SendBack(data.Create("newAuctions", pages, A_MINUTE));
            }
        }
    }
}
