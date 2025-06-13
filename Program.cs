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
                    Console.WriteLine(NBT.Pretty("H4sIAAAAAAAA/1VUyY7r1hFl93vP6W4YcLzIXgFsIIEgi4NIigGyICmJIsVBEieRG+OSvOI8iIMo6h+yySLIH/QmW/9Af4o/JAi7bRjOqoBzTp06BRTqBUGekYf4BUGQh0fkMQ4e/vGAfOHLrmgfXpBPLQg/Ic/bOICbDITNqPrvC/Kip12WaX0B6yfkUQyQ73wa8ykUX85wgiFnBKTIGRMw3gwjiQVkIA3wAHtA/hgNVXyD2b6sugy0MBgnvOzrsoJ1G8PmGXlq4a3tath8xHlCnvU4LMA78vgfYzmnJc/XNokCphFRJFkbnqwq27f7kBNOantkKoOUWRxQN7XCB04KnOG0SqpddXGS8yWRmtg4JtjQUEt7cPKE1yksLMyBy4rhMDcxnXdPTR2FpGq1u7vQQqe9LRcp7Too1h4c69ioxLLFrHzRXSthF/D+btPQpnQ52L66II/L5M6G8VRz6r1Eln5628hVq061ltblLh5KU9Yk/xzRkQlptieJebJhFSZO10vq1BnyGiQ9t7htQeSI1jFfz9X0cLEPt8BhzEFV6QCvBM8i5CUWq21o48S+9iBmYGtsmWZHsZcgFTq04q13952ZKw09JRc5KuV8LDiUtyTn6k5pOlUEG7LUFYU4HPA01vakfsw1Pz3XqCtvra3Nq8oevyeVlKh5T9peyQP9pMv+KW8MsEILv7evrebSorDsudQnSJwFDunpe/letKDzp5ttYKqy0w2Qcedc3EfDyusy1vdN91LY5TSiCfyqtAPeHfm42VxvgRbinkELsmCzps6ChZcPczu5bndF47ZnUzCdq3ydStawuRhofyBU4xbISbJGBW9R2PoeEjF6sOhhb0oc72tDO1+biyFgWOJ4pNx9k11KTQCNMeemzFpy0J6aUky1thJ+o1hCFDncihG86XWdXYvxhFzphO/2m0XTdt7qmF0v/uJa1JmosONe/iH3xCV0eZXzik6+y/P+dqHwu9djqrEs6etGl8y/PyFfLJB18OHfsC9DkZdQYGOZTxwj78TG4qoMFcO/qSsRVQxx0IwUU/V+J/Js7G+lq5tnjWtmqRizlMiLhCusB8cW746R3h18jWqClWkrpVdXUeTmyt1Jwt41nIWriw0fs6FYcIOHu5UnWJozzv3FR9p6+C11Tlzu2n0ssx+57sAOOud0xPzc0t3TBgMnKXN5MdRiDvULK/tVh7qnCA1Gzh8+OPpjzpjXRFvJiP8Pe9ePfv3HnmI+9m1ZSh6Y33mQLbDJzCGkyC0OnZdbqEwcM7h9z2FeFXtNuolCKrhDKoIVufZm3JJLlMQflFWKKis1cfPDTTV8QhPEm2KEhJqkmJtEmWNwkWarkWqYhGJsMtfYRNoqjX/JxjHv9XyomPG/vCB/COKmysDwjHyWyxo+jeAX5Pu3V8qIYQ2DCVcWXfO3CQ/qIr6CbMLX4A4nf8Hm5F+R2dsrLYC4mLy9guni+7HQ+egxVvib3ihTWDQj88Po/K54e10aUdxM4hbmEx8UEw9Oangu6xAGf0b+NFq9vWamymuKoqkTQdastf6EfFZBDpFvRnYP6naYCFl5hc2Y/5v1ra0B27Z17HUtbJ7e/zfy9Z49Gs6Pv3V33Qh+t1ieAYQYmJ19QM0WiwCfLRmSmJ1phkLRgILBAvuMPLdxDpsW5NX4f//17U8//xNBHpGvViAHIUQ+Icj/AFl18ncvBgAA"));
                    File.WriteAllText("xy", NBT.Pretty("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"));
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

            if (RequestsSinceStart < 200)
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
                client = new RestClient("https://playerdb.co/");
                request = new RestRequest($"/api/player/minecraft/{uuid}", Method.Get);
                type = 3;
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