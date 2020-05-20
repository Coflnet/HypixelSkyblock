using System;
using MessagePack;

namespace hypixel
{
    [MessagePackObject]
    public class SaveBids {
        [Key (0)]
        public string AuctionId;
        [Key (1)]
        public string Bidder {get;set;}
        [Key (2)]
        /// <summary>
        /// Will be null if it is the same as <see cref="Bidder"/>
        /// </summary>
        public string ProfileId;
        [Key (3)]
        public long Amount;
        [Key (4)]
        public DateTime Timestamp;

        public SaveBids (Hypixel.NET.SkyblockApi.AuctionByPage.Bids bid) {
            AuctionId = bid.AuctionId.Substring (0, 5);
            Bidder = bid.Bidder;
            ProfileId = bid.ProfileId == bid.Bidder ? null : bid.ProfileId;
            Amount = bid.Amount;
            Timestamp = bid.Timestamp;
        }

        public SaveBids () { }
    }

}