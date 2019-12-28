using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Coflnet;
using Newtonsoft.Json;
using RestSharp;

namespace hypixel
{
    class CacheItem
    {
        public int hits;
        public object value;

        public Action<CacheItem> Save;
    }
    class StorageManager {
        static ConcurrentDictionary<string,CacheItem> cache = new ConcurrentDictionary<string, CacheItem>();

        static int savedOnDisc = 0;

        public static int maxItemsInCache = 1000;


        static bool StopPurging = false;


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
                Dictionary<string,User> users;
                try{
                    users = FileController.LoadAs<Dictionary<string,User>>(compactPath);

                    SaveToCache(cacheKey,users,Save);
                    if(users != null && users.TryGetValue(uuid,out result))
                    {
                        Program.usersLoaded++;
                        return result;
                    }
                } catch(InvalidOperationException e)
                {
                    Console.WriteLine($"Because of {e.Message} \n Removing corrupt user {compactPath}" );
                    FileController.Delete(compactPath);

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

        /// <summary>
        /// Clears the cache, usefull if new data is available on disc
        /// </summary>
        public static void ClearCache()
        {
            cache.Clear();
        }

        private static void Save(Dictionary<string,User> users)
        {
            if(users.Count == 0)
            {
                // nothing to save
                return;
            }
            FileController.SaveAs ("users/"+users.Values.First().uuid.Substring(0,4), users);
        }

        private static void Save(Dictionary<string,SaveAuction> auctions)
        {
            if(auctions.Count == 0)
            {
                // nothing to save
                return;
            }
            FileController.SaveAs (AuctionFilePath(auctions.Values.First().Uuid).Replace("sauc","nauc"), auctions);
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
        public static IEnumerable<SaveAuction> GetAllAuctions(bool deleteAfterRead = false)
        {
            return GetContents<SaveAuction>("auctions",true,deleteAfterRead);
        }




        public static IEnumerable<T> GetContents<T>(string path,bool subDirectories = false, bool deleteAfterRead = false)
        {
            foreach (var item in FileController.DirectoriesNames("*",path))
            {
                foreach (var file in Directory.GetFiles (item))
                {
                    var compactPath = Path.Combine(path,file);
                    if(FileController.Exists (compactPath))
                    {
                        foreach (var auction in FileController.ReadLinesAs<T> (compactPath,()=>
                        {// read impossible
                            // move the file
                            try{
                                FileController.Move(compactPath,$"corrupted/{compactPath.Substring(compactPath.Length-6,6)}");
                            } catch(Exception e)
                            {
                                Console.WriteLine($"Backup failed {e.Message} {e.StackTrace}" );
                            }
                            
                        }))
                        {
                            yield return auction;
                        }
                    }

                    if(deleteAfterRead && !StopPurging)
                    {
                        FileController.Delete(compactPath);
                    }
                }
            }
        }


         public static IEnumerable<T> GetFileContents<T>(string path,bool subDirectories = false,bool deleteAfterRead = false)
        {
            foreach (var file in FileController.FileNames("*",path))
            {
                var compactPath = Path.Combine(path,file);
                if(FileController.Exists (compactPath))
                {
                    foreach (var auction in FileController.ReadLinesAs<T> (compactPath))
                    {
                        yield return auction;
                    }
                }
                if(deleteAfterRead)
                    FileController.Delete(compactPath);
            }
        }

        public static IEnumerable<SaveAuction> GetAuctionsWith(string itemName,DateTime start, DateTime end)
        {
            foreach (var item in FileController.FileNames( "*","items"))
            {
                if(item.ToLower() == itemName.ToLower())
                {
                    var itemRef = GetOrCreateItemRef(item,true);
                     /*   
                    foreach (var auctionId in itemsAuctions)
                    {
                        // convert index to time based one
                        yield return GetOrCreateAuction(auctionId,null,true);
                    }*/

                    // new Time included index
                    foreach (var auctionRef in itemRef?.auctions)
                    {
                        if(auctionRef.End > start && auctionRef.End < end)
                            yield return GetOrCreateAuction(auctionRef.uuId,null,true);
                    }
                }
              
                
            }
        }

        public static Task Save (int removeUntil = 0, Action afterSave = null) {
                
            return Task.Run(()=>{
                // save dirty
                while(cache.Count > removeUntil)// dirty.TryDequeue(out Action a) && dirty.Count >= removeUntil)
                {
                    // also clear the cache
                    //a?.Invoke();
                    var keys = cache.Keys;

                    foreach (var item in keys)
                    {
                        if(removeUntil > 0)
                        {
                            // decide what to remove
                            if(cache.TryGetValue(item,out CacheItem elemet))
                            {
                                // skip this key if the hits are higher
                                if(--elemet.hits > 0)
                                    continue;
                            }
                        } 
                        if(cache.TryRemove(item,out CacheItem element))
                        {
                            element.Save(element);
                        }

                        afterSave?.Invoke();
                    }

                    

                    //Console.Write($"\rc: {dirty.Count}");
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
            var cacheKey = uuid.Substring(0,5);
            lock(cacheKey)
            {


            if(TryFromCache<Dictionary<string,SaveAuction>>(cacheKey,out Dictionary<string,SaveAuction> resultSet) 
                && resultSet.TryGetValue(uuid,out SaveAuction result) )
            {
                if(input !=null)
                {
                    resultSet[input.Uuid] = input;
                }
                return result;
            }

            var compactPath = AuctionFilePath(uuid).Replace("sauc","nauc");


            if(FileController.Exists (compactPath))
            {
                resultSet = FileController.LoadAs<Dictionary<string,SaveAuction>>(compactPath);
                if(noWrite)
                    SaveToCache(cacheKey,resultSet,s=>{});
                else
                    SaveToCache(cacheKey,resultSet,Save);
                if(resultSet != null && resultSet.TryGetValue(uuid,out result))
                {
                    if(input !=null)
                    {
                        resultSet[input.Uuid] = input;
                    }
                    return result;
                }
            }


            var auction = new SaveAuction () { Uuid = uuid };
            if(resultSet == null)
            {
                //Console.WriteLine("creating new " + compactPath);
                resultSet = new Dictionary<string, SaveAuction>();
                if(noWrite)
                    SaveToCache(cacheKey,resultSet,s=>{});
                else
                    SaveToCache(cacheKey,resultSet,Save);
                SaveToCache<Dictionary<string,SaveAuction>>(cacheKey,resultSet,Save);
            }
            if(input != null)
            {
                auction=input;
            }
            resultSet.Add(uuid,auction);

            return auction;
            }
        }

        public static void SaveToCache<T>(string key, T obj, Action<T> save)
        {
            if(cache.Count > maxItemsInCache)
            {
                // save half
                Save(maxItemsInCache/2);
            }


            var cacheItem = new CacheItem(){value=obj,Save=(self)=>{
                var lockKey = key;
                if(key.Length > 4)
                {
                    lockKey = key.Substring(0,4);
                }
                lock(lockKey)
                {
                    try{
                        save((T)self.value);
                        savedOnDisc++;
                    } catch(Exception e)
                    {
                        Console.WriteLine($"Failed to save: {key} {e.Message} {e.StackTrace}");
                    }
                }
            }};
            cache.AddOrUpdate(key,cacheItem,(sk,ob)=>cacheItem);
      
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
            var checkPointHeight = 0;
            var files = 0;
            Console.WriteLine("migrating");
            var checkPointName = "migrationCheckPoint";

            if(FileController.Exists(checkPointName))
            {
                checkPointHeight = FileController.LoadAs<int>(checkPointName);
            }

            //maxItemsInCache = 10;

            Parallel.ForEach(GetAllAuctions(true),(auction,state)=>// FileController.FileNames( "*","auctions"),item=>
            {
                //var compactPath = Path.Combine("auctions/"+ item);
                /*
                if(checkPointHeight > files)
                {
                    files++;
                    return;
                }
                try{
                    foreach (var auction in FileController.ReadLinesAs<SaveAuction>(compactPath))
                    {*/
                        if(auction == null || auction.Uuid == null)
                        {
                            return;
                        }
                        GetOrCreateAuction(auction.Uuid,auction);
                        i++;/*
                    }
                } catch(Exception e)
                {
                    Console.WriteLine($"Could not process {item} {e.Message} {e.StackTrace}");
                    return;
                }*/
                
                lock(checkPointName)
                    {
                        files++;
                
                        Console.Write($"\r{i} - {files} cache: ({cache.Count})\t {savedOnDisc}\t");
                    if(files%5000==0)
                    {
                    
                        checkPointHeight = files;
                        Console.Write("Reached Checkpoint " + files/5000);
                        Save(0,()=>Console.Write($"\r{i} - {files} cache: ({cache.Count})")).Wait();
                        FileController.SaveAs(checkPointName,checkPointHeight);
                        Console.WriteLine($"\r{i} - {files} cache: ({cache.Count})");
                        if(cache.Keys?.Count >0)
                        Console.WriteLine(cache.Keys?.First());
                    }

                    if(i > 10000 && i % 10 == 0)
                    {
                        StopPurging = true;
                        Console.WriteLine("Stopped at " + auction.Uuid);
                        state.Break();
                    }
                }
            });
            ItemPrices.Instance.Save();
            Save(0,()=>Console.Write($"\r{i} - {files} cache: ({cache.Count}) {savedOnDisc}")).Wait();
        }

        private static string AuctionFilePath(string uuid)
        {
            return "sauctions/"+uuid.Substring(0,5).Insert(2,"/");
            //return "fauctions/"+uuid.Substring(0,5);
        }

        public static void Save(User user)
        {
            FileController.ReplaceLine<User> ("users/"+user.uuid.Substring(0,4),(a)=>a.uuid == user.uuid, user);
        }

        public static void Save(SaveAuction auction)
        {
            //FileController.ReplaceLine<SaveAuction> ("auctions/"+auction.Uuid.Substring(0,4),(a)=>a.Uuid == auction.Uuid, auction);
            FileController.ReplaceLine<SaveAuction> (AuctionFilePath(auction.Uuid),(a)=>a!= null && a.Uuid == auction.Uuid, auction);
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
}