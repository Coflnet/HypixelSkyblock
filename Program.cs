using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Coflnet;
using dev;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using RestSharp;

namespace hypixel
{

    class Program
    {
        static string apiKey = SimplerConfig.Config.Instance["apiKey"];

        public static bool displayMode = false;

        public static bool FullServerMode { get; private set; }

        public static int usersLoaded = 0;

        /// <summary>
        /// Is set to the last time the ip was rate limited by Mojang
        /// </summary>
        /// <returns></returns>
        private static DateTime BlockedSince = new DateTime(0);

        public static int RequestsSinceStart { get; private set; }

        public static event Action onStop;

        static void Main(string[] args)
        {

            Console.CancelKeyPress += delegate
            {
                Console.WriteLine("\nAbording");
                onStop?.Invoke();

                var cacheCount = StorageManager.CacheItems;
                StorageManager.Stop();
                Indexer.Stop();

                var t = StorageManager.Save();
                Console.WriteLine("Saving");
                ItemPrices.Instance.Save();
                t.Wait();
                Console.WriteLine($"Saved {cacheCount}");
            };

            if (args.Length > 0)
            {
                FileController.dataPaht = args[0];
                Directory.CreateDirectory(FileController.dataPaht);
                Directory.CreateDirectory(FileController.dataPaht + "/users");
                Directory.CreateDirectory(FileController.dataPaht + "/auctions");

                if (args.Length > 1)
                {
                    runSubProgram(args[1][0]);
                    return;
                }
            }

            displayMode = true;

            while (true)
            {
                //try {

                Console.WriteLine("1) List Auctions");
                Console.WriteLine("2) List Bids");
                Console.WriteLine("3) Display");
                Console.WriteLine("4) List Won Bids");
                Console.WriteLine("5) Search For auction");
                Console.WriteLine("6) Avherage selling price in the last 2 weeks");
                Console.WriteLine("9) End");

                var res = Console.ReadKey();
                if (runSubProgram(res.KeyChar))
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
            switch (mode)
            {
                case 't':
                    // test

                    break;
                case 'b':
                    //var key = System.Text.Encoding.UTF8.GetString (FileController.ReadAllBytes ("apiKey")).Trim ();
                    BazaarUpdater.NewUpdate(apiKey);
                    break;
                case 'f':
                    FullServer();
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
                    foreach (var item in user.auctionIds)
                    {
                        Console.WriteLine(StorageManager.GetOrCreateAuction(item).ItemName);
                    }
                    break;
                case '2':
                    OptionTwo();
                    break;
                case '3':
                    DisplayUser();
                    break;

                case '4':
                    var targetUser = ReadUser();
                    Console.WriteLine("Won Auctions");
                    foreach (var item in GetBiddedAuctions(targetUser))
                    {
                        if (item.Bids[item.Bids.Count - 1].Bidder == targetUser.uuid)
                            Console.WriteLine($"{item.ItemName} for {item.HighestBidAmount}, Ended: {item.End < DateTime.UtcNow.ToLocalTime()}, End {item.End}");

                    }
                    break;
                case '5':
                    var itemName = "Enchanted Glowstone";
                    var time = DateTime.Parse("12.12.2019 9:53:55");

                    var matchedAuctions = StorageManager.GetOrCreateItemRef(itemName, true).auctions;
                    //.Where(a=>a.End > (time-new TimeSpan(24,0,5)) );//&& a.End < time+new TimeSpan(1,0,5) );

                    Console.WriteLine("Searching");

                    foreach (var item in matchedAuctions)
                    {
                        var a = StorageManager.GetOrCreateAuction(item.uuId);

                        if (a.Count == 0 || a.HighestBidAmount / a.Count > 10 || a.HighestBidAmount == 0)
                            continue;

                        if (a.Bids == null || a.Bids.Count == 0)
                            Console.WriteLine($"{a.ItemName}(x{a.Count}) for {a.HighestBidAmount} End {a.End} ({a.AuctioneerId}) to noone");
                        else
                            Console.WriteLine($"{a.ItemName}(x{a.Count}) for {a.HighestBidAmount} End {a.End} ({a.AuctioneerId}) to {a.Bids.Last().Bidder} id: {item.uuId} ");

                    }

                    break;
                case '6':
                    var targetItemName = Console.ReadLine();
                    Console.WriteLine($"Overview for the last 2 weeks for {targetItemName}");
                    var twoWeeksAgo = DateTime.UtcNow.ToLocalTime().Subtract(new TimeSpan(14, 0, 0, 0));
                    var collection = AuctionsForItem(targetItemName, default(DateTime), DateTime.MaxValue)
                        .Where(
                            item =>
                            {
                                if (item == null)
                                {
                                    Console.WriteLine("ein null :/");
                                }
                                return item != null && item.Bids != null && item.Bids.Count > 0
                                    //&& item.Start > twoWeeksAgo
                                    &&
                                    item.End < DateTime.UtcNow.ToLocalTime();
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
                    Console.WriteLine(NBT.Pretty("H4sIAAAAAAAAAHVUS28jRRAux8mu4ywbxIkLohdtRCKvkxnb49cByXGcxIJ1IvJYblbPTHmmyUyP6elxyJEbJ84cFgkJpEgcOSMh5afkhyCqZ5woHLjMo/qrr756dRVgHUqiCgClFVgRfqlegrVhkkldqkJZ82AdVlF64RJRjhYRlHMkVKAEGxfSVcivuBthqQzrx8LHw4gHKcH/qcJzX6TziN8QyVeJwgpZP4FP7247R8gVO/PI1md3t75j9+jV3bYbVmsnB5xphTLQoTn2araTH9eand2es0MsNkGOkUcFgNcalsOOT+kTt2stiz53CgfH7u62zdEOfE4uBzhDmWLh07UKfMNaghtWY5fCdwk4lhqjSASU+hJtN42G3vYEPZVo4bGa/ejYbNm7Tq6LgvQon7vb6J1I/SRml2+M1wHOdcgoJyqPYuPxGF6R9RC5Dun/kFMoGbBvcuyRSq4JfAnm55RioadFIpdMX+O32QIl10gIgK272/ZhFkXsDDXbT2SW9tloNhOeQKmZVlxIYobPKKXRAtUNUbgmj86Qa+4lsZuyCBcYpW9MuI4ORcq4ihPF5gI9ZAERpKxGBWhswQt6ESjVXKe7FNywHikuNfn4vjAqecRcowJT5vIU/RxD4m+STLEUI8oFfeZnMkCyehFP01fE9Jpk7aNKUV0R3hS8QzG9hnX/yw/sYRRgSCjTdFRPQNy2tswndV8h9QsVz8vFpf9A0yKa31kxLqZi7lse4BMK17Hu3//EnvYcqIsdOjnn8qki3iSqH9lyjMiWh1HoZx5l7POYiMExheRXKJlb1DvXN6OaIvfCZWGpICzkC2T4XSbmTMxgj0DGyhWyUGjjTNPBUh4jixOZapqUa0EdkmZsbQte0su0A71E+tSQLyjWQHnhf8rTc+5//o0NFREOQ06ZPdhtOvi1ODgodAN8fHdL4qPR6XjIDi4mR6OTCds/OTk/K8Oal0SJgr//+rMCqxPSZCrkPC7DwOdzLSid/SShcaCpvH//x/89oQqbo+9pOAeaNsLNNKZl+DBM9HSe0GwlU8/cP6SnWnmIu2H37L7d7fbtTrcClTjxxUyggopcKijDR8upmgqN8TSfaqJYq8B6okQg5DkP4PnF5MvJybtJJb/AXg4OBqfn48vRNE+yCi/MTUfzHNPykKQPfLO207RYWyIrl2Ezi7SIaf2m1/mCmxBknRWrPJ0Vq2y0l6GqHpe1gD0L8t0ufqrzx902BrpOV7OMVL3mLbvddjtOHTvYrrfsZrfOXR/rnPdmlt12vRl3yZ3LhYimZtXIfYPSJF1IuxnPYbO713D2GhZr922bDd4CrMCzxy7Dvy+PLTX1BQAA"));
                    //Console.WriteLine (JsonConvert.SerializeObject (ItemDetails.Instance.Items.Where (item => item.Value.AltNames != null && item.Value.AltNames.Count > 3 && !item.Key.Contains("DRAGON")).Select((item)=>new P(item.Value))));
                    break;
                case 'g':
                    var ds = new DataSyncer();
                    ds.Sync("e5bac11a8cc04ca4bae539aed6500823");
                    break;
                case 'm':
                    Migrator.Migrate();
                    break;
                case 'd':
                    DBTest();
                    break;
                default:
                    return true;
            }
            return false;
        }

        private static void OptionTwo()
        {
            using(var context = new HypixelContext())
            {

                var auctionI = context.Auctions.Include(ac => ac.Bids).Where(ac => ac.Id == 169333).First();
                var ares = (new BidComparer()).Equals(auctionI.Bids[0], auctionI.Bids[2]);

                var result = context.BazaarPull
                    .Include(p => p.Products)
                    .ThenInclude(p => p.BuySummery)
                    .Include(p => p.Products)
                    .ThenInclude(p => p.QuickStatus)

                    .Where(p => p.Timestamp > new DateTime(2020, 5, 22, 19, 32, 0)).ToList();

                var a = result.Where(b => b.Products.Exists(p => p.ProductId == "SUPERIOR_FRAGMENT"))
                    .ToList();
                var res = new List<ProductInfo>();
                foreach (var item in a)
                {
                    res.AddRange(item.Products.Where(p => p.ProductId == "SUPERIOR_FRAGMENT"));
                }
                foreach (var item in res)
                {
                    if (item == null || item.BuySummery == null)
                    {
                        Console.WriteLine("null");
                        continue;
                    }
                    if (item.BuySummery.Count == 0)
                    {
                        Console.WriteLine("empty");
                        continue;
                    }
                    Console.WriteLine($"Top: {item?.BuySummery?.First().PricePerUnit}  {item?.QuickStatus?.BuyPrice} {item?.QuickStatus?.BuyVolume}");
                }

            }
        }

        private static void FullServer()
        {
            Console.WriteLine("\n - Starting FullServer 0.2.4 - \n");
            Console.Write("Key: " + apiKey);
            FullServerMode = true;

            Updater updater = new Updater(apiKey);
            updater.UpdateForEver();
            Server server = new Server();
            Task.Run(() => server.Start());

            // bring the db up to date
            GetDBToDesiredState();
            ItemDetails.Instance.LoadFromDB();
            FastIndexPrototype();

            Console.WriteLine("booting db dependend stuff");
            WaitForDatabaseCreation();

            var bazzar = new BazaarUpdater();
            bazzar.UpdateForEver(apiKey);
            RunIndexer();

            NameUpdater.Run();
            SearchService.Instance.RunForEver();

            onStop += () =>
            {
                Console.WriteLine("Stopping");
                server.Stop();
                Indexer.Stop();
                updater.Stop();
                bazzar.Stop();
                System.Threading.Thread.Sleep(500);
                Console.WriteLine("done");
            };
            try {
            CleanDB();
            } catch(Exception e)
            {
                Console.WriteLine($"Cleaning failed {e.Message}");
            }

            System.Threading.Thread.Sleep(System.Threading.Timeout.Infinite);

        }

        private static void CleanDB()
        {
            using(var context = new HypixelContext())
            {
                // remove dupplicate itemnames
                context.Database.ExecuteSqlRaw(@"
                DELETE
                FROM AltItemNames
                WHERE ID NOT IN
                (
                    SELECT MIN(ID)
                    FROM AltItemNames
                    GROUP BY Name,DBItemId
                )
                ");
            }
        }

        private static void FastIndexPrototype()
        {
            Console.WriteLine($"Start making index {DateTime.Now}");
            try
            {
                Indexer.AvgPriceHistory();
            }
            catch (Exception)
            {

            }
            Console.WriteLine($"Done making index {DateTime.Now}");
        }

        private static void GetDBToDesiredState()
        {
            using(var context = new HypixelContext())
            {
                try
                {
                    context.Database.ExecuteSqlRaw("CREATE TABLE `__EFMigrationsHistory` ( `MigrationId` nvarchar(150) NOT NULL, `ProductVersion` nvarchar(32) NOT NULL, PRIMARY KEY (`MigrationId`) );");
                    context.Database.ExecuteSqlRaw("INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`) VALUES ('20201212165211_start', '3.1.6');");
                    context.Database.ExecuteSqlRaw("DELETE FROM Enchantment where SaveAuctionId is null");
                }
                catch (Exception e)
                {
                    if (e.Message != "Table '__EFMigrationsHistory' already exists")
                        Console.WriteLine($"creating migrations table failed {e.Message} {e.StackTrace}");
                }
                //context.Database.ExecuteSqlRaw("set net_write_timeout=99999; set net_read_timeout=99999");
                context.Database.SetCommandTimeout(99999);
                // Creates the database if not exists
                context.Database.Migrate();
                Console.WriteLine("\nmigrated :)\n");

                if (!context.Items.Any())
                    context.Items.AddRange(ItemDetails.Instance.Items.Values.Select(v => new DBItem(v)));
                context.SaveChanges();
            }
        }

        private static void RunIndexer()
        {
            Task.Run(() =>
            {
                Indexer.MiniumOutput();
                Indexer.LoadFromDB();
                while (true)
                {
                    try
                    {
                        Indexer.ProcessQueue();
                        Indexer.LastHourIndex();
                        //Indexer.NumberUsers();
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine();
                        Console.WriteLine($"An error occured while indexing {e.Message} {e.InnerException?.Message} {e.StackTrace} {e.InnerException?.StackTrace}");
                    }
                    System.Threading.Thread.Sleep(2000);
                }
            });
            RunUserIndexer();
        }

        /// <summary>
        /// Assigns ids to users, auctions,items and bids
        /// </summary>
        private static void RunUserIndexer()
        {
            Task.Run(() =>
            {
                Indexer.MiniumOutput();
                while (true)
                {
                    try
                    {
                        Indexer.NumberUsers();
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine();
                        Console.WriteLine($"Error occured while userIndexing: {e.Message} {e.StackTrace}\n {e.InnerException?.Message} {e.InnerException?.StackTrace} {e.InnerException?.InnerException?.Message} {e.InnerException?.InnerException?.StackTrace}");
                    }
                    System.Threading.Thread.Sleep(2000);
                }
            });
        }

        private static void WaitForDatabaseCreation()
        {
            try
            {

                using(var context = new HypixelContext())
                {
                    try
                    {
                    var testAuction = new SaveAuction()
                    {
                    Uuid = "00000000000000000000000000000000"
                        };
                        context.Auctions.Add(testAuction);
                        context.SaveChanges();
                        context.Auctions.Remove(testAuction);
                        context.SaveChanges();
                    }
                    catch (Exception)
                    {
                        // looks like db doesn't exist yet
                        Console.WriteLine("Waiting for db creating in the background");
                        System.Threading.Thread.Sleep(10000);
                    }
                    // TODO: switch to .Migrate()
                    context.Database.Migrate();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Waiting for db creating in the background {e.Message} {e.InnerException?.Message}");
                System.Threading.Thread.Sleep(10000);
            }
        }

        static void DBTest()
        {
            using(var context = new HypixelContext())
            {
                // Creates the database if not exists
                context.Database.Migrate();

                var data = new NbtData("H4sIAAAAAAAAAE1T3W7aSBQ+JO0WqLZ/6nU7Reld3DVg/iL1gkBCTGOHJEASqqoa28dmwD/IHidxql7sA+wj9JoX6BPwKH2Q1R5It6o00pyf73zfOUczRYAC5EQRAHJbsCWc3D85eNiJ0lDmirAtubcNhSPh4KHPvYRQ/xaheD5Pff/kJsQ4D1u6Azuu06w2uKYpTq3lKFW7bClc47ZSd3izbqlNB60K1Q3iaIGxFJgUIC/xVqYxJhvpPDwccz9F+I5ZX51cTlXnsu/bmV4nf3iu+if6bNHQw3FmdfS6HlD+qF0/zlq/YWuSX9T8q2p/OglPUysYq8fVMx+Pzsp2MLo2Z+b85MIU5mx0a1SuMqNrZ5PheG72Rqoxm8zNu/2pORyVzUp/ZtzNNbLVqztPM3qn6mToCLMynp30xoHZnavmxVgY3X5giH7LvVTf0wRFeOSIZOHzrAAPjqMY8xR8AX+uls1OFFhcsgFKCj1fLRvnMsbQk9M9tlpyFZ5RqIsuhgn+jAA8XS3r+9yOQnbI4wBjeEUgOgPhsUCEIgoTdhPF8w3+XfUtvCGD8i5PJMbsZip8ZFSdRWnMROLz0CHWJ8Q6WFcZGxi8vifdT103YXKKjOgDHrKEqB1mZbC7AZCCRkL2j29/sy4PuIfkNYjyXr3yMweP6f5/NlLb2RTjmfCmUrF9Yc+ZjBh3HJISCVugJH+9ENx0ufYDDNM3622ulrXV0j8Y6J08PDB5gPCS2D4eX/us/In0aved0tqfHtzKmLeljIWVSkzy8IiY9NCN4OJLSWYLLO2VBnrPaJul3RK3pbimiMv9BHdLeLso7anv1N0SPcqYgGtFgk3pwa8pfgFtmjYbJegQ/GseClEsPBEOuQdPTkd658Pnzln7cKibvfz6D8H24GBIjacp2TtVTaMfUOOK1WygommtltJqYUOx3Faj0bTLWC9zopQiwETyYAHPan9VVDqsrO6Vm6xtAGzBH/ebh22A/wDOxlWDtAMAAA\u003d\u003d");

                int count = 0;
                Console.Write("starting");
                foreach (var item in AllAuctions())
                {
                    Console.Write($"\r {count}  {item.AuctioneerId}");
                    if (!context.Auctions.Contains(item) && item != null)
                    {
                        context.Auctions.Add(item);
                        AddPlayer(context, item.AuctioneerId, ref count);
                        if (item.Bids != null)
                            foreach (var player in item.Bids)
                            {
                                AddPlayer(context, player.Bidder, ref count);
                            }
                        count++;
                    }
                    if (count % 1000 == 0)
                        context.SaveChanges();

                    if (count > 10000)
                        break;
                }

                // Saves changes
                context.SaveChanges();
            }
        }

        public static int AddPlayer(HypixelContext context, string uuid, ref int highestId, string name = null)
        {
            var p = new Player() { UuId = uuid, Id = highestId, ChangedFlag = true };
            var existingPlayer = context.Players.Find(p.UuId);
            if (existingPlayer != null)
                return existingPlayer.Id;

            if (p.UuId != null)
            {
                p.Name = name;
                p.Id = System.Threading.Interlocked.Increment(ref highestId);
                context.Players.Add(p);
                return p.Id;
            }
            return highestId;
        }

        static IEnumerable<SaveAuction> AllAuctions()
        {
            var path = "nauctions";
            foreach (var item in FileController.DirectoriesNames("*", path))
            {
                var dirName = Path.GetFileName(item);
                foreach (var fileName in Directory.GetFiles(item).Select(Path.GetFileName))
                {
                    var compactPath = $"{path}/{dirName}/{fileName}";

                    Dictionary<string, SaveAuction> auctions = null;
                    try
                    {
                        auctions = FileController.LoadAs<Dictionary<string, SaveAuction>>(compactPath);

                        FileController.Move(compactPath, compactPath.Replace("nauctions", "importedAuctions"));
                    }
                    catch (Exception e)
                    {

                        Console.WriteLine($"Skipping {compactPath} because of {e.Message}");
                    }
                    if (auctions == null)
                        continue;
                    foreach (var auction in auctions)
                    {
                        yield return auction.Value;
                    }

                }
            }

        }

        static void Indexed()
        {
            long count = 0;

            foreach (var item in FileController.FileNames("*", "items"))
            {
                var itemsAuctions = StorageManager.GetOrCreateItemRef(item);
                if (itemsAuctions == null)
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

                if (perPice > 100000)
                    continue;
                count++;
                sum += perPice;
                if (perPice < min)
                {
                    min = perPice;
                }
                if (perPice > max)
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
                if (a.Bids == null || a.Bids.Count == 0)
                {
                    continue;
                }
                var highestOwn = a.Bids.Where(bid => bid.Bidder == displayUser.uuid)
                    .OrderByDescending(bid => bid.Amount).FirstOrDefault();

                if (highestOwn == null)
                {
                    continue;
                }

                Console.WriteLine($"On {a.ItemName} {highestOwn.Amount} \tTop {highestOwn.Amount == a.HighestBidAmount} {highestOwn.Timestamp} ({item.auctionId.Substring(0,10)})");
            }

            Console.WriteLine("Auctions:");
            foreach (var item in displayUser.auctionIds)
            {
                var a = StorageManager.GetOrCreateAuction(item);
                if (a.Enchantments != null && a.Enchantments.Count > 0)
                {
                    // enchanted is only one item
                    Console.WriteLine($"{a.ItemName}  for {a.HighestBidAmount} End {a.End} ({item.Substring(0,10)})");
                    foreach (var enachant in a.Enchantments)
                    {
                        Console.WriteLine($"-- {enachant.Type} {enachant.Level}");
                    }
                }
                else
                    // not enchanted may be multiple (Count)
                    Console.WriteLine($"{a.ItemName} (x{a.Count}) for {a.HighestBidAmount} End {a.End} ({item.Substring(0,10)})");
            }
        }

        static User ReadUser()
        {
            return StorageManager.GetOrCreateUser(Console.ReadLine().Trim());
        }

        public static IEnumerable<SaveAuction> AuctionsForItem(string itemName, DateTime start, DateTime end)
        {
            itemName = itemName.ToLower();
            return StorageManager.GetAuctionsWith(itemName, start, end);
        }

        /// <summary>
        /// Gets all the Auctions the User bidded on/in
        /// </summary>
        /// <param name="target">The user to search bids for</param>
        /// <returns></returns>
        static IEnumerable<SaveAuction> GetBiddedAuctions(User target)
        {
            var bids = new List<SaveAuction>();
            foreach (var bidReference in target?.Bids)
            {
                var seller = StorageManager.GetOrCreateUser(bidReference.sellerId);
                yield return seller.auctions[bidReference.auctionId];
            }
        }

        static bool IsAnotherInstanceRunning(string typeId = "lock", bool placeLockIfNonexisting = false)
        {
            var lastUpdate = new DateTime(1970, 1, 1);
            if (FileController.Exists("lastUpdate"))
                lastUpdate = FileController.LoadAs<DateTime>("lastUpdate").ToLocalTime();

            var lastUpdateStart = new DateTime(0);
            if (FileController.Exists("lastUpdateStart"))
                lastUpdateStart = FileController.LoadAs<DateTime>("lastUpdateStart").ToLocalTime();

            Console.WriteLine($"{lastUpdateStart > lastUpdate} {DateTime.Now - lastUpdateStart}");
            FileController.SaveAs("lastUpdateStart", DateTime.Now);

            return false;
        }

        static void RemoveFileLock(string typeId)
        {
            FileController.Delete(typeId + "lock");
        }

        /// <summary>
        /// Downloads username for a given uuid from mojang.
        /// Will return null if rate limit reached.
        /// </summary>
        /// <param name="uuid"></param>
        /// <returns>The name or null if error occurs</returns>
        public static string GetPlayerNameFromUuid(string uuid)
        {
            if (DateTime.Now.Subtract(new TimeSpan(0, 10, 0)) < BlockedSince && RequestsSinceStart >= 2000)
            {
                //Console.Write("Blocked");
                // blocked
                return null;
            }
            else if (RequestsSinceStart >= 2000)
            {
                Console.Write("\tFreed 2000 ");
                RequestsSinceStart = 0;
            }

            //Create the request
            RestClient client = null;
            RestRequest request;
            int type = 0;

            if (RequestsSinceStart == 600)
            {
                BlockedSince = DateTime.Now;
            }

            if (RequestsSinceStart < 600)
            {
                client = new RestClient("https://api.mojang.com/");
                request = new RestRequest($"user/profiles/{uuid}/names", Method.GET);
            }
            else if (RequestsSinceStart < 1500)
            {
                client = new RestClient("https://mc-heads.net/");
                request = new RestRequest($"/minecraft/profile/{uuid}", Method.GET);
                type = 1;
            }
            else
            {
                client = new RestClient("https://minecraft-api.com/");
                request = new RestRequest($"/api/uuid/pseudo.php?uuid={uuid}", Method.GET);
                type = 2;
            }

            RequestsSinceStart++;

            //Get the response and Deserialize
            var response = client.Execute(request);

            if (response.Content == "")
            {
                return null;
            }

            if (response.StatusCode != System.Net.HttpStatusCode.OK)
            {
                // Shift out to another ip
                RequestsSinceStart += 1000;
                return null;
            }

            if (type == 2)
            {
                return response.Content;
            }

            dynamic responseDeserialized = JsonConvert.DeserializeObject(response.Content);

            if (responseDeserialized == null)
            {
                return null;
            }

            switch (type)
            {
                case 0:
                    return responseDeserialized[responseDeserialized.Count - 1]?.name;
                case 1:
                    return responseDeserialized.name;
            }

            return responseDeserialized.name;
        }

    }

}