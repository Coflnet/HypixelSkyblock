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

            using (var context = new HypixelContext ()) {
                var databaseResult = context.Players.Where(p=>p.UuId==search).Include (p => p.Auctions)
                    .Include (p => p.Bids)
                    .ThenInclude (b => b.Auction)
                    .ThenInclude (a => a.Bids).First ();

                result.Auctions = databaseResult.Auctions.Select (a => new AuctionResult (a)).ToList ();

                foreach (var item in databaseResult.Bids)
                {
                    var a = item.Auction;

                    if (a.Bids == null || a.Bids.Count == 0)
                    {
                        Console.WriteLine("Auction has no bids");
                        continue;
                    }
                    SaveBids highestOwn = FindHighestOwnBid(databaseResult, a);

                    if (highestOwn == null)
                    {
                        Console.WriteLine("no highest own");
                        continue;
                    }

                    result.Bids.Add(NewBidResult(a, highestOwn));
                }
            }

            data.SendBack (MessageData.Create ("playerResponse", result));
        }

        private static SaveBids FindHighestOwnBid(Player databaseResult, SaveAuction a)
        {
            return a.Bids.Where(bid => bid.Bidder == databaseResult.UuId)
                .OrderByDescending(bid => bid.Amount).FirstOrDefault();
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