using System.Collections.Generic;
using MessagePack;
using Newtonsoft.Json;
using System;

namespace dev
{
    [MessagePackObject]
    public class ProductInfo
    {
        [Key(0)]
        [JsonProperty("product_id")]
        public string ProductId { get; private set; }
        [Key(1)]
        [JsonProperty("buy_summary")]
        public List<Order> BuySummery { get; private set; }
        [Key(2)]
        [JsonProperty("sell_summary")]
        public List<Order> SellSummary { get; private set; }
        [Key(3)]
        [JsonProperty("quick_status")]
        public QuickStatus QuickStatus { get; private set; }
        [Key(4)]
        public DateTime Timestamp;
    }
}