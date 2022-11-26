using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Coflnet;
using dev;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using RestSharp;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;

namespace Coflnet.Sky.Core
{

    public class Program
    {
        public static string InstanceId { get; }

        public static bool displayMode = false;

        public static bool FullServerMode { get; private set; }
        public static bool LightClient { get; private set; }
        public static string KafkaHost = SimplerConfig.Config.Instance["KAFKA_HOST"];

        public static int usersLoaded = 0;

        /// <summary>
        /// Is set to the last time the ip was rate limited by Mojang
        /// </summary>
        /// <returns></returns>
        private static DateTime BlockedSince = new DateTime(0);
        private static string version = "0.4.0";
        public static string Version => version;

        public static int RequestsSinceStart { get; private set; }
        public static bool Migrated { get; internal set; }

        public static event Action onStop;

        public static CoreServer server;

        static Program()
        {

            InstanceId = DateTime.Now.Ticks.ToString() + version;
        }

        static void Main(string[] args)
        {
            Console.CancelKeyPress += delegate
            {
                Console.WriteLine("\nAbording");
                onStop?.Invoke();

                var cacheCount = StorageManager.CacheItems;
                StorageManager.Stop();

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
                case 'f':
                    FullServer();
                    break;

                case '7':
                    displayMode = false;
                    StorageManager.Migrate();
                    var auction = StorageManager.GetOrCreateAuction("00e45a19c27848829612be8edf53bd71");
                    Console.WriteLine(auction.ItemName);
                    //Console.WriteLine(ItemReferences.RemoveReforges("Itchy Bat man"));
                    break;
                case 'n':
                    var af = new SaveAuction();
                    NBT.FillDetails(af, "H4sIAAAAAAAAAE1Ry26bUBAdP9JiNl120yosKnXlCptglyWysY1jrmOCH7CJLnB5OBdswSUx/od+h7f9Bn9Y1ZuqqirNZuacmXNmRgToQCMVAaDRhGYaNn404GZ0qHLWEKHFcNyCziwNyYTiuOSsXyKIj88VpcvXnBQCNM0QvgTEx+pd1OtGfVXpKpoadbEiy11F9Yd337VADvwe73soDkdSsJSUHRAYObGqIOUfaQFuNphWBH6Sei57u0QOd3Ma1OaA586jTJfm/jg0803tj8yBmXF8pg8WtfYfV2V4q1JXmSdevqr8bCMvFJuSmd0LsvWL59iJu0XpcmqcUbaurX34vBy7J29ryGhs9VGGqNU3z8gxXr3tWnHPpmLt53s03STImaRW5taeE5/ReaW6mXuy9qs02vU07l6E92FaHimuO9BeHAoi8GIbbq+XIQ9dmhQ4zkjOpJDvfySh5NcSSwh85ig91JhKJSlecM5K6RBJeoGDJCff3gZfL9r1Qm3dNgRoI5wR+MRLfxlfS+meEH7QfwLcyQfjxAqsM1akfsVIKbz9FD7qtj6aIePp3jAeDPtpYutTy0AOQBPejXGGYwItgN8ILuLQDQIAAA\u003d\u003d", true);
                    Console.WriteLine(af.Tier);
                    Console.WriteLine(NBT.Pretty("H4sIAAAAAAAAAI1T227rRBTdac/hOEGi4gEh3owpT8Q9Thw7F4mHqk2pS+30kl4ShNDEs21PMrYje9wTF/EJPHP+oK98Qz+FD0HsFA7iEcuWZ61Ze60943ELoAkN0QKAxg7sCN74tQGvj/IqU40W7CoW70LzVHA8kSwuSfVnC1rXq0rKybsMCw12PA77LuOc9fjCdIbITZuHjsnCDjctm3XDnh31Obeo7qLI11gogWUTNIUbVRVYvkRr8PqWyQrhd6zPrPl9YvH7MxnWnkt4em3Jibdc973stl4cea6X0vzpoXteD/+jdRS7c+TMPkvm2WW1SG+tc/tK4ulVJ0xvHvxpsJwvk9VsGVuT40N79nhp+3eX9J45/tJ79I+9OlieJLPHExHcjTfBo0wn0/nKn44dejpBekv1gfTvvM38OFjOrs+G0b31LXXfgjdclGvJ6ia8Os8L1Ij8BPaenwYnecFikcX6BSoiiepfrxH5SH9+Yt+48DkRXqZQShFjFuLffMch7cfPT+60QFzkRQZfk47u7wqWqfIfzYFDA/eP9+/1DynwFREEVJUhqdv6u0SEiS6ysEBWYqnXeVXAFzQVJozSdKZ0nlcLibrM4/KAUvdfgvBKxIkyQynCla5ynT6vrhJR6mtUhOFTkmy9XnCKWfXldsXU1vOTvAmOJr4/CTR4FbAU4TNy/OH8Qep258dt536erbCmTdsbb1TBDpUqxKJSWGrwhuy8LMrht58NVa/RGBlk9P14ZrQNFirxQEzEZIltAzdrY9Qd9rruwaDTG1jba9BvG3S4Cir70AQVJnR4t6b/loYs4/VNidwYWW2jqgQNjKjroo2Ra3b7zDV7gwjNgR0uzNDqI+850YBH3PhFg2ZeCNrqKYtBuxhPf/LHwY22/W9glyCteesH+//HjsyUSLFULF3D3vBtd/C229GdkTXUL3yAHfjomKUsRtgF+AvZCYDnpwMAAA\u003d\u003d"));
                    //Console.WriteLine (JsonConvert.SerializeObject (.Instance.Items.Where (item => item.Value.AltNames != null && item.Value.AltNames.Count > 3 && !item.Key.Contains("DRAGON")).Select((item)=>new P(item.Value))));
                    break;
                case 'm':
                    Migrator.Migrate();
                    break;
                default:
                    return true;
            }
            return false;
        }

