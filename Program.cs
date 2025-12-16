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
                    NBT.FillDetails(af, "H4sIAAAAAAAAAFVTW27aQBS91EDBJI3Uh5SvatI2atOKihICSb5CgFAkSlIgfXyhsT22R7Fn6HicNBvoFip1A+yDpXQhVe9goqr8cH187j33cWwDlOAe92Az5oK5ivr62JE3OSh0ZCp0zgZL06AMeSbcEMwvB5VL4ShGr6gTsZwF5ffcY2cRDRJ8+8eG+x5P5hG9xaShVKyE6DY8XC5aXRrTgB2T5cJ9865Zg0eITbRiItBhhh7UYAfBjuKadEIq3DW7XtvF/6NXJtj7R/mv4MGagsEeYNgaCM2iiAdsXYZi9l2VPWxqE+NMhAwGA6iYx9ThSUw+mXaPuooGUpCpoi5TiNmI4Zgx0uExxgPhc8E1Ix9Tfr1imIoXnCmXiwBJJuFC3qxebWA8EXyeaYGZvf775w/SZzEZp+IO3l4umm2HR1zfHqMyn0eMTEKpCTw3ywql1AnZJ1QpeZMQqgklmsdsh0xDRuqwiyT2XSt6x/AYjczojdUCW9InOmTwFENvtTtChUdCiTNpSTRVAdPJW+zj2XJxuFxErzHpsCOrck4mMo0ctIRH1q/MCE0Mhr1+b9Rtj7+S0/PPJciPcENmP80xd0MzG6XiZUJO5Q3YsNUz3bW1VtxJNUtsKCicPrHA6vc+oLCFdizF0uM+7hFKCmvM0I8lKEvFAy6mNIAnnXH7bDoY9Wf98aA765wPh73ONLNxZXw5ardHkxk2Y8OGcS0VOmZCo0bRXZ3byKDOfH0p42kLtvj6nLNvq3MiWrCgMDf3y+IK2old4VeS8CTLKSTmolm5gm+skeFFd+WjLK1IeZypFHC2fJpily9q9Ybve0es6jOnVm0cuLR62Gg1q7Vm06l7eLV6a98Cm4prHs3ShBnBezl44ElBNfNmMWJpnMtD2Zw/0TSeo3J88uvLCRKhmH0ZmPQXz6UveuADAAA=", true);
                    Console.WriteLine(JsonConvert.SerializeObject(af, Formatting.Indented));
                    var toprint = "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";  Console.WriteLine(NBT.Pretty(toprint));
                    File.WriteAllText("xy", NBT.Pretty(toprint));
                    // //Console.WriteLine (JsonConvert.SerializeObject (.Instance.Items.Where (item => item.Value.AltNames != null && item.Value.AltNames.Count > 3 && !item.Key.Contains("DRAGON")).Select((item)=>new P(item.Value))));
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


            onStop += () =>
            {
                Console.WriteLine("stopped");
            };
            Thread.Sleep(Timeout.Infinite);
        }

        static CancellationTokenSource fillRedisCacheTokenSource;
        private static void FillRedisCache()
        {
            fillRedisCacheTokenSource?.Cancel();
            fillRedisCacheTokenSource = new CancellationTokenSource();
            Task.Run(async () =>
            {
                try
                {
                    //   await ItemPrices.Instance.FillHours(fillRedisCacheTokenSource.Token);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Backfill failed :( \n{e.Message}\n {e.InnerException?.Message} {e.StackTrace}");
                }
            }, fillRedisCacheTokenSource.Token).ConfigureAwait(false); ;
        }

        private static async Task MakeSureRedisIsInitialized()
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
                    catch (TaskCanceledException)
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

        public static void ResetRequestsSinceStart()
        {
            RequestsSinceStart = 0;
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
            else if (RequestsSinceStart < 1750)
            {
                client = new RestClient("https://minecraft-api.com/api/uuid/pseudo.php?uuid=");
                request = new RestRequest($"/api/uuid/pseudo.php?uuid={uuid}", Method.Get);
                type = 2;
            }
            else
            {
                // updates slowly
                client = new RestClient("https://playerdb.co/");
                request = new RestRequest($"/api/player/minecraft/{uuid}", Method.Get);
                type = 3;
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
            if (type == 3)
            {
                var data = JsonConvert.DeserializeObject<PlayerSearch.PlayerDbResponse>(response.Content);
                return data?.Data?.Player.Username ?? null;
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