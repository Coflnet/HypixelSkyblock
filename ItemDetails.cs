using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Coflnet;
using fNbt;
using Hypixel.NET.SkyblockApi;
using Newtonsoft.Json;

namespace hypixel
{
    public partial class ItemDetails
    {
        public static ItemDetails Instance;

        public Dictionary<string, Item> Items = new Dictionary<string, Item>();
        /// <summary>
        /// Contains the Tags indexed by name [Name]=Tag
        /// </summary>
        /// <typeparam name="string"></typeparam>
        /// <typeparam name="string"></typeparam>
        /// <returns></returns>
        public Dictionary<string, string> ReverseNames = new Dictionary<string, string>();
        /// <summary>
        /// Contains a cache for <see cref="DBItem.Tag"/> to <see cref="DBItem.Id"/>
        /// </summary>
        /// <typeparam name="string"></typeparam>
        /// <typeparam name="int"></typeparam>
        /// <returns></returns>
        public Dictionary<string, int> TagLookup = new Dictionary<string, int>();

        static ItemDetails()
        {
            Instance = new ItemDetails();
            Instance.Load();
        }

        public void LoadFromDB()
        {
            using(var context = new HypixelContext())
            {
                var items = context.Items.Where(item => item.Description == null);
                foreach (var item in items)
                {
                    ToFillDetails.TryAdd(item.Tag, item);
                }
                TagLookup = context.Items.Where(item=>item.Tag != null).Select(item=>new {item.Tag,item.Id})
                .ToDictionary(item=>item.Tag,item=>item.Id);
            }
        }

        public void Load()
        {
            if (FileController.Exists("itemDetails"))
                Items = FileController.LoadAs<Dictionary<string, Item>>("itemDetails");

            if (Items == null)
            {
                Items = new Dictionary<string, Item>();
            }
            // correct keys
            foreach (var item in Items.Keys.ToList())
            {
                if (Items[item].Id != item)
                {
                    Items.TryAdd(Items[item].Id, Items[item]);
                    Items.Remove(item);
                }
            }

            foreach (var item in Items)
            {
                if (item.Value.AltNames == null)
                    continue;
                item.Value.AltNames = item.Value.AltNames?.Select(s => ItemReferences.RemoveReforgesAndLevel(s)).ToHashSet();

                foreach (var name in item.Value.AltNames)
                {
                    if (!ReverseNames.TryAdd(name, item.Key))
                    {
                        // make a good guess, anyone?
                    }
                }
            }

        }

        public string GetIdForName(string name)
        {
            var normalizedName = ItemReferences.RemoveReforgesAndLevel(name);
            return ReverseNames.GetValueOrDefault(normalizedName, normalizedName);
        }

        /// <summary>
        /// Fast access to an item id for index lookup
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public int GetItemIdForName(string name)
        {
            var tag = GetIdForName(name);
            if (!TagLookup.TryGetValue(tag, out int value))
                throw new CoflnetException("item_not_found", $"could not find the item with the name `{name}`");
            return value;
        }

        public IEnumerable<string> AllItemNames()
        {
            return ReverseNames.Keys;
        }

        public void AddOrIgnoreDetails(Auction a)
        {
            var id = NBT.ItemID(a.ItemBytes);

            var name = ItemReferences.RemoveReforgesAndLevel(a.ItemName);

            if (ToFillDetails.TryRemove(id, out DBItem i))
            {
                Console.WriteLine("Filling details for " + i.Tag + i.Id);
                AddNewItem(a, name, id, i);
                return;
            }
            if (Items.ContainsKey(id))
            {
                var tragetItem = Items[id];
                if (tragetItem.AltNames == null)
                    tragetItem.AltNames = new HashSet<string>();

                // try to get shorter lore
                if (Items[id]?.Description?.Length > a?.ItemLore?.Length && a.ItemLore.Length > 10)
                {
                    Items[id].Description = a.ItemLore;
                }
                tragetItem.AltNames.Add(name);
                return;
            }
            // legacy item names
            if (Items.ContainsKey(name))
            {
                var item = Items[name];
                item.Id = id;
                if (item.AltNames == null)
                {
                    item.AltNames = new HashSet<string>();
                }
                item.AltNames.Add(name);
                Items[id] = item;
                Items.Remove(name);

                return;
            }

            // new item, add it
            AddNewItem(a, name, id, i);
        }

        private void AddNewItem(Auction a, string name, string tag, DBItem existingItem = null)
        {
            var i = new Item();
            i.Id = tag;
            i.AltNames = new HashSet<string>() { name };
            i.Tier = a.Tier;
            i.Category = a.Category;
            i.Description = a.ItemLore;
            i.Extra = a.Extra;
            i.MinecraftType = MinecraftTypeParser.Instance.Parse(a);

            //Console.WriteLine($"New: {name} ({i.MinecraftType})" );
            SetIconUrl(a, i);

            Items[name] = i;
            var newItem = new DBItem(i);
            if (existingItem == null)
                AddItemToDB(newItem);
            else
                UpdateItem(existingItem, newItem);
        }

        private void UpdateItem(DBItem existingItem, DBItem newItem)
        {
            Console.WriteLine("updating item");
            using(var context = new HypixelContext())
            {
                newItem.Id = existingItem.Id;
                context.Items.Update(newItem);
                context.SaveChanges();
            }
        }

        private static void SetIconUrl(Auction a, IItem i)
        {
            if (i.MinecraftType == "skull")
            {
                try
                {
                    i.IconUrl = $"https://mc-heads.net/head/{Path.GetFileName(NBT.SkullUrl(a.ItemBytes))}/50";
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Error :O \n {e.Message} \n {e.StackTrace}");
                }
                // Console.WriteLine(i.IconUrl);
            }
            else
            {
                var t = MinecraftTypeParser.Instance.GetDetails(i.MinecraftType);
                i.IconUrl = $"https://skyblock-backend.coflnet.com/static/{t?.type}-{t?.meta}.png";
            }
        }

        private int AddItemToDB(DBItem item)
        {
            using(var context = new HypixelContext())
            {
                context.Items.Add(item);
                context.SaveChanges();
                return item.Id;
            }
        }

        /// <summary>
        /// Tries to find and return an item by name
        /// </summary>
        /// <param name="fullName">Full Item name</param>
        /// <returns></returns>
        public DBItem GetDetails(string fullName)
        {
            if (Items == null)
                Load();
            var name = ItemReferences.RemoveReforgesAndLevel(fullName);
            /*if (ReverseNames.TryGetValue(name, out string key) &&
                Items.TryGetValue(key, out Item value))
            {
                return value;
            }*/

            using(var context = new HypixelContext())
            {
                var id = context.AltItemNames.Where(name => name.Name == fullName || name.Name == name)
                    .Select(name => name.Id).FirstOrDefault();
                if (id > 1)
                {
                    var item = context.Items.Where(i => i.Id == id).First();
                    item.Name = fullName;
                    return item;
                }
            }

            return new DBItem() { Tag = "Unknown" };
        }

        public void Save()
        {
            FileController.SaveAs("itemDetails", Items);
        }
    }
}