using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Coflnet;
using Hypixel.NET;
using Hypixel.NET.SkyblockApi;
using MessagePack;
using Newtonsoft.Json;
using RestSharp;
using WebSocketSharp.Server;

namespace hypixel
{


    class Program {
        static string apiKey = "9be89f9a-74f9-4e90-a861-8e184aee685f";

        public static bool displayMode = false;

        public static int usersLoaded = 0;

        /// <summary>
        /// Is set to the last time the ip was rate limited by Mojang
        /// </summary>
        /// <returns></returns>
        private static DateTime BlockedSince = new DateTime(0);

        static void Main (string[] args) {

                      
            if (args.Length > 0 ) {
                FileController.dataPaht = args[0];
                Directory.CreateDirectory (FileController.dataPaht);
                Directory.CreateDirectory (FileController.dataPaht + "/users");
                Directory.CreateDirectory (FileController.dataPaht + "/auctions");

                if(args.Length > 1)
                {
                    runSubProgram(args[1][0]);
                    return;
                }
            } 

            displayMode = true;

            while (true) {
                //try {
                    
                Console.WriteLine ("1) List Auctions");
                Console.WriteLine ("2) List Bids");
                Console.WriteLine ("3) Display");
                Console.WriteLine ("4) List Won Bids");
                Console.WriteLine ("5) List Lost Bids");
                Console.WriteLine ("6) Avherage selling price in the last 2 weeks");
                Console.WriteLine ("9) End");

                var res = Console.ReadKey ();
                if(runSubProgram(res.KeyChar))
                    return;
                
                //} catch(Exception e)
                //{
                //    Console.WriteLine("Error Occured: "+ e.Message);
                //    throw e;
                //}
            }

        }

        /// <summary>
        /// returns true if application should be closed
        /// </summary>
        /// <param name="mode"></param>
        /// <returns></returns>
        static bool runSubProgram(char mode)
        {
            switch (mode) {
                case 's':
                    StartServer();
                    break;
                case 'u':
                    Update();
                    break;
                case '1':
                    var user = ReadUser();
                    foreach (var item in user.auctionIds) {
                        Console.WriteLine (StorageManager.GetOrCreateAuction(item).ItemName);
                    }
                    break;
                case '2':
                    foreach (var item in GetBiddedAuctions(ReadUser()))
                    {
                        Console.WriteLine($"{item.ItemName} highest bid {item.HighestBidAmount}, Ended: {item.End < DateTime.UtcNow.ToLocalTime()}");
                        foreach (var bids in item.Bids)
                        {
                            Console.WriteLine($"\t{bids.Amount} from {StorageManager.GetOrCreateUser(bids.Bidder).Name}");
                        }
                    }
                    break;
                case '3':
                    DisplayUser();
                    break;

                case '4':
                    var targetUser = ReadUser();
                    Console.WriteLine("Won Auctions");
                    foreach (var item in GetBiddedAuctions(targetUser))
                    {
                        if(item.Bids[item.Bids.Count-1].Bidder == targetUser.uuid)
                        Console.WriteLine($"{item.ItemName} for {item.HighestBidAmount}, Ended: {item.End < DateTime.UtcNow.ToLocalTime()}, End {item.End}");
    
                    }
                    break;
                case '5':
                    var target = ReadUser();
                    Console.WriteLine("Lost Auctions");
                    foreach (var item in GetBiddedAuctions(target))
                    {
                        if(item.Bids[item.Bids.Count-1].Bidder != target.uuid)
                        Console.WriteLine($"{item.ItemName} for {item.HighestBidAmount}, Ended: {item.End < DateTime.UtcNow.ToLocalTime()}, End {item.End}");
                    }
                    break;
                case '6':
                    var targetItemName = Console.ReadLine();
                    Console.WriteLine($"Overview for the last 2 weeks for {targetItemName}");
                    var twoWeeksAgo = DateTime.UtcNow.ToLocalTime().Subtract(new TimeSpan(14,0,0,0));
                    var collection = AuctionsForItem(targetItemName)
                            .Where(
                                item=> {
                                    if(item == null)
                                    {
                                        Console.WriteLine("ein null :/");
                                    }
                                    return item != null && item.Bids != null && item.Bids.Count > 0 
                                //&& item.Start > twoWeeksAgo
                                        && item.End < DateTime.UtcNow.ToLocalTime();
                                });

                    Console.WriteLine(collection.First().ItemName);
                    CalculateAggregates(collection);
                    break;
                case '7':
                displayMode = false;
                    StorageManager.Migrate();
                    var auction = StorageManager.GetOrCreateAuction("00e45a19c27848829612be8edf53bd71");
                    Console.WriteLine(auction.ItemName);
                    //Console.WriteLine(ItemReferences.RemoveReforges("Itchy Bat man"));
                    break;
                    case 'a':
                    Indexed();
                    break;
                case 'i':
                    Console.WriteLine("building indexes");
                    // increase max items in cache
                    StorageManager.maxItemsInCache += 40000;
                    var lastIndex = new DateTime(1970,1,1);
                    var updateStart = DateTime.Now;

                    if(FileController.Exists("lastIndex"))
                        lastIndex = FileController.LoadAs<DateTime>("lastIndex");

                    // add an extra hour to make sure we don't miss something
                    lastIndex = lastIndex.Subtract(new TimeSpan(1,0,0));

                    AddIndexes(StorageManager.GetAllAuctions());

                    // we are done
                    FileController.SaveAs("lastIndex",updateStart);
                    //Console.WriteLine(ItemReferences.RemoveReforges("Itchy Bat man"));
                    break;
                    case 'p':
                    LastHourIndex();
                    break;
                default:
                    return true;
            }
            return false;
        }

