using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using MessagePack;

namespace hypixel
{
    public class PlayerDetailsCommand : Command
    {
        [MessagePackObject]
        public class Result
        {
            [Key("bids")]
            public List<BidResult> Bids = new List<BidResult>();

            [Key("auctions")]
            public List<AuctionResult> Auctions = new List<AuctionResult>();
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
            [Key("auctionId")]
            public string AuctionId;
            [Key("end")]
            public DateTime End;
        }

        [MessagePackObject]
        public class AuctionResult
        {
            [Key("auctionId")]
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

        public override void Execute(MessageData data)
        {
            Result result = new Result();

            Regex rgx = new Regex("[^a-f0-9]");
            var search = rgx.Replace(data.Data, "");

            var displayUser = StorageManager.GetOrCreateUser(search);
            foreach (var item in displayUser.Bids)
            {
                var a = StorageManager.GetOrCreateAuction(item.auctionId,null,true);

                if(a.Bids == null || a.Bids.Count == 0)
                {
                    continue;
                }
                var highestOwn = a.Bids.Where(bid=>bid.Bidder == displayUser.uuid)
                            .OrderByDescending(bid=>bid.Amount).FirstOrDefault();

                if(highestOwn == null)
                {
                    continue;
                }

                result.Bids.Add(new BidResult(){
                    AuctionId = a.Uuid,
                    HighestBid = a.Bids.Last().Amount,
                    HighestOwnBid = highestOwn.Amount,
                    ItemName = a.ItemName,
                    End=a.End
                });
            }

            foreach (var item in displayUser.auctionIds)
            {
                result.Auctions.Add(new AuctionResult(StorageManager.GetOrCreateAuction(item)));
            }

            data.SendBack(MessageData.Create("playerResponse",result));
        }
    }
}
