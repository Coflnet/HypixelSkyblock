using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace hypixel
{
    public class AuctionSyncCommand : Command
    {
        public override Task Execute(MessageData data)
        {
            var auctions = data.GetAs<List<AuctionSync>>();
            using (var context = new HypixelContext())
            {


                List<string> incomplete = new List<string>();

                foreach (var auction in auctions)
                {
                    var a = AuctionService.Instance.GetAuctionWithSelect(auction.Id,col=>col.Select(a => new { a.Id, a.HighestBidAmount }).FirstOrDefault());
                    if (a.HighestBidAmount == auction.HighestBid)
                        continue;
                    incomplete.Add(auction.Id);
                }
            }
            return Task.CompletedTask;
        }
    }
}
