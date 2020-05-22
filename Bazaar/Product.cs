using System.Collections.Generic;
using MessagePack;
using Newtonsoft.Json;
using System;
using Hypixel.NET.SkyblockApi.Bazaar;
using System.Linq;

namespace dev
{


    [MessagePackObject]
    public class ProductInfo
    {
        public ProductInfo() { }

        public ProductInfo(Product value,BazaarPull pull)
        {
            this.ProductId = value.ProductId;
            this.BuySummery = value.BuySummary.Select(s=>new BuyOrder(s)).ToList();
            this.SellSummary = value.SellSummary.Select(s=>new SellOrder(s)).ToList();
            this.QuickStatus = new QuickStatus( value.QuickStatus);
            this.PullInstance = pull;
        }

        [IgnoreMember]
        public BazaarPull PullInstance {get;set;}

        [IgnoreMember]
        public int Id{get;set;}

        [Key(0)]
        [JsonProperty("product_id")]
        [System.ComponentModel.DataAnnotations.MaxLength(40)]
        public string ProductId { get; set; }
        [Key(1)]
        [JsonProperty("buy_summary")]
        public List<BuyOrder> BuySummery { get; set; }
        [Key(2)]
        [JsonProperty("sell_summary")]
        public List<SellOrder> SellSummary { get;  set; }
        [Key(3)]
        [JsonProperty("quick_status")]
        public QuickStatus QuickStatus { get;  set; }
        [Key(4)]
        public DateTime Timestamp {get;set;}
    }
}