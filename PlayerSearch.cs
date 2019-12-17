using System;
using System.Collections.Generic;
using System.Linq;
using Coflnet;
using MessagePack;
using WebSocketSharp;

namespace hypixel
{
    public class PlayerSearch
    {
        public static PlayerSearch Instance;

        public static Dictionary<string,HashSet<PlayerResult>> players = new Dictionary<string, HashSet<PlayerResult>>();

        static PlayerSearch()
        {
            Instance = new PlayerSearch();
            FileController.CreatePath("players/");
        }

        public HashSet<PlayerResult> GetPlayers(string start)
        {
            if(start.Length < 3)
            {
                throw new ValidationException("The search term has to be 3 characters or longer");
            }
            if(start == "ekw")
                {
                    Console.WriteLine("Searching file");
                }

            var firstThree = start.Substring(0,3).ToLower();
            var path = "players/"+firstThree;
            if(players.TryGetValue(firstThree,out HashSet<PlayerResult> result))
            {
                return result;
            } else if(FileController.Exists(path))
            {
                if(firstThree == "ekw")
                {
                    Console.WriteLine("Reading file");
                }


                // maybe in the file
                result = FileController.LoadAs<HashSet<PlayerResult>>(path);
                // is the cache to large?
                if(players.Count > StorageManager.maxItemsInCache / 10)
                {
                    players.Remove(players.Keys.First());
                }
                if(firstThree == "ekw")
                {
                    Console.WriteLine(MessagePackSerializer.ToJson(result));
                }
                // cache
                players[firstThree] = result;
                return result;
                
            } else {
                 if(start == "ekw")
                {
                    Console.WriteLine("nothing found");
                }
                return new HashSet<PlayerResult>();
            }
        }

        public void SaveNameForPlayer(string name, string uuid)
        {
            //Console.WriteLine($"Saving {name} ({uuid})");
            var index = name.Substring(0,3).ToLower();
            string path = "players/"+index;
            lock(path)
            {
                HashSet<PlayerResult> list = null;
                if(FileController.Exists(path))
                    list = FileController.LoadAs<HashSet<PlayerResult>>(path);
                if(list == null)
                    list = new HashSet<PlayerResult>();
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
