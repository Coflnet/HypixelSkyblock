using System.Collections.Generic;

namespace Coflnet.Sky.Core.Data
{
    public class TradeOffer
    {
        public string PlayerUUid;

        public TradeSide Search;

        public List<TradeSide> Offers;
    }

    public class TradeSide
    {
        public List<SlotContent> Items;
    }

    public class SlotContent
    {
        public string ItemName;

        public List<Enchantment> Enchantments;

        public short Count;
    }
}