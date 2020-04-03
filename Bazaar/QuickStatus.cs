using MessagePack;
using Newtonsoft.Json;

namespace dev
{
    [MessagePackObject]
    public class QuickStatus
    {
        [Key(0)]
        [JsonProperty("productId")]
        public string ProductId { get; private set; }
        [Key(1)]
        [JsonProperty("buyPrice")]
        public double BuyPrice { get; private set; }
        [Key(2)]
        [JsonProperty("buyVolume")]
        public long BuyVolume { get; private set; }
        [Key(3)]
        [JsonProperty("buyMovingWeek")]
        public long BuyMovingWeek { get; private set; }
        [Key(4)]
        [JsonProperty("buyOrders")]
        public int BuyOrders { get; private set; }
        [Key(5)]
        [JsonProperty("sellPrice")]
        public double SellPrice { get; private set; }
        [Key(6)]
        [JsonProperty("sellVolume")]
        public long SellVolume { get; private set; }
        [Key(7)]
        [JsonProperty("sellMovingWeek")]
        public long SellMovingWeek { get; private set; }
        [Key(8)]
        [JsonProperty("sellOrders")]
        public int SellOrders { get; private set; }
    }
}