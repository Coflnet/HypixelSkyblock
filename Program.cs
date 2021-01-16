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
using Stripe;
using Stripe.Checkout;

namespace hypixel
{

    class Program
    {
        static string apiKey = SimplerConfig.Config.Instance["apiKey"];
        public static string StripeKey;
        public static string StripeSigningSecret;


        public static bool displayMode = false;

        public static bool FullServerMode { get; private set; }

        public static int usersLoaded = 0;

        /// <summary>
        /// Is set to the last time the ip was rate limited by Mojang
        /// </summary>
        /// <returns></returns>
        private static DateTime BlockedSince = new DateTime(0);
        private static string version = "0.2.4";
        public static string Version => version;

        public static int RequestsSinceStart { get; private set; }

        public static event Action onStop;

        static void Main(string[] args)
        {
            StripeKey = SimplerConfig.Config.Instance["stripeKey"];
            StripeSigningSecret = SimplerConfig.Config.Instance["stripeSecret"];
            StripeConfiguration.ApiKey = Program.StripeKey;


            Console.CancelKeyPress += delegate
            {
                Console.WriteLine("\nAbording");
                onStop?.Invoke();

                var cacheCount = StorageManager.CacheItems;
                StorageManager.Stop();
                Indexer.Stop();

                var t = StorageManager.Save();
                Console.WriteLine("Saving");
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
                    NotificationService.Instance.NotifyAsync("dPRj0dnG2NcY_kMTdNbpjz:APA91bHJINgv1SjuUlv-sGM21wLlHX5ISC5nYgl8DKP2r0fm273Cs0ujcESW6NR1RyGvFDtTBdQLK0SSq5TY_guLgc57VylKk8AAnH_xKq3zDIrdA1F6UhJNTu-Q0wNDKKIIQkYoVcyj","test","click me","https://sky.coflnet.com").Wait();
                    SetGoogleIdCommand.ValidateToken("eyJhbGciOiJSUzI1NiIsImtpZCI6IjI1MmZjYjk3ZGY1YjZiNGY2ZDFhODg1ZjFlNjNkYzRhOWNkMjMwYzUiLCJ0eXAiOiJKV1QifQ.eyJpc3MiOiJhY2NvdW50cy5nb29nbGUuY29tIiwiYXpwIjoiNTcwMzAyODkwNzYwLW5sa2dkOTliNzFxNGQ2MWFtNGxwcWRoZW4xcGVuZGR0LmFwcHMuZ29vZ2xldXNlcmNvbnRlbnQuY29tIiwiYXVkIjoiNTcwMzAyODkwNzYwLW5sa2dkOTliNzFxNGQ2MWFtNGxwcWRoZW4xcGVuZGR0LmFwcHMuZ29vZ2xldXNlcmNvbnRlbnQuY29tIiwic3ViIjoiMTAxOTkzNTcwNzI0MDg4NDMyMjk4IiwiZW1haWwiOiJ0by5jb2ZsbmV0QGdtYWlsLmNvbSIsImVtYWlsX3ZlcmlmaWVkIjp0cnVlLCJhdF9oYXNoIjoiYWdLN21RM2YySFZQclZNQ3l1UVVmdyIsIm5hbWUiOiJFa3dhdiBDb2ZsbmV0IiwicGljdHVyZSI6Imh0dHBzOi8vbGgzLmdvb2dsZXVzZXJjb250ZW50LmNvbS9hLS9BT2gxNEdobEx6TjV5U1o3VDZWYnpYRnFhUlR4c3dNRXJLaW1VQk1uem41Nz1zOTYtYyIsImdpdmVuX25hbWUiOiJFa3dhdiIsImZhbWlseV9uYW1lIjoiQ29mbG5ldCIsImxvY2FsZSI6ImRlIiwiaWF0IjoxNjEwMjk4MTE5LCJleHAiOjE2MTAzMDE3MTksImp0aSI6ImIzMWYzODUwNDMwYjNhOWMxNTQ5YTRjMDFiNTFiNTBlZjBhZTkwYTAifQ.cvsqp0GaYca---qkBAm-nS3QI-x_ZTGkzZh7sk-SsYctubikHqJz9VpafY_ih88ouOFTg_CWHKPMvS9dTrR8T4W_iY65cYp2hxsc-iMignDBgxbP6KlUCm3MvpRTHTdLAtL3Eq4JeXAL6_BN21AetRMaOhsWMgvz6yprhTkirOgFSuDt386Q8NXr19csjDhAW6bb2bRwEYJp4ZlBXD77zfzP_kZaF2y671M_lZUXnrqKrDqF7sFL2Jx4r6htKV_e86IuKhx0N1ttNTuEOeqccIZHdRQasivVO9Nq0twjhFIWn-5-azkPyz0VstxzIuYc7mTi2LSVjF4QDl-aLiOlPQ");
                    break;
                case 'b':
                    //var key = System.Text.Encoding.UTF8.GetString (FileController.ReadAllBytes ("apiKey")).Trim ();
                    BazaarUpdater.NewUpdate(apiKey).Wait();
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
                    updater.Update().Wait();
                    break;

                case '7':
                    displayMode = false;
                    StorageManager.Migrate();
                    var auction = StorageManager.GetOrCreateAuction("00e45a19c27848829612be8edf53bd71");
                    Console.WriteLine(auction.ItemName);
                    //Console.WriteLine(ItemReferences.RemoveReforges("Itchy Bat man"));
                    break;
                case 'i':
                    Console.WriteLine("got removed");
                    //Indexer.BuildIndexes();
                    break;
                case 'p':
                    Indexer.LastHourIndex().Wait();
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
                default:
                    return true;
            }
            return false;
        }


