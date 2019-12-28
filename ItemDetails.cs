using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Coflnet;
using Hypixel.NET.SkyblockApi;
using MessagePack;

namespace hypixel
{
    public class ItemDetails
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
            return Items.Keys.ToList();
        }

        public void AddOrIgnoreDetails(Auction a)
        {
            var name = ItemReferences.RemoveReforges(a.ItemName);
            if(Items.ContainsKey(name))
            {
                // already exists
                // try to get shorter lore
                if(Items[name]?.Description?.Length > a?.ItemLore?.Length && a.ItemLore.Length > 10)
                {
                    Items[name].Description = a.ItemLore;
                }
                return;
            }

            // new item, add it
            var i = new Item();
            i.Name = name;
            i.Tier = a.Tier;
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



        [MessagePackObject]
        public class Item
        {
            public static Item Default = new Item(){Name = "unknown",Description="This item has not yet been reviewed by our team"};


            [Key(0)]
            public string Name;
            [Key(1)]
            public List<string> AltNames;
            [Key(2)]
            public string Description;
            [Key(3)]
            public string IconUrl;
            [Key(4)]
            public string Category;
            [Key(5)]
            public string Extra;
            [Key(6)]
            public string Tier;
            [Key(7)]
            public string MinecraftType;
            [Key(8)]
            public string color;
        }
    }
}
