using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Coflnet;
using MessagePack;
using Newtonsoft.Json;
using RestSharp;

namespace hypixel
{
    class CacheItem
    {
        public int hits;
        public object value;
    }
    class StorageManager {
        static ConcurrentQueue<Action> dirty = new ConcurrentQueue<Action> ();
        static ConcurrentDictionary<string,CacheItem> cache = new ConcurrentDictionary<string, CacheItem>();

        public static int maxItemsInCache = 10000;

        static object saveLock = new object();

        public static User GetOrCreateUser (string uuid, bool cacheAll = false) {

            var cacheKey = "u"+uuid.Substring(0,4);
            lock(cacheKey)
            {
              


            if(uuid?.Length < 30)
            {
                // this is a username
                uuid = GetUuidFromPlayerName(uuid);
            }
            if(String.IsNullOrEmpty(uuid) )
            {
                throw new Exception($"User {uuid} not found");
            }
           

            

            var compactPath = "users/"+uuid.Substring(0,4);

            if(TryFromCache<Dictionary<string,User>>(cacheKey,out Dictionary<string,User> resultSet) 
                && resultSet.TryGetValue(uuid,out User result) )
            {
                return result;
            }


            if(FileController.Exists (compactPath))
            {
                var users = FileController.LoadAs<Dictionary<string,User>>(compactPath);
                SaveToCache(cacheKey,users,Save);
                if(users.TryGetValue(uuid,out result))
                {
                    Program.usersLoaded++;
                    return result;
                }
            }


            var usr = new User () { uuid = uuid };
            if(resultSet == null)
            {
                //Console.WriteLine("creating new " + compactPath);
                resultSet = new Dictionary<string, User>();
                SaveToCache<Dictionary<string,User>>(cacheKey,resultSet,Save);
            }
            resultSet.Add(uuid,usr);
            
            return usr;  
            }
            

        }

        private static void Save(Dictionary<string,User> users)
        {
            try{
                FileController.SaveAs ("users/"+users.Values.First().uuid.Substring(0,4), users);
            } catch (Exception e)
            {
                throw e;
            }
            
        }


        static bool TryFromCache<T>(string key, out T result)
        {
            if(cache.TryGetValue(key,out CacheItem value))
            {
                result = (T)value.value;
                value.hits++;
                return true;
            }

            result = default(T);
            return false;
        }

        public static string GetUuidFromPlayerName(string playerName)
        {
            //Create the request
            var client = new RestClient("https://api.mojang.com/");
            var request = new RestRequest($"users/profiles/minecraft/{playerName}", Method.GET);

            //Get the response and Deserialize
            var response = client.Execute(request);

            if (response.Content == "")
            {
                return null;
            }

            dynamic responseDeserialized = JsonConvert.DeserializeObject(response.Content);

            //Mojang stores the uuid under id so return that
            return responseDeserialized.id;
        }

        /// <summary>
        /// be careful when using this, will return minimum 20k auctions
        /// </summary>
        /// <returns></returns>
        public static IEnumerable<SaveAuction> GetAllAuctions()
        {
            return GetFileContents<SaveAuction>("auctions");
        }

        public static IEnumerable<T> GetFileContents<T>(string path)
        {
            foreach (var item in FileController.FileNames("*",path))
            {
                var compactPath = Path.Combine(path,item);
                if(FileController.Exists (compactPath))
                {
                    foreach (var auction in FileController.ReadLinesAs<T> (compactPath))
                    {
                        yield return auction;
                    }
                }
            }
        }


        public static IEnumerable<SaveAuction> GetAuctionsWith(string itemName)
        {
            foreach (var item in FileController.FileNames( "*","items"))
            {
                if(item.ToLower() == itemName.ToLower())
                {
                    var itemsAuctions = GetOrCreateItemRef(item,true)?.auctionIds;
                    if(itemsAuctions == null)
                        continue;
                        
                    foreach (var auctionId in itemsAuctions)
                    {
                        yield return GetOrCreateAuction(auctionId,null,true);
                    }
                }
              
                
            }
        }

        public static Task Save (int removeUntil = 0) {
                
            return Task.Run(()=>{
                // save dirty
                while(dirty.TryDequeue(out Action a) && dirty.Count >= removeUntil)
                {
                    // also clear the cache
                    a?.Invoke();
                }
            });
        }

        static T LoadAsLock<T> (string path,string lockString = "saveLock")
        {
            lock(lockString)
            {
                try
                {
                return FileController.LoadAs<T>(path);
                } catch(Exception)
                {
                    Console.WriteLine("Loading error");
                    Console.WriteLine(path);
                    throw;
                }
            }
        }

