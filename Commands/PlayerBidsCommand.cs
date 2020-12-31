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

        public override IEnumerable<PlayerBidsCommand.BidResult> GetAllElements(string selector,int amount,int offset)
        {
            using(var context = new HypixelContext())
            {
                var playerBids = context.Bids.Where(b=>b.BidderId == context.Players.Where(p=>p.UuId == selector).Select(p=>p.Id).FirstOrDefault())
                    // filtering
                    .OrderByDescending(auction=>auction.Timestamp)
                        .Skip(offset)
                        .Take(amount)
                    //.Include (p => p.Auction)
                    .Select(b=>new {
                        b.Auction.Uuid,
                        b.Auction.ItemName,
                        b.Auction.HighestBidAmount,
                        b.Auction.End,
                        b.Amount,
                        
                    }).GroupBy(b=>b.Uuid)
                    .Select(bid=> new {
                        bid.Key,
                        Amount = bid.Max(b=>b.Amount),
                        HighestBid = bid.Max(b=>b.HighestBidAmount),
                        ItemName = bid.Max(b=>b.ItemName),
                        HighestOwnBid = bid.Max(b=>b.Amount),
                        End = bid.Max(b=>b.End)
                    })
                    
                    //.ThenInclude (b => b.Auction)
                    .ToList ();

                var aggregatedBids = playerBids
                                .Select(b=>new PlayerBidsCommand.BidResult(){
                                    HighestBid = b.HighestBid,
                                    AuctionId=b.Key,
                                    End = b.End,
                                    HighestOwnBid = b.HighestOwnBid,
                                    ItemName = b.ItemName
                                })
                                .OrderByDescending (b => b.End)
                                .ToList();
                return aggregatedBids;
            }
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
            [Key("tag")]
            public string Tag;
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
                Tag=a.Tag;
            }

            public BidResult(){ }
        }
    }
}
