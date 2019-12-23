using System;
using System.Collections.Generic;
using System.Linq;
using MessagePack;

namespace hypixel
{
    public class PlayerBidsCommand : PaginatedRequestCommand<SaveAuction>
    {
        public override string ResponseCommandName => "playerBidsResponse";

        public override IEnumerable<string> GetAllIds(string id)
        {
            return StorageManager.GetOrCreateUser(id).Bids.Select(b=>b.auctionId);
        }

        public override SaveAuction GetElement(string id)
        {
            return StorageManager.GetOrCreateAuction(id);
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

            public BidResult(SaveAuction a, User displayUser)
            {
                var highestOwn = a.Bids.Where(bid=>bid.Bidder == displayUser.uuid)
                            .OrderByDescending(bid=>bid.Amount).FirstOrDefault();

                AuctionId = a.Uuid;
                HighestBid = a.Bids.Last().Amount;
                HighestOwnBid = highestOwn.Amount;
                ItemName = a.ItemName;
                End=a.End;
            }

            public BidResult(){ }
        }
    }
}
