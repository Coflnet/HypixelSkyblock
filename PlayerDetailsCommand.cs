using System;
using System.Collections.Generic;
using System.Linq;
using MessagePack;

namespace hypixel
{
    public class PlayerDetailsCommand : Command
    {
        [MessagePackObject]
        public class Result
        {
            [Key("bids")]
            public List<BidResult> Bids;

            [Key("auctions")]
            public List<SaveAuction> Auctions;
        }

        [MessagePackObject]
        public class BidResult
        {
            [Key("highestOwn")]
            public int HighestOwnBid;
            [Key("highestBid")]
            public int HighestBid;
            [Key("itemName")]
            public string ItemName;
            [Key("auctionId")]
            public string AuctionId;
        }

        public override void Execute(MessageData data)
        {
            Result result = new Result();

            var displayUser = StorageManager.GetOrCreateUser(data.Data);
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

                Console.WriteLine($"On {a.ItemName} {highestOwn.Amount} \tTop {highestOwn.Amount == a.HighestBidAmount} {highestOwn.Timestamp} ({item.auctionId.Substring(0,10)})");
            }

            Console.WriteLine("Auctions:");
            foreach (var item in displayUser.auctionIds)
            {
                var a = StorageManager.GetOrCreateAuction(item);
                if(a.Enchantments != null && a.Enchantments.Count > 0){
                    // enchanted is only one item
                    Console.WriteLine($"{a.ItemName}  for {a.HighestBidAmount} End {a.End} ({item.Substring(0,10)})");
                    foreach (var enachant in a.Enchantments)
                    {
                        Console.WriteLine($"-- {enachant.Type} {enachant.Level}");
                    }
                } else
                    // not enchanted may be multiple (Count)
                    Console.WriteLine($"{a.ItemName} (x{a.Count}) for {a.HighestBidAmount} End {a.End} ({item.Substring(0,10)})");
            }
        }
    }
}
