using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using MessagePack;

namespace hypixel
{
    public partial class ItemDetails
    {
        [MessagePackObject]
        public class Item : IItem
        {
            public static Item Default = new Item() { Id = "unknown", Description = "This item has not yet been reviewed by our team" };

            [Key(0)]
            public string Id;
            [NotMapped]
            [Key(1)]
            public HashSet<string> AltNames = new HashSet<string>();
            [Key(2)]
            public string Description;
            [Key(3)]
            public string IconUrl { get; set; }

            [Key(4)]
            public string Category;
            [Key(5)]
            public string Extra;
            [Key(6)]
            public string Tier;
            [Key(7)]
            public string MinecraftType { get; set; }

            [Key(8)]
            public string color;

        }

        private const int MAX_MEDIUM_INT = 8388607;
        private static ConcurrentDictionary<string, DBItem> ToFillDetails = new ConcurrentDictionary<string, DBItem>();

        public int GetOrCreateItemIdForAuction(SaveAuction auction, HypixelContext context)
        {
            var tag = GetIdForName(auction.ItemName);
            if (tag != null && TagLookup.TryGetValue(tag, out int value))
                return value;

            // doesn't exist yet, create it
            var itemByTag = context.Items.Where(item => item.Tag == auction.Tag).FirstOrDefault();
            if (itemByTag != null)
            {
                // new alternative name
                this.ReverseNames[auction.ItemName] = auction.Tag;
                context.AltItemNames.Add(new AlternativeName() { DBItemId = itemByTag.Id, Name = auction.ItemName });
                return itemByTag.Id;
            }
            // new Item
            //var tempAuction = new Hypixel.NET.SkyblockApi.Auction(){Category=auction.Category,};
            //AddNewItem(tempAuction,auction.ItemName,auction.Tag,null);
            var item = new DBItem()
            {
                Tag = auction.Tag, Name = auction.ItemName,
                Names = new List<AlternativeName>() { new AlternativeName() { Name = auction.ItemName } }
            };
            if (item.Tag == null)
            {
                // unindexable item
                return MAX_MEDIUM_INT;
            }
            ToFillDetails[item.Tag] = item;
            return AddItemToDB(item);
            //throw new CoflnetException("can_add","can't add this item");
        }
    }

}