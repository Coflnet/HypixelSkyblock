using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using Coflnet;
using Hypixel.NET;
using MessagePack;
using Newtonsoft.Json;
using RestSharp;
using WebSocketSharp.Net;
using SixLabors.ImageSharp.Formats;
using dev;

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

        public static int RequestsSinceStart {get;private set;}

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
                Console.WriteLine ("5) Search For auction");
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
                case 't':
                    // test
                    
                break;
                case 'b':
                    BazaarUpdater.Update(apiKey);
                    break;
                case 's':
                    var server = new Server();
                    server.Start();
                    break;
                case 'u':
                    var updater = new Updater(apiKey);
                    updater.Update();
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
                    var itemName = "Enchanted Glowstone";
                    var time = DateTime.Parse("12.12.2019 9:53:55");
                    
                    var matchedAuctions = StorageManager.GetOrCreateItemRef(itemName,true).auctions;
                        //.Where(a=>a.End > (time-new TimeSpan(24,0,5)) );//&& a.End < time+new TimeSpan(1,0,5) );

                    Console.WriteLine("Searching");

                    foreach (var item in matchedAuctions)
                    {
                        var a = StorageManager.GetOrCreateAuction(item.uuId);

                        if(a.Count == 0 || a.HighestBidAmount/a.Count > 10 || a.HighestBidAmount == 0)
                        continue;

                        if(a.Bids == null || a.Bids.Count == 0)
                            Console.WriteLine($"{a.ItemName}(x{a.Count}) for {a.HighestBidAmount} End {a.End} ({a.Auctioneer}) to noone");
                        else
                            Console.WriteLine($"{a.ItemName}(x{a.Count}) for {a.HighestBidAmount} End {a.End} ({a.Auctioneer}) to {a.Bids.Last().Bidder} id: {item.uuId} ");
                        
                    }
    

                    break;
                case '6':
                    var targetItemName = Console.ReadLine();
                    Console.WriteLine($"Overview for the last 2 weeks for {targetItemName}");
                    var twoWeeksAgo = DateTime.UtcNow.ToLocalTime().Subtract(new TimeSpan(14,0,0,0));
                    var collection = AuctionsForItem(targetItemName,default(DateTime),DateTime.MaxValue)
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
                    Indexer.BuildIndexes();
                    break;
                case 'p':
                    Indexer.LastHourIndex();
                    //StorageManager.Migrate();
                    break;
                case 'n':
                    //Console.WriteLine(JsonConvert.SerializeObject(ItemDetails.Instance.Items));
                    Console.WriteLine(NBT.Pretty("H4sIAAAAAAAAAFVUXW/bNhS9jpPGdpot6LACW7uNG5otQZbWH0lVZ9iDpziOUScNYs99NCjp2iYikYJI5WN/yP/DP2zYpSQ3i15Ennt4eXl5DmsAVSiJGgCU1mBNBKVXJdhwVSpNqQZlw2dVWEfpz8F+Jdj6W3oJ8hvuhVgqQ/VcBHgW8pmm6L812AyEjkP+QIsGKsEKoa/h1XLhnPKIz/CELRf+QcOp0x/3Dpr1fTig4NAkKGdmXoSbrS9hGnzYG8bCf2AHrX3YJbKbCMPcOZf+Kt3u/1mN3X3YW9Ge7Fp3nhBbTWL+SsyOMdy/YcMYMcipT4kO8QC+Xy7aboj8Ftn4d2YntIHwecjG8KOdpp7QURHrygATNqRG0G8MP1noHv3UrBZ372NMBPUVWb/ft+dqn4kEWUfH6BvCMhZB2jDqjrgh3hh+IawnuDTsowhDmzqj9aOYh0LOslQ/EzBAMyfEPBSEgZgipUEq1lK+s5BSpliSU1JqwRjscOjTIeWM0q+CwzlPYolas7yGEYZ4IwgQmuWM0Ty1Rw5VEhDnB0LGPIpF8tiSMUoVqdSmAPhtuXjfNxixjidsnSdsgDy2t75chNf93vmIuYO++xHe0P1kISGNYmaOjIuEcRmwwJ5mueCt47qtiROReVwju+Az4Rc3z2iRRJ54D/aOHJQYCdQsjZVkIWWxHaAh5bV35MwS0n3wtlgcsBX/jroNr4nAQ62Yh2yaqH9QsqlKbAmNt3XYzkvQ6CvKAN+Qfi645MxV2lhR2TJfEOgqFQbqTp5kC61rdpaLYzp196rvsuHnT9enFVi/5BHCSwrkErQdsKUO76i/UIOvu/cm4aTbRHikKV2Gnbkyk1gZbtTEt+alxLUKfEXwVYb+pWSq4eVwdN297I3O/2zW/zjtXHR6XTuqQCVSgZgKTGBD2y0rUFWJmAk54jP41r3unI36l71J77p/OnE/DQZdd1SxrwVsD7qdKxvKSq/Bc/tWkEIjlIbqqohCm1RPuQzrIcmMhhsU8Qv/5NNnfuatfLIZ5urMF1X1SpD5fMs8ys++SWW7KYlvojO/5Smqtyv95fOtKdlrwjN7EbJWhhp+8WCetxaSTSba2iQHNjH3bJ7h+cw6b3KTOa+AptagtMQalKB1OtZtofNiW/NojKIwvTLT6uDZw1EEw5Vz7RxICWlKTX7jNNt86mH78IPjNQ6PsNU+9OpB6/CYB/XG0ZHTxOM6HYDLWxFOUo029zbdoRERakOdgJ1G/V3z/btGmzknDYddXVAP4FkudPuy/wdXfv82CAYAAA\u003d\u003d"));
                    break;
                case 'g':

                default:
                    return true;
            }
            return false;
        }

       


        static void Indexed()
        {
            long count = 0;

            foreach (var item in FileController.FileNames( "*","items"))
            {
                    var itemsAuctions = StorageManager.GetOrCreateItemRef(item);
                    if(itemsAuctions == null)
                    {
                        Console.WriteLine($"{itemsAuctions.Name} emtpy");
                        continue;
                    }
                        
                //Console.WriteLine($"{itemsAuctions.Name} has {itemsAuctions.auctionIds.Count}");
                
                count += itemsAuctions.auctions.Count;
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

        public static IEnumerable<SaveAuction> AuctionsForItem(string itemName,DateTime start, DateTime end)
        {
            itemName =  itemName.ToLower();
            return StorageManager.GetAuctionsWith(itemName,start, end);
        }

        /// <summary>
        /// Gets all the Auctions the User bidded on/in
        /// </summary>
        /// <param name="target">The user to search bids for</param>
        /// <returns></returns>
        static IEnumerable<SaveAuction> GetBiddedAuctions(User target)
        {
            var bids = new List<SaveAuction>();
            foreach (var bidReference in target?.Bids) {
                var seller = StorageManager.GetOrCreateUser(bidReference.sellerId);
                yield return seller.auctions[bidReference.auctionId];
            }
        }

        static bool IsAnotherInstanceRunning(string typeId = "lock", bool placeLockIfNonexisting = false)
        {
            var lastUpdate = new DateTime (1970,1,1);
            if (FileController.Exists ("lastUpdate"))
                lastUpdate = FileController.LoadAs<DateTime> ("lastUpdate").ToLocalTime ();

            var lastUpdateStart = new DateTime (0);
            if (FileController.Exists ("lastUpdateStart"))
                lastUpdateStart = FileController.LoadAs<DateTime> ("lastUpdateStart").ToLocalTime ();

            if(lastUpdateStart > lastUpdate && DateTime.Now - lastUpdateStart  < new TimeSpan(0,5,0))
            {
                Console.WriteLine("Last update start was to recent");
                return true;
            }
            Console.WriteLine($"{lastUpdateStart > lastUpdate} {DateTime.Now - lastUpdateStart}");
            FileController.SaveAs("lastUpdateStart",DateTime.Now);

            return false;
        }

        static void RemoveFileLock(string typeId)
        {
            FileController.Delete(typeId+"lock");
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

        

      
        /// <summary>
        /// Downloads username for a given uuid from mojang.
        /// Will return null if rate limit reached.
        /// </summary>
        /// <param name="uuid"></param>
        /// <returns>The name or null if error occurs</returns>
        public static string GetPlayerNameFromUuid(string uuid)
        {
            if(DateTime.Now.Subtract(new TimeSpan(0,10,0)) < BlockedSince && RequestsSinceStart >= 2000)
            {
                //Console.Write("Blocked");
                // blocked
                return null;
            } else if(RequestsSinceStart >= 2000)
            {
                Console.Write("\tFreed 2000 ");
                RequestsSinceStart = 0;
            }


            //Create the request
            RestClient client = null;
            RestRequest request ;
            int type = 0;

            if(RequestsSinceStart == 600)
            {
                BlockedSince = DateTime.Now;
            }
            
            if(RequestsSinceStart < 600)
            {
                client = new RestClient("https://api.mojang.com/");
                request = new RestRequest($"user/profiles/{uuid}/names", Method.GET);
            } else if(RequestsSinceStart < 1500)
            {
                client = new RestClient("https://mc-heads.net/");
                request = new RestRequest($"/minecraft/profile/{uuid}", Method.GET);
                type = 1;
            } else {
                client = new RestClient("https://minecraft-api.com/");
                request = new RestRequest($"/api/uuid/pseudo.php?uuid={uuid}", Method.GET);
                type = 2;
            }

            RequestsSinceStart++;


            //Get the response and Deserialize
            var response = client.Execute(request);


            if (response.Content == "" )
            {
                return null;
            }

            if(response.StatusCode != System.Net.HttpStatusCode.OK)
            {
                //Console.WriteLine("Blocked");
                //BlockedSince = DateTime.Now;
                return null;
            }

            if(type == 2)
            {
                return response.Content;
            }



            dynamic responseDeserialized = JsonConvert.DeserializeObject(response.Content);

            if(responseDeserialized == null)
            {
                return null;
            }

            switch(type)
            {
                case 0:
                    return responseDeserialized[responseDeserialized.Count-1]?.name;
                case 1:
                    return responseDeserialized.name;
            }

            


            return responseDeserialized.name;
        }


    }



    public class SubscribeEngine
    {
        
    }
}
