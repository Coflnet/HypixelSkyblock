using MessagePack;
using Newtonsoft.Json;

namespace dev
{
    [MessagePackObject]
    public class Order
    {
        [Key(0)]
        [JsonProperty("amount")]
        public int Amount { get; set; }
        [Key(1)]
        [JsonProperty("pricePerUnit")]
        public double PricePerUnit { get; set; }
        [Key(2)]
        [JsonProperty("orders")]
        public int Orders { get; set; }
    }
}