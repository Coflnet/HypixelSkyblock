using MessagePack;
using Newtonsoft.Json;

namespace dev
{
    [MessagePackObject]
    public class QuickStatus
    {
        [Key(0)]
        [JsonProperty("productId")]
        public string ProductId { get; set; }
        [Key(1)]
        [JsonProperty("buyPrice")]
        public double BuyPrice { get; set; }
        [Key(2)]
        [JsonProperty("buyVolume")]
        public long BuyVolume { get; set; }
        [Key(3)]
        [JsonProperty("buyMovingWeek")]
        public long BuyMovingWeek { get; set; }
        [Key(4)]
        [JsonProperty("buyOrders")]
        public int BuyOrders { get; set; }
        [Key(5)]
        [JsonProperty("sellPrice")]
        public double SellPrice { get; set; }
        [Key(6)]
        [JsonProperty("sellVolume")]
        public long SellVolume { get; set; }
        [Key(7)]
        [JsonProperty("sellMovingWeek")]
        public long SellMovingWeek { get; set; }
        [Key(8)]
        [JsonProperty("sellOrders")]
        public int SellOrders { get; set; }
    }
}