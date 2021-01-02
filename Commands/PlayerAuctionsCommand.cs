using System;
using System.Collections.Generic;
using System.Linq;
using MessagePack;

namespace hypixel
{
    public class PlayerAuctionsCommand : PaginatedRequestCommand<PlayerAuctionsCommand.AuctionResult>
    {
        public override string ResponseCommandName => "playerAuctionsResponse";

        public override IEnumerable<AuctionResult> GetAllElements(string selector,int amount,int offset)
        {
            Console.WriteLine(selector);
            Console.WriteLine(offset);
            Console.WriteLine(amount);
            using(var context = new HypixelContext())
            {
                var auctions = context.Auctions
                        .Where(a=>a.SellerId == context.Players.Where(p=>p.UuId == selector).Select(p=>p.Id).FirstOrDefault())
                        .OrderByDescending(a=>a.End)
                        
                        .Skip(offset)
                        .Take(amount)
                        .ToList();

                Console.WriteLine(auctions.Count);

                return auctions.Select(a=>new AuctionResult(a));
            }
        }

        public override IEnumerable<string> GetAllIds(string id)
        {
            return StorageManager.GetOrCreateUser(id).auctionIds;
        }

        public override AuctionResult GetElement(string id,string parentUuid)
        {
            return new AuctionResult(StorageManager.GetOrCreateAuction(id));
        }    

        [MessagePackObject]
        public class AuctionResult
        {
            [Key("uuid")]
            public string AuctionId;
            [Key("highestBid")]
            public long HighestBid;
            [Key("itemName")]
            public string ItemName;
            [Key("tag")]
            public string Tag;
            [Key("end")]
            public DateTime End;
            [Key("startingBid")]
            public long StartingBid;

            public AuctionResult(SaveAuction a)
            {
                AuctionId = a.Uuid;
                HighestBid = a.HighestBidAmount;
                ItemName = a.ItemName;
                End = a.End;
                Tag = a.Tag;
                StartingBid = a.StartingBid;
            }

            public AuctionResult()
            {
            }
        }         
    }
}
