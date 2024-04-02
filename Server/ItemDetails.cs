using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using dev;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace Coflnet.Sky.Core
{
    public partial class ItemDetails
    {
        public static ItemDetails Instance;

        public ConcurrentDictionary<string, Item> Items = new ConcurrentDictionary<string, Item>();
        /// <summary>
        /// Contains the Tags indexed by name [Name]=Tag
        /// </summary>
        /// <returns></returns>
        public ConcurrentDictionary<string, string> ReverseNames = new ConcurrentDictionary<string, string>();
        /// <summary>
        /// Contains a cache for <see cref="DBItem.Tag"/> to <see cref="DBItem.Id"/>
        /// </summary>
        /// <returns></returns>
        public ConcurrentDictionary<string, int> TagLookup = new ConcurrentDictionary<string, int>();

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
            try
            {
                var client = new Items.Client.Api.ItemsApi(SimplerConfig.SConfig.Instance["ITEMS_BASE_URL"]);
                var ids = await client.ItemsIdsGetWithHttpInfoAsync() ?? throw new Exception("no items found");
                TagLookup = new(ids.Data);
                Logger.Instance.Info("loaded item tag lookup " + TagLookup.Count + " items");
            }
            catch (Exception e)
            {
                Logger.Instance.Error(e, "trying to load itemid lookup from service " + SimplerConfig.SConfig.Instance["ITEMS_BASE_URL"]);
                using (var context = new HypixelContext())
                {
                    TagLookup = new(await context.Items.Where(item => item.Tag != null).Select(item => new { item.Tag, item.Id })
                                        .ToDictionaryAsync(item => item.Tag, item => item.Id));
                }
            }
        }

        public void Load()
        {
            if (Items == null)
            {
                Items = new();
            }
            // correct keys
            foreach (var item in Items.Keys.ToList())
            {
                if (Items[item].Id != item)
                {
                    Items.TryAdd(Items[item].Id, Items[item]);
                    Items.Remove(item, out _);
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
        /// <param name="tag"></param>
        /// <param name="forceGet">throw an exception if the lookup wasn't found</param>
        /// <returns></returns>
        public int GetItemIdForTag(string tag, bool forceGet = true)
        {
            if (string.IsNullOrEmpty(tag))
                return 0;
            if (TagLookup == null || TagLookup.Count == 0)
                LoadLookup().GetAwaiter().GetResult();
            if(tag.StartsWith("RUNE_") && TagLookup.ContainsKey("UNIQUE_" + tag))
            {
                // extend to unique rune as that prefix is not present on the rune item
                tag = "UNIQUE_" + tag;
            }
            if (TagLookup.TryGetValue(tag, out int value))
                return value;

            try
            {
                var client = new Items.Client.Api.ItemsApi(SimplerConfig.SConfig.Instance["ITEMS_BASE_URL"]);
                var response = client.ItemsSearchTermIdGetWithHttpInfo(tag);
                if (response.StatusCode != System.Net.HttpStatusCode.OK)
                    throw new Exception("response from items service " + response.StatusCode + " " + response.RawContent.Truncate(100));
                var id = response.Data;
                if (id == 0 && forceGet)
                {
                    throw new CoflnetException("item_not_found", $"could not find the item with the tag `{tag}`");
                }
                if (id != 0)
                    TagLookup[tag] = id;
                return id;
            }
            catch (Exception e)
            {
                // fall back to the db
                Logger.Instance.Error(e, "loading itemid from service for " + tag);
                using var context = new HypixelContext();
                var id = context.Items.Where(i => i.Tag == tag).Select(i => i.Id).FirstOrDefault();
                if (id == 0 && forceGet)
                    throw new CoflnetException("item_not_found", $"could not find the item with the tag `{tag}`");
                return id;
            }
        }

        public IEnumerable<string> AllItemNames()
        {
            return ReverseNames.Keys;
        }

        public int AddItemToDB(DBItem item)
        {
            throw new Exception("items are handled by the items service now");
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
                var id = GetItemIdForTag(cleanedName, false);
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
            if (fullName.ToUpper() == fullName)
            {
                // looks like actually a tag
                return new DBItem() { Tag = fullName, Name = fullName };
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
            return await CoreServer.ExecuteCommandWithCache<string, DBItem>("itemDetails", itemTag);
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
            //FileController.SaveAs("itemDetails", Items);
        }
    }
}