        private static void AddIndexes(IEnumerable<SaveAuction> auctions)
        {
            int count = 0;
            Parallel.ForEach(auctions,item=>{

                        CreateIndex(item);

                        if(count++ % 10 == 0)
                        Console.Write($"\r{count} {item.Uuid.Substring(0,4)} u{usersLoaded}");
                    });
                    StorageManager.Save().Wait();
        }

        private static void CreateIndex(SaveAuction item)
        {
            StorageManager.GetOrCreateItemRef(item.ItemName)?.auctionIds.Add(item.Uuid);
                        
                        try {
                          
                            var u = StorageManager.GetOrCreateUser(item.Auctioneer,true);
                            u?.auctionIds.Add(item.Uuid);
                            
                            PlayerSearch.Instance.LoadName(u);
                        }catch(Exception e)
                        {
                            Console.WriteLine("Corrupted " + item.Auctioneer + $" {e.Message}");
                        }

                    
                        foreach (var bid in item.Bids)
                        {
                            try {
                                StorageManager.GetOrCreateUser(bid.Bidder,true).Bids.Add(new AuctionReference(null,item.Uuid));
                            }catch(Exception)
                            {
                                Console.WriteLine("Corrupted " + bid.Bidder);
                            }

                        }
        }


        static void Indexed()
        {
            long count = 0;

            foreach (var item in FileController.FileNames( "*","items"))
            {
                    var itemsAuctions = StorageManager.GetOrCreateItemRef(item);
                    if(itemsAuctions == null|| itemsAuctions.auctionIds == null)
                    {
                        Console.WriteLine($"{itemsAuctions.Name} emtpy");
                        continue;
                    }
                        
                //Console.WriteLine($"{itemsAuctions.Name} has {itemsAuctions.auctionIds.Count}");
                
                count += itemsAuctions.auctionIds.Count;
            }
            Console.WriteLine($"Total: {count}");
        }

        static void CalculateAggregates(IEnumerable<SaveAuction> collection)
        {
            Console.WriteLine();
            long sum = 0;
            int count = 0;
            long min = long.MaxValue;
            long max = 0;
            foreach (var item in collection)
            {

                var perPice = item.HighestBidAmount / item.Count;
                
                if(perPice > 100000)
                    continue;
                count ++;
                sum += perPice;
                if(perPice<min)
                {
                    min = perPice;
                }
                if(perPice>max)
                {
                    max = perPice;
                }
                Console.Write($"\rAvg: {sum/count} Sum: {sum} Min: {min} Max: {max} Count: {count} ");
            }
            Console.WriteLine();
        }