        public static SaveAuction GetOrCreateAuction(string uuid,SaveAuction input = null,bool noWrite = false)
        {
            if(TryFromCache<SaveAuction>(uuid, out SaveAuction result))
            {
                if(input !=null)
                {
                    var c = new CacheItem(){value=input};
                    cache.AddOrUpdate(uuid,c,(id,old)=>c);
                }
                return result;
            }


            var compactPath = "auctions/"+uuid.Substring(0,4);
            if(input == null && FileController.Exists (compactPath))
            {
                foreach (var auction in FileController.ReadLinesAs<SaveAuction> (compactPath))
                {
                    if(auction.Uuid == uuid)
                    {
                        if(noWrite)
                            SaveToCache<SaveAuction>(uuid,auction,s=>{});
                        else
                            SaveToCache<SaveAuction>(uuid,auction,Save);
                        return auction;
                    }
                }
            }

            // not found
            var a = new SaveAuction () { Uuid = uuid };
            if(input != null)
            {
                a=input;
            }
            SaveToCache<SaveAuction>(uuid,a,Save);                     
            return a;
            
        }

        public static void SaveToCache<T>(string key, T obj, Action<T> save)
        {
            if(dirty.Count > maxItemsInCache)
            {
                // save half
                Save(maxItemsInCache/2);
            }

            cache.AddOrUpdate(key,new CacheItem(){value=obj},(sk,ob)=>ob);
            dirty.Enqueue(()=>{

                var lockKey = key;
                if(key.Length > 4)
                {
                    lockKey = key.Substring(0,4);
                }
                lock(lockKey)
                {
                    cache.TryRemove(key,out CacheItem value);
                    try{
                        if(value == null)
                        {
                            // got remove in the meantime
                            return;
                        }

                    save((T)value.value);
                    } catch(Exception e)
                    {
                        Console.WriteLine($"Failed to save: {key} {e.Message}");
                    }
                }
            });
        }



        public static ItemReferences GetOrCreateItemRef(string name, bool noWrite = false)
        {
            name = ItemReferences.RemoveReforges(name);

            if(TryFromCache<ItemReferences>(name, out ItemReferences result))
            {
                return result;
            }

            var path = "items/"+name;
            if (FileController.Exists (path)) {
                        try{
                        var items = LoadAsLock<ItemReferences> (path,name);
                        if(noWrite)
                            SaveToCache<ItemReferences>(name,items,s=>{});
                        else
                            SaveToCache<ItemReferences>(name,items,Save);
                        return items;
                    } catch(Exception)
                    {
                        // move it 
                        Console.WriteLine(name);
                        Console.WriteLine(name);
                        throw;
                    }
                
                
                
            } else {
                var items = new ItemReferences () { Name = name };
                SaveToCache<ItemReferences>(name,items,Save);
                return items;
            }
        }

        public static void Migrate()
        {
           // var db = new SQLiteConnection(Path.Combine(FileController.dataPaht,"auctions.db"));
           // db.CreateTable<SaveAuction>();

            var i = 0;
            var files = 0;

            foreach (var item in FileController.FileNames( "*","auctions"))
            {
                var compactPath = Path.Combine("auctions/"+ item);
                foreach (var auction in FileController.ReadLinesAs<SaveAuction> (compactPath))
                {
                    GetOrCreateItemRef(auction.ItemName).auctionIds.Add(auction.Uuid);
                    i++;
                }

                files++;
                
                Console.Write($"\r{i}");
            }

            Save();
        }

        public static void Save(User user)
        {
            FileController.ReplaceLine<User> ("users/"+user.uuid.Substring(0,4),(a)=>a.uuid == user.uuid, user);
        }

        public static void Save(SaveAuction auction)
        {
            FileController.ReplaceLine<SaveAuction> ("auctions/"+auction.Uuid.Substring(0,4),(a)=>a.Uuid == auction.Uuid, auction);
        }

        public static void Save(ItemReferences item)
        {
            if(item == null)
            {
                // silent
                return;
            }
            FileController.SaveAs ("items/"+item.Name, item);
        }
    }

    [MessagePackObject]
    public class ItemReferences
    {
        private static HashSet<string> reforges = new HashSet<string>(){
            "Demonic",
            "Forceful",
            "Gentle",
            "Godly,",
            "Hurtful",
            "Keen",
            "Strong",
            "Superior",
            "Unpleasant",
            "Zealous",
            "Odd",
            "Rich",
            "EntryPointNotFoundException",
            "FileParameter",
            "FlagsAttribute",
            "Heroic",
            "Legendary",
            "ConsoleSpecialKey",
            "Deadly",
            "NotFiniteNumberException",
            "Grand",
            "Hasty",
            "Neat",
            "Papid",
            "Unreal",
            "Clean",
            "FileController",
            "Heavy",
            "Light",
            "Mythic",
            "Pure",
            "Smart",
            "TypeInitializationException",
            "WriteState",
            "Very",
            "Highly",
            "Bizzare",
            "Itchy",
            "Omnious",
            "Pleasant",
            "Pretty",
            "Shiny",
            "Simple",
            "Strange",
            "Vivid",
            "Titanic"
        };


        [Key(0)]
        public string Name;

        [Key(1)]
        public ConcurrentBag<string> auctionIds = new ConcurrentBag<string>();


        public static string RemoveReforges(string fullItemName)
        {
            if(reforges.Contains(fullItemName.Split(' ')[0]))
            { 
                int i = fullItemName.IndexOf(" ")+1;
                return fullItemName.Substring(i);
            }

            return fullItemName;
        }
    }
}