        public static void CreateHost(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                }).ConfigureAppConfiguration((context, config) =>
                {
                    Console.WriteLine("called configure\n+#+#+#+#+#");
                    config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);
                    config.AddJsonFile("custom.conf.json", optional: true, reloadOnChange: false);
                    config.AddEnvironmentVariables();
                });




        public static void FullServer()
        {
            Console.WriteLine($"\n - Starting FullServer {version} - \n");
            FullServerMode = true;

            server = new CoreServer();
            Task.Run(async () =>
            {
                while (true)
                {
                    try
                    {
                        CreateHost(new string[0]);
                    }
                    catch (Exception e) { dev.Logger.Instance.Error(e, "Exited asp.net"); }
                    await Task.Delay(2000);
                }
            }).ConfigureAwait(false);

            var mode = SimplerConfig.Config.Instance["MODE"];
            var modes = SimplerConfig.Config.Instance["MODES"]?.Split(",");
            if (modes == null)
                modes = new string[] { "indexer", "updater", "flipper" };

            LightClient = modes.Contains("light");
            if (LightClient)
            {


                Console.WriteLine("running on " + System.Net.Dns.GetHostName());
                System.Threading.Thread.Sleep(System.Threading.Timeout.Infinite);
            }


            Task redisInit = null;
            // bring the db up to date
            if (modes.Contains("indexer"))
            {
                GetDBToDesiredState();
                ItemDetails.Instance.LoadFromDB();
                redisInit = MakeSureRedisIsInitialized();

                Console.WriteLine("booting db dependend stuff");
                var bazaar = new BazaarIndexer();
                RunIsolatedForever(bazaar.ProcessBazaarQueue, "bazaar queue");
                //NameUpdater.Run();
                Task.Run(async () =>
                {
                    await Task.Delay(TimeSpan.FromMinutes(3));
                    await ItemPrices.Instance.BackfillPrices();
                }).ConfigureAwait(false);

                try
                {
                    CleanDB();
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Cleaning failed {e.Message}");
                }
            }


            onStop += () =>
            {
                Console.WriteLine("stopped");
            };


            redisInit?.GetAwaiter().GetResult();

            System.Threading.Thread.Sleep(System.Threading.Timeout.Infinite);

        }

        static CancellationTokenSource fillRedisCacheTokenSource;
        public static void FillRedisCache()
        {
            fillRedisCacheTokenSource?.Cancel();
            fillRedisCacheTokenSource = new CancellationTokenSource();
            Task.Run(async () =>
            {
                try
                {
                    await ItemPrices.Instance.FillHours(fillRedisCacheTokenSource.Token);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Backfill failed :( \n{e.Message}\n {e.InnerException?.Message} {e.StackTrace}");
                }
            }, fillRedisCacheTokenSource.Token).ConfigureAwait(false); ;
        }

        public static async Task MakeSureRedisIsInitialized()
        {
            var Key = "LastbazaarUpdate";
            try
            {

                var last = await CacheService.Instance.GetFromRedis<DateTime>(Key);
                await CacheService.Instance.SaveInRedis(Key, DateTime.Now);


                if (last < DateTime.Now - TimeSpan.FromMinutes(2))
                    Program.FillRedisCache();


            }
            catch (Exception e)
            {
                await CacheService.Instance.SaveInRedis(Key, default(DateTime));
                Logger.Instance.Error($"Redis init failed {e.Message} \n{e.StackTrace}");
            }

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
            try
            {
                bool isNew = false;
                using (var context = new HypixelContext())
                {
                    try
                    {
                        context.Database.ExecuteSqlRaw("CREATE TABLE `__EFMigrationsHistory` ( `MigrationId` nvarchar(150) NOT NULL, `ProductVersion` nvarchar(32) NOT NULL, PRIMARY KEY (`MigrationId`) );");
                        //context.Database.ExecuteSqlRaw("INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`) VALUES ('20201212165211_start', '3.1.6');");
                        isNew = true;
                        //context.Database.ExecuteSqlRaw("DELETE FROM Enchantment where SaveAuctionId is null");

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

                    context.SaveChanges();
                    if (!context.Items.Any() || context.Players.Count() < 2_000_000)
                        isNew = true;
                }
                Migrated = true;
            }
            catch (Exception e)
            {
                Logger.Instance.Error(e, "GetDB to desired state failed");
                Thread.Sleep(TimeSpan.FromSeconds(20));
                GetDBToDesiredState();
            }


        }


        public static void RunIsolatedForever(Func<Task> todo, string message, int backoff = 2000)
        {
            Task.Run(async () =>
            {
                while (true)
                {
                    try
                    {
                        await todo();
                    }
                    catch(TaskCanceledException)
                    {
                        return;
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine();
                        Console.WriteLine($"{message}: {e.Message} {e.StackTrace}\n {e.InnerException?.Message} {e.InnerException?.StackTrace} {e.InnerException?.InnerException?.Message} {e.InnerException?.InnerException?.StackTrace}");
                        await Task.Delay(2000);
                    }
                    await Task.Delay(backoff);
                }
            }).ConfigureAwait(false);
        }

        private static void RunIsolatedForever(Action todo, string message, int backoff = 2000)
        {
            RunIsolatedForever(() =>
            {
                todo();
                return Task.CompletedTask;
            }, message, backoff);
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

        private static System.Collections.Concurrent.ConcurrentDictionary<string, int> PlayerAddCache = new System.Collections.Concurrent.ConcurrentDictionary<string, int>();

        public static int AddPlayer(HypixelContext callingContext, string uuid, ref int highestId, string name = null)
        {
            try
            {
                if (PlayerAddCache.TryGetValue(uuid, out int id))
                    return id;


                using var context = new HypixelContext();
                var existingPlayer = context.Players.Find(uuid);
                if (existingPlayer != null)
                    return existingPlayer.Id;

                if (uuid != null)
                {
                    var p = new Player() { UuId = uuid, ChangedFlag = true };
                    p.Name = name;
                    p.Id = System.Threading.Interlocked.Increment(ref highestId);
                    context.Players.Add(p);
                    context.SaveChanges();
                    return p.Id;
                }
            }
            catch (Exception e)
            {
                dev.Logger.Instance.Error(e, "failed to save user");
                var existingPlayer = callingContext.Players.Find(uuid);
                if (existingPlayer != null)
                    return existingPlayer.Id;
            }


            return 0;


        }


        /// <summary>
        /// Downloads username for a given uuid from mojang.
        /// Will return null if rate limit reached.
        /// </summary>
        /// <param name="uuid"></param>
        /// <returns>The name or null if error occurs</returns>
        public static async Task<string> GetPlayerNameFromUuid(string uuid)
        {
            if (DateTime.Now.Subtract(new TimeSpan(0, 10, 0)) < BlockedSince && RequestsSinceStart >= 2400)
            {
                await Task.Delay(2000);
                Console.Write("Blocked");
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

            if (RequestsSinceStart == 60)
            {
                BlockedSince = DateTime.Now;
            }

            if (RequestsSinceStart < 600)
            {
                client = new RestClient("https://api.mojang.com/");
                request = new RestRequest($"user/profile/{uuid}", Method.Get);
                type = 1;
            } else if(RequestsSinceStart < 1200)
            {
                client = new RestClient("https://sessionserver.mojang.com");
                request = new RestRequest($"/session/minecraft/profile/{uuid}", Method.Get);
                type = 1;
            }
            else if (RequestsSinceStart < 1850)
            {
                client = new RestClient("https://mc-heads.net/");
                request = new RestRequest($"/minecraft/profile/{uuid}", Method.Get);
                type = 1;
            }
            else
            {
                client = new RestClient("https://minecraft-api.com/");
                request = new RestRequest($"/api/uuid/pseudo.php?uuid={uuid}", Method.Get);
                type = 2;
            }

            RequestsSinceStart++;

            //Get the response and Deserialize
            var response = await client.ExecuteAsync(request);

            if(response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                // Shift out to another method
                RequestsSinceStart += 300;
            }
            if (response.StatusCode != System.Net.HttpStatusCode.OK)
            {
                dev.Logger.Instance.Info(client.BuildUri(request) + $" returned {response.StatusCode} {response.Content}");
                return null;
            }
            if (response.Content == "")
            {
                Console.WriteLine("no content");
                return null;
            }

            if (type == 2)
            {
                if(response.Content == null)
                    Console.WriteLine("content null");
                return response.Content;
            }

            dynamic responseDeserialized = JsonConvert.DeserializeObject(response.Content);

            if(responseDeserialized == null || (responseDeserialized?.name == null) || responseDeserialized.name == null)
            {
                dev.Logger.Instance.Error(client.BuildUri(request) + $" returned {response.StatusCode} {response.Content}");
            }

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