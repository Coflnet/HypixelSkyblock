using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using MessagePack;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

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

        public class ItemSearchResult
        {
            [Key(0)]
            public string Name;

            [Key(1)]
            public string Tag;

            [Key(2)]
            public string IconUrl;

            [Key(2)]
            public int HitCount;
        }

        internal IEnumerable<ItemSearchResult> Search(string search, int count = 5)
        {
            //return this.ReverseNames.Keys
            //    .Where(key => key.StartsWith(search))
            //    .Select(key => new ItemSearchResult() { Name = key, Tag = ReverseNames[key] });
            using(var context = new HypixelContext())
            {
                var items = context.Items
                    .Include(item => item.Names)
                    .Where(item =>
                        item.Names
                        .Where(name => EF.Functions.Like(name.Name, search + '%')).Any()
                    ).OrderBy(item => item.Name.Length - item.HitCount)
                    .Take(count);

                return items.ToList()
                    .Select(item => new ItemSearchResult()
                    {
                        Name = ItemReferences.RemoveReforgesAndLevel(item.Names
                                .Where(n => n != null && n.Name != null && n.Name.ToLower().StartsWith(search.ToLower()))
                                .FirstOrDefault()?.Name) ?? item.Name,
                            Tag = item.Tag,
                            IconUrl = item.IconUrl,
                            HitCount = item.HitCount
                    });
            }
        }

        private const int MAX_MEDIUM_INT = 8388607;
        private static ConcurrentDictionary<string, DBItem> ToFillDetails = new ConcurrentDictionary<string, DBItem>();

        public int GetOrCreateItemIdForAuction(SaveAuction auction, HypixelContext context)
        {
            var clearedName = ItemReferences.RemoveReforgesAndLevel(auction.ItemName);
            var tag = GetIdForName(clearedName ?? auction.Tag) ;
            if (tag != null && TagLookup.TryGetValue(tag, out int value))
                return value;
            

            Console.WriteLine($"Creating item {auction.ItemName}");
            // doesn't exist yet, create it
            var itemByTag = context.Items.Where(item => item.Tag == auction.Tag).FirstOrDefault();
            if (itemByTag != null)
            {
                // new alternative name
                if (clearedName != null)
                    this.ReverseNames[clearedName] = auction.Tag;
                var exists = context.AltItemNames
                    .Where(name => name.Name == clearedName && name.DBItemId == itemByTag.Id)
                    .Any();
                if (!exists)
                    context.AltItemNames.Add(new AlternativeName() { DBItemId = itemByTag.Id, Name = clearedName });
                return itemByTag.Id;
            }
            Console.WriteLine($"!! completely new !! {JsonConvert.SerializeObject(auction)}");
            // new Item
            //var tempAuction = new Hypixel.NET.SkyblockApi.Auction(){Category=auction.Category,};
            //AddNewItem(tempAuction,auction.ItemName,auction.Tag,null);
            var item = new DBItem()
            {
                Tag = auction.Tag, 
                Name = auction.ItemName,
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