        static void DisplayUser()
        {
            var displayUser = ReadUser();

            Console.WriteLine("Bids");
            foreach (var item in displayUser.Bids)
            {
                var a = StorageManager.GetOrCreateAuction(item.auctionId);
                if(a.Bids == null || a.Bids.Count == 0)
                {
                    continue;
                }
                var highestOwn = a.Bids.Where(bid=>bid.Bidder == displayUser.uuid)
                            .OrderByDescending(bid=>bid.Amount).FirstOrDefault();

                if(highestOwn == null)
                {
                    continue;
                }

                Console.WriteLine($"On {a.ItemName} {highestOwn.Amount} \tTop {highestOwn.Amount == a.HighestBidAmount} {highestOwn.Timestamp} ({item.auctionId.Substring(0,10)})");
            }

            Console.WriteLine("Auctions:");
            foreach (var item in displayUser.auctionIds)
            {
                var a = StorageManager.GetOrCreateAuction(item);
                if(a.Enchantments != null && a.Enchantments.Count > 0){
                    // enchanted is only one item
                    Console.WriteLine($"{a.ItemName}  for {a.HighestBidAmount} End {a.End} ({item.Substring(0,10)})");
                    foreach (var enachant in a.Enchantments)
                    {
                        Console.WriteLine($"-- {enachant.Type} {enachant.Level}");
                    }
                } else
                    // not enchanted may be multiple (Count)
                    Console.WriteLine($"{a.ItemName} (x{a.Count}) for {a.HighestBidAmount} End {a.End} ({item.Substring(0,10)})");
            }
        }

        static User ReadUser()
        {
            return StorageManager.GetOrCreateUser(Console.ReadLine ().Trim ());
        }

        public static IEnumerable<SaveAuction> AuctionsForItem(string itemName)
        {
            itemName =  itemName.ToLower();
            return StorageManager.GetAuctionsWith(itemName);
        }


        static IEnumerable<SaveAuction> GetBiddedAuctions(User target)
        {
            var bids = new List<SaveAuction>();
            foreach (var bidReference in target?.Bids) {
                var seller = StorageManager.GetOrCreateUser(bidReference.sellerId);
                yield return seller.auctions[bidReference.auctionId];
            }
            //return bids;//.GroupBy(x => x.Start).Select(y => y.First());
        }

