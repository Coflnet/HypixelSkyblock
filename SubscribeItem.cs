using System;
using System.Linq;
using MessagePack;

namespace hypixel
{
    [MessagePackObject]
    public class SubscribeItem
    {
        [Key("name")]
        public string itemName;
        [Key("category")]
        public string itemCategory;
        [Key("count")]
        public short itemCount;
        [Key("playerId")]
        public string playerUUid;
        [Key("minPrice")]
        public long minPrice;
        [Key("maxPrice")]
        public long maxPrice;

        [Key("enchantments")]
        public Enchantment[] enchantments;


        public enum Type
        {
            NewAuction = 1,
            NewBid = 2
        }

        public Type type;


        public bool Match(SaveAuction auction)
        {
            if(!String.IsNullOrEmpty(itemName) && !auction.ItemName.StartsWith(itemName))
            {
                return false;
            }
            if(type == Type.NewAuction)
            {
                if(!String.IsNullOrEmpty(playerUUid) && auction.Auctioneer != playerUUid)
                {
                    return false;
                }
            } else if(type == Type.NewBid)
            {
                if(!String.IsNullOrEmpty(playerUUid) && !auction.Bids.Where(b=>b.Bidder == playerUUid).Any())
                {
                    return false;
                }
            }

            // matching category
            if(!String.IsNullOrEmpty(itemCategory) && auction.Category.ToString().ToLower() != itemCategory.ToLower())
            {
                return false;
            }

            // matching count
            if(itemCount != 0 && auction.Count != itemCount)
            {
                return false;
            }

            var itemPrice = auction.StartingBid;
            if(auction.Bids.Count > 0)
            {
                itemPrice = auction.HighestBidAmount;
            }

            // in price range
            if((maxPrice != 0 && itemPrice > maxPrice) ||itemPrice < minPrice)
            {
                return false;
            }

            if(enchantments != null)
            {
                // make sure there are all the required enchantments
                return enchantments.Except(auction.Enchantments).Any();
            }
            
            return true;
        }
    }
}
