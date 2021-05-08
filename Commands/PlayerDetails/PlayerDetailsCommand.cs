using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using MessagePack;
using Microsoft.EntityFrameworkCore;

namespace hypixel {
    public class PlayerDetailsCommand : Command {
        [MessagePackObject]
        public class Result {
            [Key ("bids")]
            public List<BidResult> Bids = new List<BidResult> ();

            [Key ("auctions")]
            public List<AuctionResult> Auctions = new List<AuctionResult> ();
        }

        [MessagePackObject]
        public class BidResult {
            [Key ("highestOwn")]
            public long HighestOwnBid;
            [Key ("highestBid")]
            public long HighestBid;
            [Key ("itemName")]
            public string ItemName;
            [Key ("auctionId")]
            public string AuctionId;
            [Key ("end")]
            public DateTime End;
        }

        [MessagePackObject]
        public class AuctionResult {
            [Key ("auctionId")]
            public string AuctionId;
            [Key ("highestBid")]
            public long HighestBid;
            [Key ("itemName")]
            public string ItemName;
            [Key ("end")]
            public DateTime End;

            public AuctionResult (SaveAuction a) {
                AuctionId = a.Uuid;
                HighestBid = a.HighestBidAmount;
                ItemName = a.ItemName;
                End = a.End;
            }

            public AuctionResult () { }
        }

        public override void Execute (MessageData data) {
            Result result = new Result ();

            Regex rgx = new Regex ("[^a-f0-9]");
            var search = rgx.Replace (data.Data, "");

            result.Bids.Add (new BidResult () { ItemName = "Loading ..." });
            result.Bids.Add (new BidResult () { ItemName = "This takes a minute :/" });
            result.Auctions.Add (new AuctionResult () { ItemName = "Loading the latest data" });
            data.SendBack (data.Create ("playerResponse", result,A_HOUR));

            using (var context = new HypixelContext ()) {
                var playerQuery = context.Players.Where (p => p.UuId == search);
                var playerWithAuctions = playerQuery
                 //   .Include (p => p.Auctions)
                    .First();

                result.Auctions = context.Auctions
                    .Where(e=>e.SellerId == playerWithAuctions.Id)
                    .Select (a => new AuctionResult (a))
                    .OrderByDescending (a => a.End)
                    .ToList ();

                // just the auctions for now
                data.SendBack (data.Create ("playerResponse", result,A_HOUR));


                var playerBids = context.Bids.Where(b=>b.Bidder == search)
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
                                .Select(b=>new BidResult(){
                                    HighestBid = b.HighestBid,
                                    AuctionId=b.Key,
                                    End = b.End,
                                    HighestOwnBid = b.HighestOwnBid,
                                    ItemName = b.ItemName
                                })
                                .OrderByDescending (b => b.End)
                                .ToList();

                result.Bids = aggregatedBids;
                //data.SendBack (MessageData.Create ("playerResponse", result));

                // fetch full auction data
                //var auctionsForBids = context.Auctions.Where(a=>result.Bids.Any(b=>b.==a.Id))
            }

            data.SendBack (data.Create ("playerResponse", result,A_HOUR));
        }

        private static SaveBids FindHighestOwnBid (Player databaseResult, SaveAuction a) {
            return a.Bids.Where (bid => bid.Bidder == databaseResult.UuId)
                .OrderByDescending (bid => bid.Amount).FirstOrDefault ();
        }

        private static BidResult NewBidResult (SaveAuction a, SaveBids highestOwn) {
            return new BidResult () {
                AuctionId = a.Uuid,
                    HighestBid = a.Bids.Last ().Amount,
                    HighestOwnBid = highestOwn.Amount,
                    ItemName = a.ItemName,
                    End = a.End
            };
        }
    }
}