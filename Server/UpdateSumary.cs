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