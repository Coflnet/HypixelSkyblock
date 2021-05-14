using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Coflnet;
using fNbt;
using Hypixel.NET.SkyblockApi;
using Newtonsoft.Json;
using dev;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

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
            using (var context = new HypixelContext())
            {
                var items = context.Items.Where(item => item.Description == null);
                foreach (var item in items)
                {
                    ToFillDetails.TryAdd(item.Tag, item);
                }
                TagLookup = context.Items.Where(item => item.Tag != null).Select(item => new { item.Tag, item.Id })
                    .ToDictionary(item => item.Tag, item => item.Id);
            }
        }

        public void Load()
        {
            try 
            {
                if (FileController.Exists("itemDetails"))
                    Items = FileController.LoadAs<Dictionary<string, Item>>("itemDetails");
            } catch(Exception)
            {
                FileController.Move("itemDetails","corruptedItemDetails"+DateTime.Now.Ticks);
            }

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
            if (name == null)
            {
                return "NULL";
            }
            var normalizedName = ItemReferences.RemoveReforgesAndLevel(name);
            return ReverseNames.GetValueOrDefault(normalizedName, normalizedName);
        }

        /// <summary>
        /// Fast access to an item id for index lookup
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public int GetItemIdForName(string name, bool forceGet = true)
        {
            var tag = GetIdForName(name);
            if (!TagLookup.TryGetValue(tag, out int value) && forceGet)
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
            if (id == null)
            {
                if (a.ItemName == "Revive Stone")
                {
                    // known item, has no tag, nothing to do
                    return;
                }
                Logger.Instance.Error($"item has no tag {JsonConvert.SerializeObject(a)}");
                return;
            }

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

            System.Threading.Tasks.Task.Run(async () =>
            {
                if (existingItem == null)
                    AddItemToDB(newItem);
                else
                    await UpdateItem(existingItem, newItem);
            });
        }

        private async Task UpdateItem(DBItem existingItem, DBItem newItem)
        {
            await Task.Delay(5000);
            Console.WriteLine("updating item");
            using (var context = new HypixelContext())
            {
                newItem.Id = existingItem.Id;
                context.Items.Update(newItem);
                await context.SaveChangesAsync();
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
                i.IconUrl = $"https://sky.coflnet.com/static/{t?.type}-{t?.meta}.png";
            }
        }

        private int AddItemToDB(DBItem item)
        {
            using (var context = new HypixelContext())
            {
                // make sure it doesn't exist
                if (!context.Items.Where(i => i.Tag == item.Tag).Any())
                    context.Items.Add(item);
                try
                {
                    context.SaveChanges();
                }
                catch (Exception)
                {
                    Console.WriteLine($"Ran into an error while saving {JsonConvert.SerializeObject(item)}");
                    throw;
                }
                return item.Id;
            }
        }

        private ConcurrentDictionary<int, int> itemHits = new ConcurrentDictionary<int, int>();

        public void AddHitFor(string tag)
        {
            if (TagLookup.TryGetValue(tag, out int id))
                itemHits.AddOrUpdate(id, 1, (key, value) => value + 1);
        }

        public void SaveHits(HypixelContext context)
        {
            var hits = itemHits;
            itemHits = new ConcurrentDictionary<int, int>();
            foreach (var hit in hits)
            {
                var item = context.Items.Where(item => item.Id == hit.Key).First();
                item.HitCount += hit.Value;
                context.Update(item);
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
            var cleanedName = ItemReferences.RemoveReforgesAndLevel(fullName);
            /*if (ReverseNames.TryGetValue(name, out string key) &&
                Items.TryGetValue(key, out Item value))
            {
                return value;
            }*/

            using (var context = new HypixelContext())
            {
                var id = GetItemIdForName(cleanedName, false);
                if (id == 0)
                    id = context.AltItemNames.Where(name => name.Name == fullName || name.Name == cleanedName)
                       .Select(name => name.DBItemId).FirstOrDefault();

                if (id > 1)
                {
                    var item = context.Items.Include(i=>i.Names).Where(i => i.Id == id).First();
                    item.Name = fullName;
                    // cooler icons 
                    //if (!item.Tag.StartsWith("POTION") && !item.Tag.StartsWith("PET") && !item.Tag.StartsWith("RUNE"))
                    //    item.IconUrl = "https://sky.lea.moe/item/" + item.Tag;
                    return item;
                }
            }

            return new DBItem() { Tag = "Unknown", Name = fullName };
        }

        public DBItem GetDetailsWithCache(string uuid)
        {
            if(CacheService.Instance.GetFromCache("itemDetails",uuid, out string json))
                return JsonConvert.DeserializeObject<DBItem>(json);
            
            var response = ItemDetailsCommand.CreateResponse(uuid);
            CacheService.Instance.Save("itemDetails",uuid,response);
            return JsonConvert.DeserializeObject<DBItem>(response.Data);
        }

        public DBItem GetDetailsWithCache(int id)
        {
            // THIS IS INPERFORMANT, Todo: find a better way
            var itemTag = TagLookup.Where(a=>a.Value == id).First().Key;
            return GetDetailsWithCache(itemTag);
        }

        public void Save()
        {
            FileController.SaveAs("itemDetails", Items);
        }
    }
}