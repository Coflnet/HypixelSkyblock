using System;
using System.Collections.Generic;
using MessagePack;

namespace Coflnet.Sky.Core
{
    [MessagePackObject]
    public class ItemIndexElement
    {
        [Key(0)]
        public string UUId;
        [Key(1)]
        public DateTime End;
        [Key(2)]
        public long Price;
        [Key(3)]
        public ItemReferences.Reforge Reforge;
        [Key(4)]
        public List<Enchantment> Enchantments;
        [Key(5)]
        public long Count;
        [Key(6)]
        public short BidCount;

        public ItemIndexElement(SaveAuction auction) : this(
            auction.Uuid,
            auction.End,
            auction.HighestBidAmount,
            ItemReferences.GetReforges(auction.ItemName),
            auction.Enchantments,
            auction.Count,
            (short)auction.Bids.Count)
            {}

        public ItemIndexElement(string uUId, DateTime end, long price, ItemReferences.Reforge reforge, List<Enchantment> enchantments,long count,short bidCount)
        {
            UUId = uUId;
            End = end;
            Price = price;
            Reforge = reforge;
            Enchantments = enchantments;
            Count = count;
            BidCount = bidCount;
        }

        public ItemIndexElement(){}

        public override bool Equals(object obj)
        {
            return obj is ItemIndexElement element &&
                   UUId == element.UUId ;
        }

        public override int GetHashCode()
        {
            return UUId.GetHashCode();
        }
    }
}
