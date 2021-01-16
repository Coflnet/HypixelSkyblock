using System.Collections.Generic;

namespace hypixel
{
    class UpdateSumary
    {
        public HashSet<AuctionEvent> Solds = new HashSet<AuctionEvent>();
        public HashSet<HypixelEvent> Items = new HashSet<HypixelEvent>();
        public HashSet<AuctionEvent> OutBids = new HashSet<AuctionEvent>();

        public class HypixelEvent
        {
            public string ItemTag;
            public long Amount;

            public HypixelEvent(string itemTag, long amount)
            {
                ItemTag = itemTag;
                Amount = amount;
            }

            public override bool Equals(object obj)
            {
                return obj is HypixelEvent @event &&
                       ItemTag == @event.ItemTag &&
                       Amount == @event.Amount;
            }

            public override int GetHashCode()
            {
                int hashCode = -355338609;
                hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(ItemTag);
                hashCode = hashCode * -1521134295 + Amount.GetHashCode();
                return hashCode;
            }
        }

        public class AuctionEvent : HypixelEvent
        {
            public string Player;

            public AuctionEvent(string itemTag, long amount, string player)
            : base(itemTag, amount)
            {
                Player = player;
            }
        }

        public void OutBid(string tag, long amount, string player)
        {
            var name = PlayerSearch.Instance.GetName(player);
            OutBids.Add(new AuctionEvent(tag, amount, name));
        }

        public void Sold(string tag, int amount, string player)
        {
            var name = PlayerSearch.Instance.GetName(player);
            Solds.Add(new AuctionEvent(tag, amount, name));
        }
    }
}