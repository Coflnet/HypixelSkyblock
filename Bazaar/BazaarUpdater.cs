using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Coflnet;
using hypixel;
using Hypixel.NET;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using RestSharp;
using System.Threading.Tasks;
using System.Runtime.Serialization;
using Confluent.Kafka;

namespace dev
{
    public class BazaarUpdater
    {
        private bool abort;

        public static DateTime LastUpdate { get; internal set; }

        public static Dictionary<string, QuickStatus> LastStats = new Dictionary<string, QuickStatus>();

        public static readonly string ConsumeTopic = SimplerConfig.Config.Instance["TOPICS:BAZAAR_CONSUME"];
        public static readonly string ProduceTopic = SimplerConfig.Config.Instance["TOPICS:BAZAAR"];


        private static async Task PullAndSave(HypixelApi api, int i)
        {
            var result = await api.GetBazaarProductsAsync();
            var pull = new BazaarPull()
            {
                Timestamp = result.LastUpdated
            };
            pull.Products = result.Products.Select(p =>
            {
                var pInfo = new ProductInfo()
                {
                    ProductId = p.Value.ProductId,
                    BuySummery = p.Value.BuySummary.Select(s => new BuyOrder()
                    {
                        Amount = (int)s.Amount,
                        Orders = (short)s.Orders,
                        PricePerUnit = s.PricePerUnit
                    }).ToList(),
                    SellSummary = p.Value.SellSummary.Select(s => new SellOrder()
                    {
                        Amount = (int)s.Amount,
                        Orders = (short)s.Orders,
                        PricePerUnit = s.PricePerUnit
                    }).ToList(),
                    QuickStatus = new QuickStatus()
                    {
                        ProductId = p.Value.QuickStatus.ProductId,
                        BuyMovingWeek = p.Value.QuickStatus.BuyMovingWeek,
                        BuyOrders = (int)p.Value.QuickStatus.BuyOrders,
                        BuyPrice = p.Value.QuickStatus.BuyPrice,
                        BuyVolume = p.Value.QuickStatus.BuyVolume,
                        SellMovingWeek = p.Value.QuickStatus.SellMovingWeek,
                        SellOrders = (int)p.Value.QuickStatus.SellOrders,
                        SellPrice = p.Value.QuickStatus.SellPrice,
                        SellVolume = p.Value.QuickStatus.SellVolume
                    },
                    PullInstance = pull
                };
                pInfo.QuickStatus.SellPrice = p.Value.SellSummary.Select(o => o.PricePerUnit).FirstOrDefault();
                pInfo.QuickStatus.BuyPrice = p.Value.BuySummary.Select(o => o.PricePerUnit).FirstOrDefault();
                return pInfo;
            }).ToList();
            await ProduceIntoQueue(pull);
        }

        private static async Task IndexBazaar(int i, BazaarPull pull)
        {
            await Program.MakeSureRedisIsInitialized();
            await ItemPrices.FillLastHourIfDue();
            using (var context = new HypixelContext())
            {

                var lastMinPulls = await context.BazaarPull

                            .OrderByDescending(b => b.Timestamp)
                            .Include(b => b.Products)
                            .ThenInclude(p => p.QuickStatus)
                            .Take(8).ToListAsync();

                if (i == 2)
                {
                    UpdateItemBazaarState(pull, context);
                }
                if (lastMinPulls.Any())
                {
                    RemoveRedundandInformation(i, pull, context, lastMinPulls);
                }

                context.BazaarPull.Add(pull);
                await context.SaveChangesAsync();
                Console.Write("\r" + i);

            }

            LastStats = pull.Products.Select(p => p.QuickStatus).ToDictionary(qs => qs.ProductId);
            LastUpdate = DateTime.Now;
        }

        private static void UpdateItemBazaarState(BazaarPull pull, HypixelContext context)
        {
            var names = pull.Products.Select(p => p.ProductId).ToList();
            var bazaarItems = context.Items.Where(i => names.Contains(i.Tag));
            foreach (var item in bazaarItems)
            {
                if (item.IsBazaar)
                    continue;
                item.IsBazaar = true;
                context.Update(item);
            }
        }

