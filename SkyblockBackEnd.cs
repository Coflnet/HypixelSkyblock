using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Coflnet;
using Hypixel.NET.SkyblockApi;
using MessagePack;
using Newtonsoft.Json;
using RestSharp;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace hypixel
{
    public class SkyblockBackEnd : WebSocketBehavior
    {
        public static Dictionary<string,Command> Commands = new Dictionary<string, Command>();

        static SkyblockBackEnd()
        {
            Commands.Add("search",new SearchCommand());
            Commands.Add("itemPrices",new ItemPricesCommand());
            Commands.Add("playerDetails",new PlayerDetailsCommand());
            Commands.Add("version",new GetVersionCommand());
            Commands.Add("auctionDetails",new AuctionDetails());
            Commands.Add("itemDetails",new ItemDetailsCommand());
            Commands.Add("clearCache",new ClearCacheCommand());

            

            
        }

        protected override void OnMessage (MessageEventArgs e)
        {
            long mId = 0;
            try{
                var data = MessagePackSerializer.Deserialize<MessageData>(MessagePackSerializer.FromJson(e.Data));
                mId = data.mId;
                data.Connection = this;
                 data.Data = System.Text.Encoding.UTF8.GetString(System.Convert.FromBase64String(data.Data));
                 Console.WriteLine(data.Data);

                if(!Commands.ContainsKey(data.Type))
                {
                    data.SendBack(new MessageData("error",$"The command `{data.Type}` is Unkown, please check your spelling"));
                    return;
                }
                Commands[data.Type].Execute(data);
            } catch(CoflnetException ex)
            {
                SendBack(new MessageData(ex.Slug,ex.Message){mId =mId});
            }catch (Exception ex)
            {
                Console.WriteLine(ex.StackTrace);
                SendBack(new MessageData("error","The Message has to follow the format {\"type\":\"SomeType\",\"data\":\"\"}"){mId =mId});
               
                throw ex;
            }
            
            
        }

        public void SendBack(MessageData data)
        {
            Send(MessagePackSerializer.ToJson(data));
        }
    }

    public class ItemDetailsCommand : Command
    {
        public override void Execute(MessageData data)
        {
            Regex rgx = new Regex("[^a-zA-Z -]");
            var search = rgx.Replace(data.Data, "");
            data.SendBack(new MessageData("itemDetailsResponse",JsonConvert.SerializeObject(ItemDetails.Instance.GetDetails(search))));
        }
    }


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
                    i.IconUrl = $"https://skyblock-backend.coflnet.com/static/skin/{Path.GetFileName(NBT.SkullUrl(a.ItemBytes))}" ;
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
            var fullName = ItemReferences.RemoveReforges(input);

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
