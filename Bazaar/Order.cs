using MessagePack;
using Newtonsoft.Json;

namespace dev
{
    public class BuyOrder : Order 
    {
        public BuyOrder() { }
    }

    public class SellOrder : Order 
    {
        public SellOrder() { }
    }


    [MessagePackObject]
    public class Order
    {
        public Order() {}

        [IgnoreMember]
        public int Id {get;set;}
        [Key(0)]
        [JsonProperty("amount")]
        public int Amount { get; set; }
        [Key(1)]
        [JsonProperty("pricePerUnit")]
        public double PricePerUnit { get; set; }
        [Key(2)]
        [JsonProperty("orders")]
        public short Orders { get; set; }

        public bool ValueSame(Order order)
        {
            return Amount == order.Amount
                && PricePerUnit == order.PricePerUnit
                && Orders == order.Orders;
        }
    }
}