        private static void RemoveRedundandInformation(int i, BazaarPull pull, HypixelContext context, List<BazaarPull> lastMinPulls)
        {
            var lastPull = lastMinPulls.First();
            var lastPullDic = lastPull
                    .Products.ToDictionary(p => p.ProductId);

            var sellChange = 0;
            var buyChange = 0;
            var productCount = pull.Products.Count;

            var toRemove = new List<ProductInfo>();

            for (int index = 0; index < productCount; index++)
            {
                var currentProduct = pull.Products[index];
                var currentStatus = currentProduct.QuickStatus;
                var lastProduct = lastMinPulls.SelectMany(p => p.Products)
                                .Where(p => p.ProductId == currentStatus.ProductId)
                                .OrderByDescending(p => p.Id)
                                .FirstOrDefault();

                var lastStatus = new QuickStatus();
                if (lastProduct != null)
                {
                    lastStatus = lastProduct.QuickStatus;
                }
                // = lastPullDic[currentStatus.ProductId].QuickStatus;

                var takeFactor = i % 60 == 0 ? 30 : 3;

                if (currentStatus.BuyOrders == lastStatus.BuyOrders)
                {
                    // nothing changed
                    currentProduct.BuySummery = null;
                    buyChange++;
                }
                else
                {
                    currentProduct.BuySummery = currentProduct.BuySummery.Take(takeFactor).ToList();
                }
                if (currentStatus.SellOrders == lastStatus.SellOrders)
                {
                    // nothing changed
                    currentProduct.SellSummary = null;
                    sellChange++;
                }
                else
                {
                    currentProduct.SellSummary = currentProduct.SellSummary.Take(takeFactor).ToList();
                }
                if (currentProduct.BuySummery == null && currentProduct.SellSummary == null)
                {
                    toRemove.Add(currentProduct);
                }
            }
            //Console.WriteLine($"Not saving {toRemove.Count}");

            foreach (var item in toRemove)
            {
                pull.Products.Remove(item);
            }

            Console.Write($"  BuyChange: {productCount - buyChange}  SellChange: {productCount - sellChange}");
            context.Update(lastPull);
        }

        private static async Task WaitForServerCacheRefresh(int i, DateTime start)
        {
            var timeToSleep = start.Add(new TimeSpan(0, 0, 0, 10)) - DateTime.Now;
            Console.Write($"\r {i} {timeToSleep}");
            if (timeToSleep.Seconds > 0)
                await Task.Delay(timeToSleep);
        }

        public void UpdateForEver(string apiKey)
        {
            HypixelApi api = null;
            Task.Run(async () =>
            {
                int i = 0;
                while (!abort)
                {
                    try
                    {
                        if (api == null)
                            api = new HypixelApi(apiKey, 9);
                        var start = DateTime.Now;
                        await PullAndSave(api, i);
                        await WaitForServerCacheRefresh(i, start);
                        i++;
                    }
                    catch (Exception e)
                    {
                        Logger.Instance.Error($"\nBazaar update failed {e.Message} \n{e.StackTrace} \n{e.InnerException?.Message}");
                        Console.WriteLine($"\nBazaar update failed {e.Message} \n{e.InnerException?.Message}");
                        await Task.Delay(5000);
                    }
                }
                Console.WriteLine("Stopped Bazaar :/");
            }).ConfigureAwait(false); ;
        }

        private static ProducerConfig producerConfig = new ProducerConfig { BootstrapServers = SimplerConfig.Config.Instance["KAFKA_HOST"] };

        private static async Task ProduceIntoQueue(BazaarPull pull)
        {
            using (var p = new ProducerBuilder<string, BazaarPull>(producerConfig).SetValueSerializer(SerializerFactory.GetSerializer<BazaarPull>()).Build())
            {
                var result = await p.ProduceAsync(ProduceTopic, new Message<string, BazaarPull> { Value = pull, Key = pull.Timestamp.ToString() });
                Console.WriteLine("wrote bazaar log " + result.TopicPartitionOffset.Offset);
            }
        }

        public async Task ProcessBazaarQueue()
        {
            var conf = new ConsumerConfig
            {
                GroupId = "sky-bazaar-indexer",
                BootstrapServers = Program.KafkaHost,
                // Note: The AutoOffsetReset property determines the start offset in the event
                // there are not yet any committed offsets for the consumer group for the
                // topic/partitions of interest. By default, offsets are committed
                // automatically, so in this example, consumption will only start from the
                // earliest message in the topic 'my-topic' the first time you run the program.
                AutoOffsetReset = AutoOffsetReset.Earliest,
            };

            using (var c = new ConsumerBuilder<Ignore, BazaarPull>(conf)
                        .SetValueDeserializer(SerializerFactory.GetDeserializer<BazaarPull>())
                        .Build())
            {
                c.Subscribe(ConsumeTopic);
                try
                {
                    var index = 0;
                    while (true)
                    {
                        try
                        {
                            var cr = c.Consume(5000);
                            if (cr == null)
                                continue;
                            await IndexBazaar(index++, cr.Message.Value);
                            // tell kafka that we stored the batch
                            c.Commit(new TopicPartitionOffset[] { cr.TopicPartitionOffset });
                        }
                        catch (ConsumeException e)
                        {
                            Console.WriteLine($"Error occured-bazaar: {e.Error.Reason}");
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // Ensure the consumer leaves the group cleanly and final offsets are committed.
                    c.Close();
                }
            }
        }

        internal void Stop()
        {
            abort = true;
        }
    }

    public class BazzarResponse
    {
        [JsonProperty("success")]
        public bool WasSuccessful { get; private set; }

        [JsonProperty("product_info")]
        public ProductInfo ProductInfo { get; private set; }
    }

    public class BazaarController
    {
        public static BazaarController Instance = new BazaarController();

        public IEnumerable<ProductInfo> GetInfo(string id)
        {
            return StorageManager.GetFileContents<ProductInfo>($"product/{id.Trim('"')}");
        }
    }
}