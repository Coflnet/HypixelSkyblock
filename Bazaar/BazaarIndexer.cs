using System;
using System.Collections.Generic;
using System.Linq;
using hypixel;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Threading.Tasks;
using Confluent.Kafka;

namespace dev
{
    public class BazaarIndexer
    {
        public static DateTime LastUpdate { get; internal set; }

        public static Dictionary<string, QuickStatus> LastStats = new Dictionary<string, QuickStatus>();

        public static readonly string ConsumeTopic = SimplerConfig.Config.Instance["TOPICS:BAZAAR"];

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
                AutoOffsetReset = AutoOffsetReset.Earliest
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
    }
}