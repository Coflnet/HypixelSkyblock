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

            foreach (var item in Items)
            {
                if(item.Value == null || item.Value.IconUrl == null)
                {
                    continue;
                }

                item.Value.IconUrl = item.Value.IconUrl.Replace("skyblock-backend.coflnet.com/static/skin","mc-heads.net/head");
                if(item.Value.IconUrl.EndsWith("/50"))
                {
                    item.Value.IconUrl = item.Value.IconUrl.Replace("/50","");
                }
                
                if(!item.Value.IconUrl.EndsWith("/50") && item.Value.IconUrl.StartsWith("https://mc"))
                {
                    item.Value.IconUrl += "/50";
                }
                
            }
        }

    

        public List<string> AllItemNames()
        {
            var result = new List<string>();
            foreach (var item in Items.Values)
            {
                if(item == null || item.AltNames == null)
                {
                    continue;
                }
                result.AddRange(item.AltNames);
            } 
            return result;
        }

        public void AddOrIgnoreDetails(Auction a)
        {
            var id = NBT.ItemID(a.ItemBytes);
            string Tier = null;
            if(id=="PET")
            {
                var nbt = new NbtData(a.ItemBytes);
                var tag = nbt.Root().Get<NbtString>("petInfo");
                PetInfo info = JsonConvert.DeserializeObject<PetInfo>(tag.StringValue);
                Tier=info.Tier;
                var petType = info.Type;
                id+=$"_{petType}_{Tier}";
                var view = nbt.Data;
            }
            if(Items.ContainsKey(id))
            {
                // already exists
                // try to get shorter lore
                if(Items[id]?.Description?.Length > a?.ItemLore?.Length && a.ItemLore.Length > 10)
                {
                    Items[id].Description = a.ItemLore;
                }
                return;
            }
            // legacy item names
            var name = ItemReferences.RemoveReforges(a.ItemName);
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
            var name = ItemReferences.RemoveReforges(fullName);
            Console.WriteLine("Getting "+ fullName);
            if(Items.TryGetValue(name,out Item value))
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
