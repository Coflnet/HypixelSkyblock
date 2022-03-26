using System;
using MessagePack;

namespace Coflnet.Sky.Core
{
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
        [Key("bin")]
        public bool Bin;

        public AuctionResult(SaveAuction a)
        {
            AuctionId = a.Uuid;
            HighestBid = a.HighestBidAmount;
            ItemName = a.ItemName;
            End = a.End;
            Tag = a.Tag;
            StartingBid = a.StartingBid;
            Bin = a.Bin;
        }

        public AuctionResult()
        {
        }
    }
}
