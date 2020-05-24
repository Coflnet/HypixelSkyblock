using System;
using System.Collections.Generic;
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

        public override bool Equals(object obj)
        {
            return obj is SaveBids bids &&
                   EqualityComparer<SaveAuction>.Default.Equals(Auction, bids.Auction) &&
                   AuctionId == bids.AuctionId &&
                   Bidder == bids.Bidder &&
                   ProfileId == bids.ProfileId &&
                   Amount == bids.Amount &&
                   Timestamp == bids.Timestamp;
        }

        public override int GetHashCode()
        {
            int hashCode = 291388595;
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(AuctionId);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Bidder);
            hashCode = hashCode * -1521134295 + Amount.GetHashCode();
            hashCode = hashCode * -1521134295 + Timestamp.GetHashCode();
            return hashCode;
        }
    }

}