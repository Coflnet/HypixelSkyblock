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
                        .Where(a=>a.AuctioneerId == selector)
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
            [Key("end")]
            public DateTime End;

            public AuctionResult(SaveAuction a)
            {
                AuctionId = a.Uuid;
                HighestBid = a.HighestBidAmount;
                ItemName = a.ItemName;
                End = a.End;
            }

            public AuctionResult()
            {
            }
        }         
    }
}