        private static void FullServer()
        {
            Console.WriteLine($"\n - Starting FullServer {version} - \n");
            Console.Write("Key: " + apiKey);
            FullServerMode = true;
            Indexer.MiniumOutput();

            Updater updater = new Updater(apiKey);
            updater.UpdateForEver();
            Server server = new Server();
            Task.Run(() => server.Start());

            // bring the db up to date
            GetDBToDesiredState();
            ItemDetails.Instance.LoadFromDB();
            SubscribeEngine.Instance.LoadFromDb();


            Task.Run(async () =>
            {
                try
                {
                    await ItemPrices.Instance.FillHours();
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Backfill failed :( \n{e.Message}\n {e.InnerException?.Message} {e.StackTrace}");
                }
            });


            Console.WriteLine("booting db dependend stuff");

            var bazzar = new BazaarUpdater();
            bazzar.UpdateForEver(apiKey);
            RunIndexer();

            NameUpdater.Run();
            SearchService.Instance.RunForEver();
            CacheService.Instance.RunForEver();

            RunIsolatedForever(async () =>
            {
                await SubscribeEngine.Instance.SendNotifications();
                await Task.Delay(5000);

            }, "Error on pushnotifications ");

            onStop += () =>
            {
                StopServices(updater, server, bazzar);
            };
            try
            {
                CleanDB();
            }
            catch (Exception e)
            {
                Console.WriteLine($"Cleaning failed {e.Message}");
            }

            System.Threading.Thread.Sleep(System.Threading.Timeout.Infinite);

        }

        private static void StopServices(Updater updater, Server server, BazaarUpdater bazzar)
        {
            Console.WriteLine("Stopping");
            server.Stop();
            Indexer.Stop();
            updater.Stop();
            bazzar.Stop();
            System.Threading.Thread.Sleep(500);
            Console.WriteLine("done");
        }

        private static void CleanDB()
        {
            // try cleaning when the dust settled
            System.Threading.Thread.Sleep(TimeSpan.FromHours(1));
            using (var context = new HypixelContext())
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


        private static void GetDBToDesiredState()
        {
            using (var context = new HypixelContext())
            {
                try
                {
                    context.Database.ExecuteSqlRaw("CREATE TABLE `__EFMigrationsHistory` ( `MigrationId` nvarchar(150) NOT NULL, `ProductVersion` nvarchar(32) NOT NULL, PRIMARY KEY (`MigrationId`) );");
                    //context.Database.ExecuteSqlRaw("INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`) VALUES ('20201212165211_start', '3.1.6');");
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
            RunIsolatedForever(async () =>
            {
                Indexer.ProcessQueue();
                await Indexer.LastHourIndex();

            }, "An error occured while indexing");

            RunUserIndexer();
        }

        /// <summary>
        /// Assigns ids to users, auctions,items and bids
        /// </summary>
        private static void RunUserIndexer()
        {
            RunIsolatedForever(Numberer.NumberUsers, "Error occured while userIndexing");
        }

        private static void RunIsolatedForever(Func<Task> todo, string message)
        {
            Task.Run(async () =>
            {
                while (true)
                {
                    try
                    {
                        await todo();
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine();
                        Console.WriteLine($"{message}: {e.Message} {e.StackTrace}\n {e.InnerException?.Message} {e.InnerException?.StackTrace} {e.InnerException?.InnerException?.Message} {e.InnerException?.InnerException?.StackTrace}");
                    }
                    await Task.Delay(2000);
                }
            });
        }

        private static void WaitForDatabaseCreation()
        {
            try
            {

                using (var context = new HypixelContext())
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


        /// <summary>
        /// Downloads username for a given uuid from mojang.
        /// Will return null if rate limit reached.
        /// </summary>
        /// <param name="uuid"></param>
        /// <returns>The name or null if error occurs</returns>
        public static async Task<string> GetPlayerNameFromUuid(string uuid)
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
            var response = await client.ExecuteAsync(request);

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