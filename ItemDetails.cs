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

        public Dictionary<string,Item> Items = new Dictionary<string, Item>();

        public Dictionary<string,string> ReverseNames = new Dictionary<string, string>();

        static ItemDetails ()
        {
            Instance = new ItemDetails();
            Instance.Load();
        }


        public void Load()
        {
            if(FileController.Exists("itemDetails"))
                Items = FileController.LoadAs<Dictionary<string,Item>>("itemDetails");

            if(Items == null)
            {
                Items = new Dictionary<string, Item>();
            }
            // correct keys
            foreach (var item in Items.Keys.ToList())
            {
                if(Items[item].Id != item)
                {
                    Items.TryAdd(Items[item].Id,Items[item]);
                    Items.Remove(item);
                }
            }

            foreach (var item in Items)
            {
                if(item.Value.AltNames == null)
                    continue;
                item.Value.AltNames = item.Value.AltNames?.Select(s=>ItemReferences.RemoveReforgesAndLevel(s)).ToHashSet();

                foreach (var name in item.Value.AltNames)
                {
                    if(!ReverseNames.TryAdd(name,item.Key))
                    {
                        // make a good guess, anyone?
                    }
                }
            }
        }


        public string GetIdForName(string name)
        {
            var normalizedName = ItemReferences.RemoveReforgesAndLevel(name);
            return ReverseNames.GetValueOrDefault(normalizedName,normalizedName);
        }
    

        public IEnumerable<string> AllItemNames()
        {
            return ReverseNames.Keys;
        }

        public void AddOrIgnoreDetails(Auction a)
        {
            var id = NBT.ItemID(a.ItemBytes);
            string Tier = null;
            


            var name = ItemReferences.RemoveReforgesAndLevel(a.ItemName);
            if(Items.ContainsKey(id))
            {
                var tragetItem = Items[id];
                if(tragetItem.AltNames == null)
                    tragetItem.AltNames = new HashSet<string>();

                // try to get shorter lore
                if(Items[id]?.Description?.Length > a?.ItemLore?.Length && a.ItemLore.Length > 10)
                {
                    Items[id].Description = a.ItemLore;
                }
                tragetItem.AltNames.Add(name);
                return;
            }
            // legacy item names
            if(Items.ContainsKey(name))
            {
                var item = Items[name];
                item.Id = id;
                if(item.AltNames == null)
                {
                    item.AltNames = new HashSet<string>();
                }
                item.AltNames.Add(name);
                Items[id] = item;
                Items.Remove(name);

                return;
            }

            // new item, add it
            AddNewItem(a,name,id,Tier);
        }

        private void AddNewItem(Auction a, string name, string id, string Tier)
        {
            var i = new Item();
            i.Id = id;
            i.AltNames = new HashSet<string>(){name};
            i.Tier = Tier?? a.Tier;
            i.Category = a.Category;
            i.Description = a.ItemLore;
            i.Extra = a.Extra;
            i.MinecraftType = MinecraftTypeParser.Instance.Parse(a);

            //Console.WriteLine($"New: {name} ({i.MinecraftType})" );
            if(i.MinecraftType == "skull" )
            {
                //Console.WriteLine("Parsing bytes");
                //Console.WriteLine(name);
                try{
                    i.IconUrl = $"https://mc-heads.net/head/{Path.GetFileName(NBT.SkullUrl(a.ItemBytes))}/50" ;
                } catch(Exception e)
                {
                    Console.WriteLine($"Error :O {i.Extra}\n {e.Message} \n {e.StackTrace}");
                }
               // Console.WriteLine(i.IconUrl);
            } else {
                var t = MinecraftTypeParser.Instance.GetDetails(i.MinecraftType);
                i.IconUrl = $"https://skyblock-backend.coflnet.com/static/{t?.type}-{t?.meta}.png";
            }

            Items[name] = i;
        }

        /// <summary>
        /// Tries to find and return an item by name
        /// </summary>
        /// <param name="fullName">Full Item name</param>
        /// <returns></returns>
        public Item GetDetails(string fullName)
        {
            if(Items== null)
                Load();
            var name = ItemReferences.RemoveReforgesAndLevel(fullName);
            if(ReverseNames.TryGetValue(name,out string key) 
            && Items.TryGetValue(key,out Item value))
            {
                return value;
            }

            return Item.Default;
        }

        public void Save()
        {
            FileController.SaveAs("itemDetails",Items);
        }
    }
}
