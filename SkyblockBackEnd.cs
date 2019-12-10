using System;
using System.Collections.Generic;
using System.Linq;
using Coflnet;
using MessagePack;
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
            Commands.Add("itemDetails",new ItemDetailsCommand());
        }

        protected override void OnMessage (MessageEventArgs e)
        {
            try{
                var data = MessagePackSerializer.Deserialize<MessageData>(MessagePackSerializer.FromJson(e.Data));
                data.Connection = this;
                 data.Data = System.Text.Encoding.UTF8.GetString(System.Convert.FromBase64String(data.Data));
                 Console.WriteLine(data.Data);

                if(!Commands.ContainsKey(data.Type))
                {
                    SendBack(new MessageData("error",$"The command `{data.Type}` is Unkown, please check your spelling"));
                }
                Commands[data.Type].Execute(data);
            } catch(CoflnetException ex)
            {
                SendBack(new MessageData(ex.Slug,ex.Message));
            }catch (Exception ex)
            {
                SendBack(new MessageData("error","The Message has to follow the format {\"type\":\"SomeType\",\"data\":\"\"}"));
               
                throw ex;
            }
            
            
        }

        public void SendBack(MessageData data)
        {
            Send(MessagePackSerializer.ToJson(data));
        }
    }

    public abstract class Command
    {
        public abstract void Execute(MessageData data);
    }

    public class SearchCommand : Command
    {
        public override void Execute(MessageData data)
        {
            var result = PlayerSearch.Instance.GetPlayers(data.Data).ToList();
            data.SendBack(MessageData.Create("searchResponse",result));
        }
    }

    public class ItemDetailsCommand : Command
    {
        public override void Execute(MessageData data)
        {
            SearchDetails details;
            try{
                details = data.GetAs<SearchDetails>();
            } catch(Exception e)
            {
                throw new ValidationException("Format not valid for itemDetails, please see the docs");
            }

            if(details.End == default(DateTime))
            {
                details.End = DateTime.Now;
            }
            Console.WriteLine($"Start: {details.Start} End: {details.End}");

            var result = Program.AuctionsForItem(details.name)
                .Where(item=>item.HighestBidAmount > 0 && item.End < details.End && item.End > details.Start)
                .Select(item=>new Result(){
                    End = item.End,
                    Price = item.HighestBidAmount
                }).ToList();
            data.SendBack (MessageData.Create("item",result));
        }

        [MessagePackObject]
        public class Result
        {
            [Key("end")]
            public DateTime End;
            [Key("price")]
            public long Price;
        }

        public class ItemPricesManager
        {
            public static ItemPricesManager Instance;

            static ItemPricesManager()
            {
                Instance = new ItemPricesManager();
            }

            public void CalculateByHour(string name)
            {
                Program.AuctionsForItem(name);
            }
        }

        [MessagePackObject]
        public class SearchDetails
        {
            [Key("name")]
            public string name;
            [Key("start")]
            public long StartTimeStamp
            {
                set{
                    Start = value.ThisIsNowATimeStamp();
                }
                get {
                    return Start.ToUnix();
                }
            }

            [IgnoreMember]
            public DateTime Start;

            [Key("end")]
            public long EndTimeStamp
            {
                set{
                    if(value == 0)
                    {
                        End = DateTime.Now;
                    } else
                        End = value.ThisIsNowATimeStamp();
                }
                get 
                {
                    return End.ToUnix();
                }
            }

            [IgnoreMember]
            public DateTime End;
        }
    }

    public class PlayerSearch
    {
        public static PlayerSearch Instance;

        public static Dictionary<string,SortedSet<PlayerResult>> players = new Dictionary<string, SortedSet<PlayerResult>>();

        static PlayerSearch()
        {
            Instance = new PlayerSearch();
            FileController.CreatePath("players/");
        }

        public SortedSet<PlayerResult> GetPlayers(string start)
        {
            if(start.Length < 3)
            {
                throw new ValidationException("The search term has to be 3 characters or longer");
            }

            var firstThree = start.Substring(0,3).ToLower();
            var path = "players/"+firstThree;
            if(players.TryGetValue(firstThree,out SortedSet<PlayerResult> result))
            {
                return result;
            } else if(FileController.Exists(path))
            {
                // maybe in the file
                result = FileController.LoadAs<SortedSet<PlayerResult>>(path);
                // is the cache to large?
                if(players.Count > StorageManager.maxItemsInCache / 10)
                {
                    players.Remove(players.Keys.First());
                }
                // cache
                players[firstThree] = result;
                return result;
                
            } else {
                return new SortedSet<PlayerResult>();
            }
        }

        public void SaveNameForPlayer(string name, string uuid)
        {
            Console.WriteLine($"Saving {name} ({uuid})");
            var index = name.Substring(0,3).ToLower();
            string path = "players/"+index;
            lock(path)
            {
                SortedSet<PlayerResult> list= new SortedSet<PlayerResult>();
                if(FileController.Exists(path))
                     list = FileController.LoadAs<SortedSet<PlayerResult>>(path);
                list.Add(new PlayerResult(name,uuid));
                FileController.SaveAs(path,list);
            }
        }

        private static int loadedNames = 0;

        public void LoadName(User user)
        {
            if(!user.Name.IsNullOrEmpty() && user.Name.Length > 2)
            {
                // already loaded
                return;
            }

            var name = Program.GetPlayerNameFromUuid(user.uuid);

            if(name.IsNullOrEmpty())
            {
                return;
            }

            user.Name = name;
            SaveNameForPlayer(name,user.uuid);
        }
    }
}
