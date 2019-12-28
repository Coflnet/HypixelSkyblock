using System;
using System.Collections.Generic;
using System.Linq;
using MessagePack;

namespace hypixel
{
    public class PlayerBidsCommand : PaginatedRequestCommand<PlayerBidsCommand.BidResult>
    {
        public override string ResponseCommandName => "playerBidsResponse";

        public override IEnumerable<string> GetAllIds(string id)
        {
            return StorageManager.GetOrCreateUser(id).Bids.Select(b=>b.auctionId);
        }

        public override BidResult GetElement(string id,string parentUuId)
        {
            return new BidResult(StorageManager.GetOrCreateAuction(id),parentUuId);
        }   


        [MessagePackObject]
        public class BidResult
        {
            [Key("highestOwn")]
            public long HighestOwnBid;
            [Key("highestBid")]
            public long HighestBid;
            [Key("itemName")]
            public string ItemName;
            [Key("uuid")]
            public string AuctionId;
            [Key("end")]
            public DateTime End;

            public BidResult(SaveAuction a, string userUuid)
            {
                var highestOwn = a.Bids?.Where(bid=>bid.Bidder == userUuid)
                            .OrderByDescending(bid=>bid.Amount).FirstOrDefault();

                AuctionId = a.Uuid;
                if(a.Bids != null)
                    HighestBid = a.Bids.Last().Amount;
                if(highestOwn != null)
                    HighestOwnBid = highestOwn.Amount;
                ItemName = a.ItemName;
                End=a.End;
            }

            public BidResult(){ }
        }
    }
}
