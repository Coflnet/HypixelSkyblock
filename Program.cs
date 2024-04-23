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

            };

            if (args.Length > 0)
            {

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

                case 'n':
                    var af = new SaveAuction();
                    NBT.FillDetails(af, "H4sIAAAAAAAAAD2STZKaQBTHn18ZZJNFKvtOKpWqLKxCUXEWWTjoRCyBiaIou1ZaaW3AQDMOHCC7rHMEL5ATeJQcJJUmi/Tu9fv9X/3fhwzQhAqVAaBShSr1Kz8q0NDjLOJVGWocH5pQJ9EugPLVoDmhPnlk+JCK8I8M8uKUMWZfIpJIUDV8+KCoO62LtX5L7fv3LVXtb1sYE7XV0zoDRWt3dr7SFrqnJD6ThFOSNkHi5IVnCUn/2ZCgscIsI/CL5FPFWweKv56yXW70RewsFGYbx7NmRKt8qxt9IxT5yUNh08Gz/2XV9Ser3Fub2dZlmeda+cb1lFnYY75+/7/WrBgrpmP2bHep2s5jaBWn9iacHq3RrrM5Lju2452sYnmxikPHcjZda+QH1pFRwVCzGKtmsQpN92vPHAm96x290bBt0en9fq18Fh3IcOfT9MxwLmY3ixMiQTnlN7frYE6+ZVQ0ihh5Jgz14E58PsSXcpqfbldtiFIBpEGOeIIpQzxG+5ix+ILyOEsQThLBvhPwxxI+n5kAA5qiJItICW9FGsUJ2mepiC8xvBccjw+EB0TIuaAJmmcR3aEn4pOUY1ZWe3u77m9XptumaVtItxfm2DF0CeoWDklpHP/++R0tGA3zUk2QIZp8PX4RJoecJ3Sb8XJ5jdJGWoPGYmaYm3KXIJU3BfX50hoDVOHVCIf4QMQhwV9sJ18cegIAAA\u003d\u003d", true);
                    Console.WriteLine(JsonConvert.SerializeObject(af, Formatting.Indented));
                    Console.WriteLine(NBT.Pretty("H4sIAAAAAAAAAI1UTW/jVBR9aTtMGoEGwQxsAD2i6Ugw8ciJPxJXmkVI0sZRnGaSfgaNRs/2je3EH8F+busiVqxnh1jwB7Jgy4INm/yUrvgViOtkWiGxGcnS8zvvnnvPObJfiZBdUvBKhJDCFtny7MLbAnnQitKQF0pkmzNnm+x2PRsOfOYkWPVPiZTG89T3j65CiItkS7fJU0liqgbMFmSZWYJU16qCJtcUQalVRY0xy2ZMRd4wjhYQcw+SXVLkcM3TGJL16CJ5cMr8FMgfkPXEybkr2uc938p0FffHY9E/0meLuh6eZmZLV/UAz7tNtZ9p/6lVODtT/Aup507CV6kZnIp9aeRDd1S1gpPLSWCIxuzi5qhtiYP2PDMCvToJBsFg1ptN2r43menS5OzUvZjNry9mPX9w05sPAkMyDk/kvGpwduAaN45s3MwVo3aiTNqOqIdVbfrq5Ut0UCIPbS9Z+CzbJTv9KIYigl+Sj1bLRisKTMbpEDhCn66W9TGPIXS4u09XS+t5VSGfIdiKPU5bLgst2ODKHvn8Dm+zgDl3uLhHvsKD76IwTWiTc2bN6XgBYG/OJSTmUw4gjiyPZznKntcUHP7JaqkaEFueD0lCx1feAsgzLMXnkHnhhi7u5evt73/Ruw54/ALZ6EXtQhDFsYtiSGVDNMAHoGwtI6Ex2KkF1AXme6FDzQx7qQoqeowrVk+jOJdTFZN3TT9GvLmAazpEKuNRTIRN3za22OQjrhXV7XUGlDmoNOGUs9gBnqzLrzzu0jCiEXchpkFkJjSH1o5YVcnZph+hvnzis7UN36Y6hyAPR2mhTRc4PfYcpA/9NHE9IN/mqcQs5AnWAMZ6+9sv9P+hv/PxaLU0V0vfaJ7Tfue0088NN25//ZNKYkWT5IpUrdHzIRZ+s7YHI89xuWD5HvbhEWW2TVFwQheoA/dZlMbkQ6zL9wGE6debGZih3+8cdgbt5uiiSHYGLADyBFt+37/0KUb1Og98bQQ/yUedax4zVBt7ZsohKZKH2E8Pp1HhyY9lni2gvF8+1g87o3KlzCzuXSIwZX4ClTJcL8r70gsRxaN2sabURKXRkDv1Shl/4BiJ9zqQ7OINkfe9p7uYcB4w1rVGR61u5/jNetCbYf9k3NU7yLFYaGcnCdjlfbFSTlMPX8rTqQSybGqCKJl4ndSsusC0Wl2omrKqqqJmKlMLuWno/ZCCnjOURk2qg60Jat2SBHla0wSmNKaCZgPTQJU0BSdsFK5Db+WZ3+sMIyO6d/1TMb8Byfawc4zZ5orI0/cRtEN2uRdAwlmwwMvs7d/Nn78gZIt8sPlvyTYh/wIXh8R1aAUAAA\u003d\u003d"));
                    //Console.WriteLine (JsonConvert.SerializeObject (.Instance.Items.Where (item => item.Value.AltNames != null && item.Value.AltNames.Count > 3 && !item.Key.Contains("DRAGON")).Select((item)=>new P(item.Value))));
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
                    catch (Exception e) { Logger.Instance.Error(e, "Exited asp.net"); }
                    await Task.Delay(2000);
                }
            }).ConfigureAwait(false);

            var mode = SimplerConfig.SConfig.Instance["MODE"];
            var modes = SimplerConfig.SConfig.Instance["MODES"]?.Split(",");
            if (modes == null)
                modes = new string[] { "indexer", "updater", "flipper" };

            LightClient = modes.Contains("light");
            if (LightClient)
            {


                Console.WriteLine("running on " + System.Net.Dns.GetHostName());
                Thread.Sleep(Timeout.Infinite);
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

            Thread.Sleep(Timeout.Infinite);

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
                    FillRedisCache();


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
            Thread.Sleep(TimeSpan.FromHours(1));
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
                        Thread.Sleep(10000);
                    }
                    // TODO: switch to .Migrate()
                    context.Database.Migrate();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Waiting for db creating in the background {e.Message} {e.InnerException?.Message}");
                Thread.Sleep(10000);
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
                    p.Id = Interlocked.Increment(ref highestId);
                    context.Players.Add(p);
                    context.SaveChanges();
                    return p.Id;
                }
            }
            catch (Exception e)
            {
                Logger.Instance.Error(e, "failed to save user");
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
            if (IsRatelimited())
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

            if (RequestsSinceStart == 2)
            {
                BlockedSince = DateTime.Now;
            }

            if (RequestsSinceStart < 600)
            {
                client = new RestClient("https://api.mojang.com/");
                request = new RestRequest($"user/profile/{uuid}", Method.Get);
                type = 1;
            }
            else if (RequestsSinceStart < 1200)
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

            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                // Shift out to another method
                RequestsSinceStart += 200;
            }
            if (response.StatusCode != System.Net.HttpStatusCode.OK)
            {
                Logger.Instance.Info(client.BuildUri(request) + $" returned {response.StatusCode} {response.Content.Truncate(100)}");
                await Task.Delay(5000); // backoff
                RequestsSinceStart += 50;
                return null;
            }
            if (response.Content == "")
            {
                Console.WriteLine("no content");
                return null;
            }

            if (type == 2)
            {
                if (response.Content == null)
                    Console.WriteLine("content null");
                return response.Content;
            }

            dynamic responseDeserialized = JsonConvert.DeserializeObject(response.Content);

            if (responseDeserialized == null || (responseDeserialized?.name == null) || responseDeserialized.name == null)
            {
                Logger.Instance.Error(client.BuildUri(request) + $" returned {response.StatusCode} {response.Content}");
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

        public static bool IsRatelimited()
        {
            return DateTime.Now.Subtract(new TimeSpan(0, 10, 0)) < BlockedSince && RequestsSinceStart >= 2400;
        }
    }

}