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
                    Console.WriteLine(NBT.Pretty("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"));
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