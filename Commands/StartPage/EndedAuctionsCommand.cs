using System.Linq;

namespace hypixel
{
    public class EndedAuctionsCommand : Command
    {
        public override void Execute(MessageData data)
        {
            using (var context = new HypixelContext())
            {
                var end = System.DateTime.Now;
                var pages = context.Auctions.Where(a => a.End < end)
                    .OrderByDescending(a => a.End)
                    .Take(20)
                    .Select(p=>new PlayerAuctionsCommand.AuctionResult(p))
                    .ToList()
                    .Select(a=>{
                        if(a.ItemName == null)
                        {
                            a.ItemName = ItemDetails.TagToName(a.Tag);
                        }
                        return a;
                    }).ToList();
                data.SendBack(data.Create("endedAuctions", pages, A_MINUTE));
            }
        }
    }
}
