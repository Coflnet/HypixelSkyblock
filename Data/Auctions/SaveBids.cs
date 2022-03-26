using System;
using System.Collections.Generic;
using MessagePack;
using Newtonsoft.Json;

namespace Coflnet.Sky.Core
{
    [MessagePackObject]
    public class SaveBids {
        [IgnoreMember]
        [JsonIgnore]
        public int Id {get;set;}
        [IgnoreMember]
        [JsonIgnore]
        [System.ComponentModel.DataAnnotations.Schema.ForeignKey("Uuid")]
        public SaveAuction Auction {get;set;}
        [Key (0)]
        [JsonIgnore]
        public string AuctionId;
        [Key (1)]
        [System.ComponentModel.DataAnnotations.Schema.Column(TypeName = "char(32)")]
        [JsonProperty("bidder")]
        public string Bidder {get;set;}

        [Key (2)]
        [System.ComponentModel.DataAnnotations.Schema.Column(TypeName = "char(32)")]
        [JsonProperty("profileId")]
        /// <summary>
        /// Will be null if it is the same as <see cref="Bidder"/>
        /// </summary>
        public string ProfileId {get; set;}
        [Key (3)]
        [JsonProperty("amount")]
        public long Amount {get; set;}
        [Key (4)]
        [JsonProperty("timestamp")]
        public DateTime Timestamp {get; set;}
        [Key(5)]

        [JsonIgnore]
        public int BidderId {get;set;}

        [IgnoreMember]
        [JsonIgnore]
        public Player player;


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