        static void Update () {
            Console.WriteLine($"Usage bevore update {System.GC.GetTotalMemory(false)}");
            var hypixel = new HypixelApi (apiKey, 5);
            long max = 1;
            var lastUpdate = new DateTime (1970,1,1);
            if (FileController.Exists ("lastUpdate"))
                lastUpdate = FileController.LoadAs<DateTime> ("lastUpdate").ToLocalTime ();

            var lastUpdateStart = new DateTime (0);
            if (FileController.Exists ("lastUpdateStart"))
                lastUpdateStart = FileController.LoadAs<DateTime> ("lastUpdateStart").ToLocalTime ();

            if(lastUpdateStart > lastUpdate && DateTime.Now - lastUpdateStart  < new TimeSpan(0,5,0))
            {
                Console.WriteLine("Last update start was to recent");
                return;
            }
            Console.WriteLine($"{lastUpdateStart > lastUpdate} {DateTime.Now - lastUpdateStart}");
            FileController.SaveAs("lastUpdateStart",DateTime.Now);

            var updateStartTime = DateTime.UtcNow.ToLocalTime ();

            Console.WriteLine (updateStartTime);

            TimeSpan timeEst = new TimeSpan(0,1,1);
            Console.WriteLine ("Updating Data");

            // add extra miniute to start to catch lost auctions
            lastUpdate = lastUpdate - new TimeSpan(0,0,1);

            var tasks = new List<Task>();
            int sum = 0;
            int doneCont=0;
            object sumloc = new object();

            for (int i = 0; i < max; i++) {
                var res = hypixel.GetAuctionPage (i);
                if(i == 0)
                {
                    // correct update time
                    Console.WriteLine($"Updating difference {lastUpdate} {res.LastUpdated}");
                    //lastUpdate = res.LastUpdated;
                }
                max = res.TotalPages;
                
                tasks.Add(Task.Run(()=>{
                     var val = Save(res,lastUpdate);
                     lock(sumloc)
                     {
                         sum += val;
                         // process done
                         doneCont++;
                     }
                    PrintUpdateEstimate(i,doneCont,sum,updateStartTime,max);
                }));
                PrintUpdateEstimate(i,doneCont,sum,updateStartTime,max);

                
                // try to stay under 100MB
                if(System.GC.GetTotalMemory(false) > 100000000)
                {
                    Console.Write("\t\t mem: " + System.GC.GetTotalMemory(false));
                    // to much memory wait on a thread
                    //tasks[i/2].Wait();
                    //tasks[i/2].Dispose();
                    System.GC.Collect();
                }
            }

            foreach (var item in tasks)
            {
                //Console.Write($"\r {index++}/{updateEstimation} \t({index}) {timeEst:mm\\:ss}");
                item.Wait();
                PrintUpdateEstimate(max,doneCont,sum,updateStartTime,max);
            }


            StorageManager.Save ().Wait();
            FileController.SaveAs ("lastUpdate", updateStartTime);
            Console.WriteLine($"Done {sum} in {DateTime.Now.ToLocalTime()}");
        }

        static void PrintUpdateEstimate(long i,long doneCont,long sum,DateTime updateStartTime, long max)
        {
            var index = sum;
            // max is doubled since it is counted twice (download and done)
            var updateEstimation = index*max*2/(i+1+doneCont)+1;
            var ticksPassed = (DateTime.Now.ToLocalTime().Ticks-updateStartTime.Ticks);
            var timeEst = new TimeSpan(ticksPassed/(index+1)*updateEstimation-ticksPassed) ;
            Console.Write($"\r Loading: ({i}/{max}) Done With: {doneCont} Total:{sum} {timeEst:mm\\:ss}");
        }

        // builds the index for all auctions in the last hour
        static void LastHourIndex()
        {
            Console.WriteLine($"{DateTime.Now}");
            var targetTmp = FileController.GetAbsolutePath("awork");
            DeleteDir(targetTmp);
            if(!FileController.Exists("auctionpull"))
            {
                // update first
                Update();
            }
            Directory.Move(FileController.GetAbsolutePath("auctionpull"),targetTmp);


            int count = 0;
            Parallel.ForEach(StorageManager.GetFileContents<SaveAuction>("awork"),item=>{
                StorageManager.GetOrCreateAuction(item.Uuid,item);
                CreateIndex(item);
                count ++;
                Console.Write($"\r {count}");
            });
            StorageManager.Save().Wait();

            DeleteDir(targetTmp);
        }

        private static void DeleteDir(string path)
        {
            if(!Directory.Exists(path))
            {
                // nothing to do
                return;
            }

            System.IO.DirectoryInfo di = new DirectoryInfo(path);

            foreach (FileInfo file in di.GetFiles())
            {
                file.Delete(); 
            }
            foreach (DirectoryInfo dir in di.GetDirectories())
            {
                dir.Delete(true); 
            }
            Directory.Delete(path);
        }

        static int Save(GetAuctionPage res,DateTime lastUpdate)
        {
            int count = 0;
            foreach (var item in res.Auctions) {
     
                // nothing changed if the last bid is older than the last update
                if (item.Bids.Count > 0 && item.Bids[item.Bids.Count - 1].Timestamp < lastUpdate ||
                    item.Bids.Count == 0 && item.Start < lastUpdate) {
                    continue;
                }

                try{
                    //var a = StorageManager.GetOrCreateAuction(item.Uuid,new SaveAuction(item));
                    var auction = new SaveAuction(item);
                    FileController.ReplaceLine<SaveAuction> ("auctionpull/"+auction.Uuid.Substring(0,4),(a)=>a.Uuid == auction.Uuid, auction);
                    //CreateIndex(a);
                } catch(Exception e)
                {
                    Console.WriteLine($"Error {e.Message} on {item.ItemName} {item.Uuid} from {item.Auctioneer}");
                    Console.WriteLine(e.StackTrace);
                }

                count++;
            }

            return count;
        }

