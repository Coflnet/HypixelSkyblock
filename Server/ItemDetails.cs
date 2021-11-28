using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Coflnet;
using Newtonsoft.Json;
using dev;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

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

        }

        public Task LoadFromDB()
        {
            return LoadLookup();
        }

        public async Task LoadLookup()
        {
            using (var context = new HypixelContext())
            {
                TagLookup = await context.Items.Where(item => item.Tag != null).Select(item => new { item.Tag, item.Id })
                                    .ToDictionaryAsync(item => item.Tag, item => item.Id);
            }
        }

        public void Load()
        {
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
        /// <param name="forceGet">throw an exception if the lookup wasn't found</param>
        /// <returns></returns>
        public int GetItemIdForName(string name, bool forceGet = true)
        {
            if(name.StartsWith("PET_SKIN_"))
                name = name.Replace("PET_SKIN_","");
            if (TagLookup.TryGetValue(name, out int value))
                return value;

            // fall back to the db
            using (var context = new HypixelContext())
            {
                var id = context.Items.Where(i => i.Tag == name).Select(i => i.Id).FirstOrDefault();
                if (id == 0 && forceGet)
                    throw new CoflnetException("item_not_found", $"could not find the item with the tag `{name}`");
                if (id != 0)
                    TagLookup[name] = id;
                return id;
            }
        }

        public IEnumerable<string> AllItemNames()
        {
            return ReverseNames.Keys;
        }

        public int AddItemToDB(DBItem item)
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
                LoadFromDB();
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
                    var item = context.Items.Include(i => i.Names).Where(i => i.Id == id).First();
                    if (item.Names != null)
                        item.Names = item.Names.OrderBy(n => GetScoreFor(n)).ToList();
                    item.Name = fullName;
                    return item;
                }
            }

            return new DBItem() { Tag = "Unknown", Name = fullName };
        }

        private static int GetScoreFor(AlternativeName n)
        {
            if (n == null || n.Name == null || n.Name == "Pet")
                return 100;
            return n.Name.Length + (Regex.IsMatch(n.Name, "^[a-zA-Z0-9 ]*$") ? 0 : 40) - n.OccuredTimes;
        }

        public async Task<DBItem> GetDetailsWithCache(string itemTag)
        {
            var details = await CoreServer.ExecuteCommandWithCache<string, DBItem>("itemDetails", itemTag);
            if (details.Tag == null)
                Console.WriteLine("got default");
            return details;
        }

        public DBItem GetDetailsWithCache(int id)
        {
            // THIS IS INPERFORMANT, Todo: find a better way
            var key = TagLookup.Where(a => a.Value == id).FirstOrDefault();
            string itemTag;
            if (key.Value != 0)
                itemTag = key.Key;
            else
            {
                using (var context = new HypixelContext())
                {
                    var dbResult = context.Items.Where(i => i.Id == id).FirstOrDefault();
                    if (dbResult == null)
                        return new DBItem();
                    itemTag = dbResult.Tag;
                }
            }
            return GetDetailsWithCache(itemTag).GetAwaiter().GetResult();
        }

        public void Save()
        {
            FileController.SaveAs("itemDetails", Items);
        }
    }
}