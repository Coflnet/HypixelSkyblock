using System.Collections.Generic;
using MessagePack;
using Newtonsoft.Json;
using System;

namespace dev
{
    [MessagePackObject]
    public class ProductInfo
    {
        public ProductInfo() { }

        [IgnoreMember]
        public BazaarPull PullInstance { get; set; }

        [IgnoreMember]
        public int Id { get; set; }

        [Key(0)]
        [JsonProperty("product_id")]
        [System.ComponentModel.DataAnnotations.MaxLength(40)]
        public string ProductId { get; set; }
        /// <summary>
        /// Sell orders
        /// </summary>
        [Key(1)]
        [JsonProperty("buy_summary")]
        public List<BuyOrder> BuySummery { get; set; }
        /// <summary>
        /// Buy orders
        /// </summary>
        [Key(2)]
        [JsonProperty("sell_summary")]
        public List<SellOrder> SellSummary { get; set; }
        [Key(3)]
        [JsonProperty("quick_status")]
        public QuickStatus QuickStatus { get; set; }
        [Key(4)]
        //[JsonIgnore]
        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public DateTime Timestamp { get; set; }
    }
}