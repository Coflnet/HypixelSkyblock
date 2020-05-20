using System;
using System.Collections.Generic;
using System.Linq;
using Coflnet;
using Hypixel.NET.SkyblockApi;
using MessagePack;
using Newtonsoft.Json;
using RestSharp;

namespace hypixel
{
    public class MinecraftTypeParser
    {
        static public MinecraftTypeParser Instance;

        static HashSet<string> HeadsThatAreMinecraftItems = new HashSet<string>();

        public static Dictionary<string,Item> Items {get; private set;}
        private static Dictionary<string,Item> ItemTypes = new Dictionary<string, Item>();

        static MinecraftTypeParser()
        {
            Instance = new MinecraftTypeParser();
            if(FileController.Exists("minecraftTypes"))
            {
                LoadFromDisc();
            } else {
                DownloadItems();
            }
        }

        static void LoadFromDisc()
        {
            Items = FileController.LoadAs<Dictionary<string,Item>>("minecraftItems");
            
            BuildTypeCache();
        }

        static void BuildTypeCache()
        {
            foreach (var item in Items)
            {
                ItemTypes[item.Value.text_type] = item.Value;
                AddAlias(item.Value,"golden","gold");
                AddAlias(item.Value,"wooden","wood");
            }

            // Add aditional type aliases 
            ItemTypes["gold_spade"] = ItemTypes["golden_shovel"];
            ItemTypes["iron_spade"] = ItemTypes["iron_shovel"];
            ItemTypes["disc"] = ItemTypes["record_blocks"];
            ItemTypes["pork"] = ItemTypes["porkchop"];
            ItemTypes["command"] = ItemTypes["command_block"];
            ItemTypes["mushroom_soup"] = ItemTypes["mushroom_stew"];
            ItemTypes["seeds"] = ItemTypes["wheat_seeds"];
            ItemTypes["trap_door"] = ItemTypes["trapdoor"];
            // special items
            ItemTypes["skeleton"] = Items["Mob Head (Skeleton)"];
            ItemTypes["zombie"] = Items["Mob Head (Zombie)"];
            ItemTypes["null"] = Items["Mob Head (Human)"];



            HeadsThatAreMinecraftItems.Add("Zombie Hat");
            HeadsThatAreMinecraftItems.Add("Zombie Skull");
            HeadsThatAreMinecraftItems.Add("Skeleton Skull");
            HeadsThatAreMinecraftItems.Add("Skeleton Hat");
            HeadsThatAreMinecraftItems.Add("Zombie Talisman");
            HeadsThatAreMinecraftItems.Add("Skeleton Talisman");
            HeadsThatAreMinecraftItems.Add("null");
        }

        static void AddAlias(Item input, string oldStart, string newStart)
        {
            if(input.text_type.StartsWith(oldStart))
            {
                ItemTypes[newStart+input.text_type.Substring(oldStart.Length)] = input;
            }
        }
        

        static void DownloadItems()
        {
            var client = new RestClient("https://minecraft-ids.grahamedgecombe.com/");
            var request = new RestRequest($"items.json", Method.GET);

            //Get the response and Deserialize
            var response = client.Execute(request);

            Items = new Dictionary<string, Item>();

            foreach (var item in JsonConvert.DeserializeObject<List<Item>>(response.Content))
            {
                if(!Items.ContainsKey(item.name))
                    Items.Add(item.name,item);
            }

            FileController.SaveAs("minecraftItems",Items);
      
            BuildTypeCache();
        }

        public string Parse(Auction a)
        {
            
            var fullName = RemoveReforgesAndEnchanted(a.Extra);

            // special items first
            if(fullName.EndsWith("Skull Item") 
            && !HeadsThatAreMinecraftItems.Contains(fullName.Substring(0,fullName.Length-" Skull Item".Length)))
            {
                if(fullName.StartsWith("Skel"))
                {
                    var name = fullName.Substring(0,fullName.Length-"Skull Item".Length);
                }
                return "skull";
            }            

            var longestWithoutSkyblock = SearchFor(fullName.Substring(RemoveReforgesAndEnchanted(a.ItemName).Length));
            var withFullName = SearchFor(fullName);

            
            if(withFullName == null || longestWithoutSkyblock != null && longestWithoutSkyblock.Length > withFullName.Length)
            {
                if(longestWithoutSkyblock == null)
                {
                    // find single word match
                    foreach (var item in fullName.Split(' ').Reverse())
                    {
                        if(ItemExists(item))
                        {
                            return item;
                        }
                    }

                    Console.WriteLine($"Could not find {fullName} - {a.ItemName}" );
                    return fullName;
                }
                return longestWithoutSkyblock.Trim();
            } 

            return withFullName?.Trim();
        }

        static string RemoveReforgesAndEnchanted(string input)
        {
            var fullName = ItemReferences.RemoveReforgesAndLevel(input);

            if(fullName.StartsWith("Enchanted"))
            {
                fullName = fullName.Substring("Enchanted".Length);
            }
            return fullName;
        }

        string SearchFor(string name)
        {
            var nameTry = "";
            string longestFound = null;
            var words = name.Split(' ');
            for (int i = 0; i < words.Length; i++)
            {
                nameTry+= words[i];

                if(ItemExists(nameTry))
                {
                    longestFound = nameTry;
                }
                
                nameTry += " ";
            }
            return longestFound;
        }

        bool ItemExists(string name)
        {
            return Items.ContainsKey(name.Trim()) || ItemTypes.ContainsKey(ToPascalCase(name));
        }

        string ToPascalCase(string input)
        {
            return input.ToLower().Trim().Replace(" ", "_");
        }

        public Item GetDetails(string name)
        {
            Items.TryGetValue(name, out Item item);

            if(item == null)
            {
                ItemTypes.TryGetValue(ToPascalCase(name),out item);
            }
            return item;
        }


        [MessagePackObject]
        public class Item
        {
            [Key(0)]
            public int type;
            [Key(1)]
            public int meta;
            [Key(2)]
            public string name;
            [Key(3)]
            public string text_type;
        }
    }
}
