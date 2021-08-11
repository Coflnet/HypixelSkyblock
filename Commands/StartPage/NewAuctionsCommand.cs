using System.Linq;
using System.Threading.Tasks;

namespace hypixel
{
    public class NewAuctionsCommand : Command
    {
        public override Task Execute(MessageData data)
        {
            using (var context = new HypixelContext())
            {
                var pages = context.Auctions.OrderByDescending(a => a.Id)
                    .Take(50)
                    .Select(p=>new PlayerAuctionsCommand.AuctionResult(p))
                    .ToList()
                    .Select(AuctionService.Instance.GuessMissingProperties)
                    .ToList();
                return data.SendBack(data.Create("newAuctions", pages, A_MINUTE));
            }
        }
    }
}
