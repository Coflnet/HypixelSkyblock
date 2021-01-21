using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using dev;
using Newtonsoft.Json;

namespace hypixel
{


    public class SubscribeEngine
    {



        private ConcurrentDictionary<string, ConcurrentBag<SubscribeItem>> outbid = new ConcurrentDictionary<string, ConcurrentBag<SubscribeItem>>();
        private ConcurrentDictionary<string, ConcurrentBag<SubscribeItem>> Sold = new ConcurrentDictionary<string, ConcurrentBag<SubscribeItem>>();
        private ConcurrentDictionary<string, ConcurrentBag<SubscribeItem>> PriceUpdate = new ConcurrentDictionary<string, ConcurrentBag<SubscribeItem>>();
        private ConcurrentDictionary<string, ConcurrentBag<SubscribeItem>> AuctionSub = new ConcurrentDictionary<string, ConcurrentBag<SubscribeItem>>();



        public static SubscribeEngine Instance { get; }

        private ConcurrentDictionary<int, UpdateSumary> NextNotifications = new ConcurrentDictionary<int, UpdateSumary>();

        static SubscribeEngine()
        {
            Instance = new SubscribeEngine();

        }


        public SubscribeEngine()
        {
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
                var minTime = DateTime.Now.Subtract(new TimeSpan(7, 0, 0));
                var all = context.SubscribeItem.Where(si => si.GeneratedAt > minTime);
                foreach (var item in all)
                {
                    AddSubscription(context, item);
                }
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
                        && (item.Type.HasFlag(SubscribeItem.SubType.BIN) || !auction.Bin))
                        AddNotifyItem(item, auction);
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
            AddToSumary(this.Sold, key, sumary =>
             {
                 sumary.Sold(auction.Tag, (int)auction.HighestBidAmount, auction.Bids.FirstOrDefault()?.Bidder, auction.Uuid);
             });
            AddToSumary(this.AuctionSub, key, sumary =>
             {
                 sumary.AuctionOver(auction.Tag, auction.Bids.FirstOrDefault()?.Bidder, auction.Uuid);
             });


        }

        private void AddToSumary(ConcurrentDictionary<string, ConcurrentBag<SubscribeItem>> target, string key, Action<UpdateSumary> todo)
        {
            if (target.TryGetValue(key, out ConcurrentBag<SubscribeItem> subscribers))
            {
                foreach (var item in subscribers)
                {
                    var sumary = GetSumary(item);
                    todo(sumary);
                }
            }
        }

        /// <summary>
        /// Called from the <see cref="Indexer"/>
        /// </summary>
        /// <param name="auction"></param>
        public void NewBids(SaveAuction auction)
        {
            foreach (var bid in auction.Bids.Skip(1))
            {
                AddToSumary(this.outbid, bid.Bidder, sumary =>
                {
                    var amount = auction.HighestBidAmount - bid.Amount;
                    sumary.OutBid(auction.Tag, amount, auction.Bids.FirstOrDefault().Bidder, auction.Uuid);
                });

                AddToSumary(this.AuctionSub, bid.Bidder, sumary =>
                {
                    var amount = auction.HighestBidAmount - bid.Amount;
                    sumary.NewBid(auction.Tag, (int)amount, auction.Bids.FirstOrDefault().Bidder, auction.Uuid);
                });
            }
        }


        private void AddNotifyItem(SubscribeItem item, SaveAuction auction)
        {
            var sumary = GetSumary(item);
            sumary.Items.Add(new UpdateSumary.HypixelEvent(auction.Tag, auction.StartingBid, "/auction/" + auction.Uuid));
        }

        private UpdateSumary GetSumary(SubscribeItem item)
        {
            return NextNotifications.GetOrAdd(item.UserId, userId => new UpdateSumary());
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
                        var sumary = GetSumary(item);
                        item.NotTriggerAgainBefore = DateTime.Now + BazzarNotificationBackoff;
                        sumary.Items.Add(new UpdateSumary.HypixelEvent(info.ProductId, (long)value));
                    }
                }
            }
        }

        public async Task SendNotifications()
        {
            while (NextNotifications.Count > 0)
            {
                var key = NextNotifications.First().Key;
                if (NextNotifications.TryRemove(key, out UpdateSumary value))
                {
                    await ProccessNotification(key, value);
                }

            }
        }

        private async Task ProccessNotification(int userId, UpdateSumary value)
        {
            var text = "";
            var prefix = "https://sky.coflnet.com";
            string gotoUrl = null;
            foreach (var item in value.Events)
            {
                if (item.Amount == -1)
                    text += $"Auction for {ItemDetails.TagToName(item.ItemTag)} by {item.Player} is over\n";
                else
                    text += $"New bid ({item.Amount} coins) on {ItemDetails.TagToName(item.ItemTag)} by {item.Player}\n";
                gotoUrl = prefix + item.LinkPath;
            }
            foreach (var item in value.Items)
            {
                text += $"Price alert: {ItemDetails.TagToName(item.ItemTag)} for {item.Amount}\n";
                if (gotoUrl == null)
                    if (item.LinkPath == null)
                        gotoUrl = prefix + "/item/" + item.ItemTag + "?hourly=true";
                    else
                        gotoUrl = prefix + item.LinkPath;
            }
            foreach (var item in value.OutBids)
            {
                text += $"You were outbid on {ItemDetails.TagToName(item.ItemTag)} by {item.Amount} by {item.Player}\n";
                gotoUrl = prefix + item.LinkPath;
            }
            foreach (var item in value.Solds)
            {
                text += $"You sold {ItemDetails.TagToName(item.ItemTag)} for {item.Amount} to {item.Player}\n";
                gotoUrl = prefix + item.LinkPath;
            }
            if (gotoUrl == null)
                gotoUrl = prefix;

            await NotificationService.Instance.Send(userId, text, gotoUrl);
        }

        private ConcurrentDictionary<string, List<SubLookup>> OnlineSubscriptions = new ConcurrentDictionary<string, List<SubLookup>>();
        private ConcurrentQueue<UnSub> ToUnsubscribe = new ConcurrentQueue<UnSub>();

        public int SubCount => OnlineSubscriptions.Count;

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

        public void PushOrIgnore(SaveAuction auction)
        {
            NotifyChange(auction.Uuid, auction);
            NotifyChange(auction.AuctioneerId, auction);
            foreach (var bids in auction.Bids)
            {
                NotifyChange(bids.Bidder, auction);
            }

        }

        public void PushOrIgnore(IEnumerable<SaveAuction> auctions)
        {
            Task.Run(() =>
            {
                foreach (var auction in auctions)
                {
                    PushOrIgnore(auction);
                }
            });
        }

        public void Subscribe(string topic, SkyblockBackEnd connection)
        {
            if (connection.Id == 0)
                throw new CoflnetException("id_not_set", "There is no `id` set on this connection. To Subscribe you need to pass a random generated id (32 char long) via get parameter (/skyblock?id=uuid) or cookie id");
            var lookup = new SubLookup(connection.Id);
            OnlineSubscriptions.AddOrUpdate(topic.Truncate(32),
            new List<SubLookup>() { lookup },
            (key, list) =>
            {
                RemoveFirstIfExpired(list);
                list.Add(lookup);
                return list;
            });
        }

        public void Unsubscribe(string topic, SkyblockBackEnd connection)
        {
            ToUnsubscribe.Enqueue(new UnSub(topic, connection.Id));
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