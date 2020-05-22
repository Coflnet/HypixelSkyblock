using Hypixel.NET.SkyblockApi.Bazaar;
using MessagePack;
using Newtonsoft.Json;

namespace dev
{
    public class BuyOrder : Order 
    {
        public BuyOrder() { }
        public BuyOrder(Summary s) : base(s)
        { }
    }

    public class SellOrder : Order 
    {
        public SellOrder() { }
        public SellOrder(Summary s) : base(s)
        { }
    }


    [MessagePackObject]
    public class Order
    {
        public Order() {}
        public Order(Summary s)
        {
            this.Amount = (int)s.Amount;
            this.Orders = (short)s.Orders;
            this.PricePerUnit = s.PricePerUnit;
        }

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