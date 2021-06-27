using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace hypixel
{
    public class BasedOnCommand : Command
    {
        public override void Execute(MessageData data)
        {
            var uuid = data.GetAs<string>();
            System.Console.WriteLine(uuid);
            using (var context = new HypixelContext())
            {
                var auction = context.Auctions
                    .Where(a => a.Uuid == uuid)
                    .Include(a => a.NbtData)
                    .Include(a => a.Enchantments)
                    .FirstOrDefault();
                if (auction == null)
                    throw new CoflnetException("auction_unkown", "not found");
                if(Flipper.FlipperEngine.Instance.relevantAuctionIds.TryGetValue(auction.UId,out List<long> ids))
                {
                    data.SendBack(data.Create("basedOnResp", context.Auctions.Where(a => ids.Contains(a.UId)).Select(a => new
                    {
                        uuid = a.Uuid,
                        highestBid = a.HighestBidAmount,
                        end = a.End
                    }),120));
                    System.Console.WriteLine("sending based on id list " + uuid);
                    return;
                }
                    System.Console.WriteLine($"uuid not found on id list " + Flipper.FlipperEngine.Instance.relevantAuctionIds.Count);

                var result = Flipper.FlipperEngine.Instance.GetRelevantAuctions(auction, context);
                result.Wait();
                data.SendBack(data.Create("basedOnResp", result.Result.Item1
                            .Select(a => new { 
                                uuid = a.Uuid, 
                                highestBid = a.HighestBidAmount, 
                                end = a.End }), 
                            A_HOUR));
            }
        }
    }
}