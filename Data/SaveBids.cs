using System;
using MessagePack;

namespace hypixel
{
    [MessagePackObject]
    public class SaveBids {
        [IgnoreMember]
        public int Id {get;set;}
        [IgnoreMember]
        [System.ComponentModel.DataAnnotations.Schema.ForeignKey("Uuid")]
        public SaveAuction Auction {get;set;}
        [Key (0)]
        public string AuctionId;
        [Key (1)]
        [System.ComponentModel.DataAnnotations.Schema.Column(TypeName = "char(32)")]
        public string Bidder {get;set;}

        [Key (2)]
        [System.ComponentModel.DataAnnotations.Schema.Column(TypeName = "char(32)")]
        /// <summary>
        /// Will be null if it is the same as <see cref="Bidder"/>
        /// </summary>
        public string ProfileId {get; set;}
        [Key (3)]
        public long Amount {get; set;}
        [Key (4)]
        public DateTime Timestamp {get; set;}

        [IgnoreMember]
        public Player player;

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