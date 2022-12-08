using System;
using System.Linq;
using MessagePack;

namespace Coflnet.Sky.Core
{
    [MessagePackObject]
    public class SubscribeItem
    {
        [IgnoreMember]
        public int Id { get; set; }
        /// <summary>
        /// Either User,auction or ItemId UserIds are +100.000
        /// </summary>
        /// <value></value>
        [Key("topicId")]
        [System.ComponentModel.DataAnnotations.MaxLength(45)]
        public string TopicId { get; set; }
        /// <summary>
        /// Price point in case of item
        /// </summary>
        /// <value></value>
        [Key("price")]
        public long Price { get; set; }

        [System.ComponentModel.DataAnnotations.Timestamp]
        [IgnoreMember]
        public DateTime GeneratedAt { get; set; }

        public enum SubType
        {
            NONE = 0,
            PRICE_LOWER_THAN = 1,
            PRICE_HIGHER_THAN = 2,
            OUTBID = 4,
            SOLD = 8,
            BIN = 16,
            USE_SELL_NOT_BUY = 32,
            AUCTION = 64,
            PLAYER = 128
        }

        [Key("type")]
        public SubType Type { get; set; }

        [IgnoreMember]
        public int UserId { get; set; }
        [IgnoreMember]
        public DateTime NotTriggerAgainBefore { get; set; }

        [Key("filter")]
        public string Filter { get; set; }
    }
}
