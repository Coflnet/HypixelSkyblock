using System;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Coflnet.Sky.FlipTracker
{
    [DataContract]
    public class FlipEvent
    {
        [IgnoreDataMember]
        [JsonIgnore]
        public int Id { get; set; }
        [DataMember(Name = "playerId")]
        public long PlayerUid { get; set; }
        [DataMember(Name = "auctionId")]
        public long AuctionUid { get; set; }
        [DataMember(Name = "type")]
        public FlipEventType Type { get; set; }
        [System.ComponentModel.DataAnnotations.Timestamp]
        [DataMember(Name = "timestamp")]
        public DateTime Timestamp { get; set; }
    }

    public enum FlipEventType
    {
        FLIP_RECEIVE = 1,
        FLIP_CLICK = 2,
        PURCHASE_START = 4,
        PURCHASE_CONFIRM = 8,
        AUCTION_SOLD = 16,
        UPVOTE = 32,
        DOWNVOTE = 64
    }
}