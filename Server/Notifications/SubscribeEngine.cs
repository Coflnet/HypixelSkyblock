using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Confluent.Kafka;
using dev;
using Newtonsoft.Json;

namespace hypixel
{


    public class SubscribeEngine
    {
        /// <summary>
        /// Subscriptions for being outbid
        /// </summary>
        private ConcurrentDictionary<string, ConcurrentBag<SubscribeItem>> outbid = new ConcurrentDictionary<string, ConcurrentBag<SubscribeItem>>();
        /// <summary>
        /// Subscriptions for ended auctions
        /// </summary>
        private ConcurrentDictionary<string, ConcurrentBag<SubscribeItem>> Sold = new ConcurrentDictionary<string, ConcurrentBag<SubscribeItem>>();
        /// <summary>
        /// Subscriptions for new auction/bazaar prices
        /// </summary>
        private ConcurrentDictionary<string, ConcurrentBag<SubscribeItem>> PriceUpdate = new ConcurrentDictionary<string, ConcurrentBag<SubscribeItem>>();
        /// <summary>
        /// All subscrptions to a specific auction
        /// </summary>
        private ConcurrentDictionary<string, ConcurrentBag<SubscribeItem>> AuctionSub = new ConcurrentDictionary<string, ConcurrentBag<SubscribeItem>>();
        private ConcurrentDictionary<string, ConcurrentBag<SubscribeItem>> UserAuction = new ConcurrentDictionary<string, ConcurrentBag<SubscribeItem>>();



        public static SubscribeEngine Instance { get; }



        static SubscribeEngine()
        {
            Instance = new SubscribeEngine();
        }
        ConsumerConfig conf = new ConsumerConfig
        {
            GroupId = "sky-sub-engine",
            BootstrapServers = Program.KafkaHost,
            AutoOffsetReset = AutoOffsetReset.Earliest
        };

        public async Task ProcessQueues()
        {
            var topics = new string[] { Indexer.AuctionEndedTopic, Indexer.SoldAuctionTopic, Indexer.MissingAuctionsTopic };
            ProcessSubscription<SaveAuction>(topics,BinSold);
            ProcessSubscription<SaveAuction>(new string[]{Indexer.NewAuctionsTopic},NewAuction);
            ProcessSubscription<BazaarPull>(new string[]{BazaarUpdater.ConsumeTopic},NewBazaar);
            ProcessSubscription<SaveAuction>(new string[]{Indexer.NewBidTopic},NewBids);
        }