        static void pastAuctions (string name) {
            var hypixel = new HypixelApi (apiKey, 300);
            var getProfilesByName = hypixel.GetSkyblockProfilesByName ("ekwav");
            foreach (var profile in getProfilesByName) {
                Console.WriteLine ("---" + profile.Profile.ProfileId);
                var auctionsByPlayerName = hypixel.GetAuctionsByProfileId (profile.Profile.ProfileId);

                foreach (var item in auctionsByPlayerName.Auctions) {
                    Console.WriteLine ($"->{item.ItemName} {item.HighestBidAmount} {item.Start} {item.End}");
                }
            }
        }

        

      

        public static string GetPlayerNameFromUuid(string uuid)
        {
            if(DateTime.Now.Subtract(new TimeSpan(0,10,0)) < BlockedSince)
            {
                // blocked
                return null;
            }


            //Create the request
            var client = new RestClient("https://api.mojang.com/");
            var request = new RestRequest($"user/profiles/{uuid}/names", Method.GET);

            //Get the response and Deserialize
            var response = client.Execute(request);


            if (response.Content == "")
            {
                return null;
            }

            dynamic responseDeserialized = JsonConvert.DeserializeObject(response.Content);


            if(response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                Console.WriteLine("Blocked");
                BlockedSince = DateTime.Now;
                return null;
            }

            //Mojang stores the names as array so return the latest
            return responseDeserialized[responseDeserialized.Count-1].name;
        }

        private static void StartServer()
        {
            var server = new WebSocketServer(8008);
            server.AddWebSocketService<SkyblockBackEnd> ("/skyblock");
            server.Start ();
            Console.ReadKey (true);
            server.Stop ();
        }

    }



    public class SubscribeEngine
    {
        
    }

    [MessagePackObject]
    public class SubscribeItem
    {
        [Key("name")]
        public string itemName;
        [Key("category")]
        public string itemCategory;
        [Key("count")]
        public short itemCount;
        [Key("playerId")]
        public string playerUUid;
        [Key("minPrice")]
        public long minPrice;
        [Key("maxPrice")]
        public long maxPrice;

        [Key("enchantments")]
        public Enchantment[] enchantments;


        public enum Type
        {
            NewAuction = 1,
            NewBid = 2
        }

        public Type type;


        public bool Match(SaveAuction auction)
        {
            if(!String.IsNullOrEmpty(itemName) && !auction.ItemName.StartsWith(itemName))
            {
                return false;
            }
            if(type == Type.NewAuction)
            {
                if(!String.IsNullOrEmpty(playerUUid) && auction.Auctioneer != playerUUid)
                {
                    return false;
                }
            } else if(type == Type.NewBid)
            {
                if(!String.IsNullOrEmpty(playerUUid) && !auction.Bids.Where(b=>b.Bidder == playerUUid).Any())
                {
                    return false;
                }
            }

            // matching category
            if(!String.IsNullOrEmpty(itemCategory) && auction.Category != itemCategory)
            {
                return false;
            }

            // matching count
            if(itemCount != 0 && auction.Count != itemCount)
            {
                return false;
            }

            var itemPrice = auction.StartingBid;
            if(auction.Bids.Count > 0)
            {
                itemPrice = auction.HighestBidAmount;
            }

            // in price range
            if((maxPrice != 0 && itemPrice > maxPrice) ||itemPrice < minPrice)
            {
                return false;
            }

            if(enchantments != null)
            {
                // make sure there are all the required enchantments
                return enchantments.Except(auction.Enchantments).Any();
            }
            
            return true;
        }
    }
}
