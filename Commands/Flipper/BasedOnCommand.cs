using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace hypixel
{
    public class BasedOnCommand : Command
    {
        public override Task Execute(MessageData data)
        {
            var uuid = data.GetAs<string>();
            System.Console.WriteLine(uuid);
            using (var context = new HypixelContext())
            {
                var auction = AuctionService.Instance.GetAuction(uuid,
                    auctions => auctions
                    .Include(a => a.NbtData)
                    .Include(a => a.Enchantments));
                if (auction == null)
                    throw new CoflnetException("auction_unkown", "not found");
                if (Flipper.FlipperEngine.Instance.relevantAuctionIds.TryGetValue(auction.UId, out List<long> ids))
                {
                    return data.SendBack(data.Create("basedOnResp", context.Auctions.Where(a => ids.Contains(a.UId)).Select(a => new Response()
                    {
                        uuid = a.Uuid,
                        highestBid = a.HighestBidAmount,
                        end = a.End
                    }).ToList(), 120));
                }
                System.Console.WriteLine($"uuid not found on id list " + Flipper.FlipperEngine.Instance.relevantAuctionIds.Count);

                var result = Flipper.FlipperEngine.Instance.GetRelevantAuctionsCache(auction, context);
                result.Wait();
                return data.SendBack(data.Create("basedOnResp", result.Result.Item1
                            .Select(a => new Response()
                            {
                                uuid = a.Uuid,
                                highestBid = a.HighestBidAmount,
                                end = a.End
                            }),
                            A_HOUR));
            }
        }
        [DataContract]
        public class Response
        {
            [DataMember(Name = "uuid")]
            public string uuid;
            [DataMember(Name = "highestBid")]
            public long highestBid;
            [DataMember(Name = "end")]
            public System.DateTime end;
        }
    }
}