        private void ProcessSubscription<T>(string[] topics, Action<T> handler,int timeout = 50)
        {
            using (var c = new ConsumerBuilder<Ignore, T>(conf).SetValueDeserializer(SerializerFactory.GetDeserializer<T>()).Build())
            {
                c.Subscribe(topics);
                try
                {
                    while (true)
                    {
                        try
                        {
                            var cr = c.Consume(timeout);
                            if (cr == null)
                                continue;
                            handler(cr.Message.Value);
                            // tell kafka that we stored the batch
                            c.Commit(new TopicPartitionOffset[] { cr.TopicPartitionOffset });
                        }
                        catch (ConsumeException e)
                        {
                            Console.WriteLine($"Error occured: {e.Error.Reason}");
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

        public void AddNew(SubscribeItem subscription)
        {
            using (var context = new HypixelContext())
            {
                context.SubscribeItem.Add(subscription);
                context.SaveChanges();
                AddSubscription(context, subscription);
            }
        }

        public async Task<int> Unsubscribe(int userId, string topic, SubscribeItem.SubType type)
        {
            using (var context = new HypixelContext())
            {
                var subs = context.SubscribeItem.Where(s => s.UserId == userId && s.TopicId == topic && s.Type == type).FirstOrDefault();
                if (subs != null)
                    context.SubscribeItem.Remove(subs);

                if (type.HasFlag(SubscribeItem.SubType.PRICE_HIGHER_THAN) || type.HasFlag(SubscribeItem.SubType.PRICE_LOWER_THAN))
                    RemoveSubscriptionFromCache(userId, topic, type, PriceUpdate);
                if (type.HasFlag(SubscribeItem.SubType.SOLD))
                    RemoveSubscriptionFromCache(userId, topic, type, Sold);
                if (type.HasFlag(SubscribeItem.SubType.OUTBID))
                    RemoveSubscriptionFromCache(userId, topic, type, outbid);
                if (type.HasFlag(SubscribeItem.SubType.AUCTION))
                    RemoveSubscriptionFromCache(userId, topic, type, AuctionSub);

                return await context.SaveChangesAsync();
            }
        }

        private static void RemoveSubscriptionFromCache(int userId, string topic, SubscribeItem.SubType type, ConcurrentDictionary<string, ConcurrentBag<SubscribeItem>> target)
        {
            if (target.Remove(topic, out ConcurrentBag<SubscribeItem> value))
                target.AddOrUpdate(topic, (key) => RemoveOldElement(userId, topic, type, value),
                (key, old) => RemoveOldElement(userId, topic, type, old));
        }

        private static ConcurrentBag<SubscribeItem> RemoveOldElement(int userId, string topic, SubscribeItem.SubType type, ConcurrentBag<SubscribeItem> old)
        {
            return new ConcurrentBag<SubscribeItem>(old.Where(s => !SubscribeItemEqual(userId, topic, type, s)));
        }

        private static bool SubscribeItemEqual(int userId, string topic, SubscribeItem.SubType type, SubscribeItem s)
        {
            return (s.UserId == userId && s.TopicId == topic && s.Type == type);
        }

        public void LoadFromDb()
        {
            using (var context = new HypixelContext())
            {
                var minTime = DateTime.Now.Subtract(TimeSpan.FromDays(200));
                var all = context.SubscribeItem.Where(si => si.GeneratedAt > minTime);
                foreach (var item in all)
                {
                    AddSubscription(context, item);
                }
                Console.WriteLine($"Loaded {all.Count()} subscriptions");
            }
        }

        private void AddSubscription(HypixelContext context, SubscribeItem item)
        {
            if (item.Type.HasFlag(SubscribeItem.SubType.OUTBID))
            {
                AddSubscription(item, outbid);
            }
            else if (item.Type.HasFlag(SubscribeItem.SubType.SOLD))
            {
                AddSubscription(item, Sold);
            }
            else if (item.Type.HasFlag(SubscribeItem.SubType.PRICE_LOWER_THAN) || item.Type.HasFlag(SubscribeItem.SubType.PRICE_HIGHER_THAN))
            {
                AddSubscription(item, PriceUpdate);
            }
            else if (item.Type.HasFlag(SubscribeItem.SubType.AUCTION))
            {
                AddSubscription(item, AuctionSub);
            }
            else if (item.Type.HasFlag(SubscribeItem.SubType.PLAYER))
            {
                AddSubscription(item, UserAuction);
            }
            else
                Console.WriteLine("ERROR: unkown subscibe type " + item.Type);
        }

        private static void AddSubscription(SubscribeItem item, ConcurrentDictionary<string, ConcurrentBag<SubscribeItem>> target)
        {
            var itemId = item.TopicId;
            var priceChange = target.GetOrAdd(itemId, itemId => new ConcurrentBag<SubscribeItem>());
            priceChange.Add(item);
        }

        internal void NewBazaar(BazaarPull pull)
        {
            foreach (var item in pull.Products)
            {
                PriceState(item);
            }
        }

        /// <summary>
        /// Called from <see cref="Updater"/>
        /// </summary>
        /// <param name="auction"></param>
        public void NewAuction(SaveAuction auction)
        {
            if (this.PriceUpdate.TryGetValue(auction.Tag, out ConcurrentBag<SubscribeItem> subscribers))
            {
                foreach (var item in subscribers)
                {
                    if ((auction.StartingBid < item.Price && item.Type.HasFlag(SubscribeItem.SubType.PRICE_LOWER_THAN)
                        || auction.StartingBid > item.Price && item.Type.HasFlag(SubscribeItem.SubType.PRICE_HIGHER_THAN))
                        && (!item.Type.HasFlag(SubscribeItem.SubType.BIN) || auction.Bin))
                        NotificationService.Instance.AuctionPriceAlert(item, auction);
                }
            }
            if (this.UserAuction.TryGetValue(auction.AuctioneerId, out subscribers))
            {
                foreach (var item in subscribers)
                {
                    item.NotifyAuction(auction);
                }
            }
        }

        /// <summary>
        /// Called from <see cref="BinUpdater"/>
        /// </summary>
        /// <param name="auction"></param>
        public void BinSold(SaveAuction auction)
        {
            var key = auction.AuctioneerId;
            NotifyIfExisting(this.Sold, key, sub =>
            {
                NotificationService.Instance.Sold(sub, auction);
            });
            NotifyIfExisting(this.AuctionSub, auction.Uuid, sub =>
            {
                NotificationService.Instance.AuctionOver(sub, auction);
            });
        }

        private void NotifyIfExisting(ConcurrentDictionary<string, ConcurrentBag<SubscribeItem>> target, string key, Action<SubscribeItem> todo)
        {
            if (target.TryGetValue(key, out ConcurrentBag<SubscribeItem> subscribers))
            {
                foreach (var item in subscribers)
                {
                    todo(item);
                }
            }
        }

        /// <summary>
        /// Called from the <see cref="Indexer"/>
        /// </summary>
        /// <param name="auction"></param>
        public void NewBids(SaveAuction auction)
        {
            foreach (var bid in auction.Bids.OrderByDescending(b => b.Amount).Skip(1))
            {
                NotifyIfExisting(this.outbid, bid.Bidder, sub =>
                {
                    NotificationService.Instance.Outbid(sub, auction, bid);
                });
            }
            NotifyIfExisting(this.AuctionSub, auction.Uuid, sub =>
            {
                NotificationService.Instance.NewBid(sub, auction, auction.Bids.OrderBy(b => b.Amount).Last());
            });
            foreach (var bid in auction.Bids)
            {
                NotifyIfExisting(UserAuction, bid.Bidder, sub =>
                {
                    sub.NotifyAuction(auction);
                });
            }
        }



        /// <summary>
        /// Called from <see cref="BazaarUpdater"/>
        /// </summary>
        /// <param name="info"></param>
        public void PriceState(ProductInfo info)
        {
            if (this.PriceUpdate.TryGetValue(info.ProductId, out ConcurrentBag<SubscribeItem> subscribers))
            {
                foreach (var item in subscribers)
                {
                    if (item.NotTriggerAgainBefore > DateTime.Now)
                        continue;
                    var value = info.QuickStatus.BuyPrice;
                    if (item.Type.HasFlag(SubscribeItem.SubType.USE_SELL_NOT_BUY))
                        value = info.QuickStatus.SellPrice;
                    if (value < item.Price && item.Type.HasFlag(SubscribeItem.SubType.PRICE_LOWER_THAN)
                         || value > item.Price && item.Type.HasFlag(SubscribeItem.SubType.PRICE_HIGHER_THAN))
                    {
                        if (item.NotTriggerAgainBefore < DateTime.Now)
                            return;
                        item.NotTriggerAgainBefore = DateTime.Now + TimeSpan.FromHours(1);
                        NotificationService.Instance.PriceAlert(item, info.ProductId, value);
                    }
                }
            }
        }


        private ConcurrentDictionary<string, List<SubLookup>> OnlineSubscriptions = new ConcurrentDictionary<string, List<SubLookup>>();
        private ConcurrentQueue<UnSub> ToUnsubscribe = new ConcurrentQueue<UnSub>();

        public int SubCount => outbid.Count + Sold.Count + PriceUpdate.Count;

        public static TimeSpan BazzarNotificationBackoff = TimeSpan.FromHours(1);

        public void NotifyChange(string topic, SaveAuction auction)
        {
            GenericNotifyAll(topic, "updateAuction", auction);
        }

        public void NotifyChange(string topic, ProductInfo bazzarUpdate)
        {
            GenericNotifyAll(topic, "bazzarUpdate", bazzarUpdate);
        }

        private void GenericNotifyAll<T>(string topic, string commandType, T data)
        {
            if (OnlineSubscriptions.TryGetValue(topic.Truncate(32), out List<SubLookup> value))
                foreach (var sub in value)
                {
                    var resultJson = JsonConvert.SerializeObject(data);
                    if (!SkyblockBackEnd.SendTo(new MessageData(commandType, resultJson), sub.Id))
                        // could not be reached, unsubscribe
                        ToUnsubscribe.Enqueue(new UnSub(topic, sub.Id));
                }
        }

        public void Subscribe(string topic, int userId)
        {
            if (userId == 0)
                throw new CoflnetException("id_not_set", "There is no `id` set on this connection. To Subscribe you need to pass a random generated id (32 char long) via get parameter (/skyblock?id=uuid) or cookie id");
            var lookup = new SubLookup(userId);
            OnlineSubscriptions.AddOrUpdate(topic.Truncate(32),
            new List<SubLookup>() { lookup },
            (key, list) =>
            {
                RemoveFirstIfExpired(list);
                list.Add(lookup);
                return list;
            });
        }

        public void Unsubscribe(string topic, int userId)
        {
            ToUnsubscribe.Enqueue(new UnSub(topic, userId));
            // unsubscribe stale elements
            while (ToUnsubscribe.TryDequeue(out UnSub result))
            {
                lock (result.Topic)
                {
                    if (OnlineSubscriptions.TryGetValue(result.Topic.Truncate(32), out List<SubLookup> value))
                    {
                        var item = value.Where(v => v.Id == result.id).FirstOrDefault();
                        if (item.Id != 0)
                            value.Remove(item);
                    }
                }
            }
        }

        private static void RemoveFirstIfExpired(List<SubLookup> list)
        {
            if (list.Count > 0 && list.First().SubTime < DateTime.Now - TimeSpan.FromHours(1))
                list.RemoveAt(0);
        }


        private struct SubLookup
        {
            public DateTime SubTime;
            public long Id;

            public SubLookup(long id)
            {
                SubTime = DateTime.Now;
                Id = id;
            }
        }

        public class UnSub
        {
            public string Topic;
            public long id;

            public UnSub(string topic, long id)
            {
                Topic = topic;
                this.id = id;
            }
        }
    }


    public static class StringExt
    {
        public static string Truncate(this string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return value.Length <= maxLength ? value : value.Substring(0, maxLength);
        }
    }
}