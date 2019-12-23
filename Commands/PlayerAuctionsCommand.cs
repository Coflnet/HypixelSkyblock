using System;
using System.Collections.Generic;
using MessagePack;

namespace hypixel
{
    public class PlayerAuctionsCommand : PaginatedRequestCommand<PlayerAuctionsCommand.AuctionResult>
    {
        public override string ResponseCommandName => "playerAuctionsResponse";

        public override IEnumerable<string> GetAllIds(string id)
        {
            return StorageManager.GetOrCreateUser(id).auctionIds;
        }

        public override AuctionResult GetElement(string id)
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
