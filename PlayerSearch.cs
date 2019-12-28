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

        public static void ClearCache()
        {
            players.Clear();
        }

        /// <summary>
        /// Registers that a specific player was looked up and modifies the search order acordingly
        /// </summary>
        /// <param name="name"></param>
        public void AddHitFor(PlayerResult player)
        {
            var name = player.Name;
            Console.WriteLine($"Adding hit for {name}");
            var key = name.Substring(0,2).ToLower();
            if(!players.TryGetValue(key,out HashSet<PlayerResult> resultSet))
            {
                resultSet = new HashSet<PlayerResult>();
                players.Add(key,resultSet);
            }
            // just increment the hit counter if he is in the set
            foreach (var item in resultSet)
            {
                if(item.Name == name)
                {
                    item.AuctionCount++;
                    return;
                }
            }

            // we have to throw someone out of the set
            if(resultSet.Count > 10)
            {
                resultSet.Remove(resultSet.Where(e=> e.AuctionCount == resultSet.Min(p=>p.AuctionCount)).First());
            }

            resultSet.Add(player);
        }

        public HashSet<PlayerResult> GetPlayers(string start)
        {
            int length = start.Length;
            if(length < 2)
            {
                throw new ValidationException("The search term has to be 2 characters or longer");
            }
            
            // enable 2 long
            if(length > 3)
            {
                length = 3;

                // 4 and more are already pretty accurate, add a hit
            }
            
            var startOfName = start.Substring(0,length).ToLower();
            
            var result = FromCacheOrFile(startOfName);

            if(start.Length > 3)
            {
                // Add all of these names to cache
                foreach (var item in result)
                {
                    AddHitFor(item);
                }
            }
            return result;

        }

        private HashSet<PlayerResult> FromCacheOrFile(string startOfName)
        {
            var path = "players/"+startOfName;
            if(players.TryGetValue(startOfName,out HashSet<PlayerResult> result))
            {
                return result;
            } else if(FileController.Exists(path))
            {
                // maybe in the file
                result = FileController.LoadAs<HashSet<PlayerResult>>(path);
                // is the cache to large?
                if(players.Count > StorageManager.maxItemsInCache / 10)
                {
                    players.Remove(players.Keys.First());
                }
                // cache
                players[startOfName] = result;
                return result;
                
            } else {
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
