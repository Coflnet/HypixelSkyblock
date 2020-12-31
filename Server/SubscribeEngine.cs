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

        public static void AddNew(SubscribeItem subscription)
        {
            using (var context = new HypixelContext())
            {
                context.SubscribeItem.Add(subscription);
                context.SaveChanges();
            }
        }

        private Dictionary<string, List<SubscribeItem>> outbid;
        private Dictionary<string, List<SubscribeItem>> PriceHigher;
        private Dictionary<string, List<SubscribeItem>> PriceLower;

        public static SubscribeEngine Instance { get; }

        static SubscribeEngine()
        {
            Instance = new SubscribeEngine();

        }


        public SubscribeEngine()
        {
        }

        private void LoadFromDb()
        {
            using (var context = new HypixelContext())
            {
                var all = context.SubscribeItem.Where(si => si.GeneratedAt > DateTime.Now.Subtract(new TimeSpan(7, 0, 0)));
                LoadOutbid(all);

                LoadPriceHigher(all);

                LoadPriceLower(all);
            }
        }

        private void LoadOutbid(IQueryable<SubscribeItem> all)
        {
            var outbid = all.Where(si => si.Type == SubscribeItem.SubType.OUTBID).GroupBy(si => si.PlayerUuid);
            foreach (var item in outbid)
            {
                this.outbid[item.Key] = item.ToList();
            }
        }

        private void LoadPriceHigher(IQueryable<SubscribeItem> all)
        {
            var priceHigher = all.Where(si => si.Type == SubscribeItem.SubType.PRICE_HIGHER_THAN)
                                                .GroupBy(si => si.ItemTag);
            foreach (var item in priceHigher)
            {
                this.PriceHigher[item.Key] = item.ToList();
            }
        }

        private void LoadPriceLower(IQueryable<SubscribeItem> all)
        {
            var priceLower = all.Where(si => si.Type == SubscribeItem.SubType.PRICE_HIGHER_THAN)
                                                .GroupBy(si => si.ItemTag);
            foreach (var item in priceLower)
            {
                this.PriceLower[item.Key] = item.ToList();
            }
        }

        public void Outbid(SaveAuction auction, SaveBids oldBid, SaveBids newBid)
        {
            if (this.outbid.TryGetValue(oldBid.Bidder, out List<SubscribeItem> subscribers))
            {
                foreach (var item in subscribers)
                {
                    Notify(item, $"You got outbid by {newBid} on {auction.ItemName}");
                }
            }
        }

        public void NewAuction(SaveAuction auction)
        {
            if (this.PriceLower.TryGetValue(auction.Tag, out List<SubscribeItem> subscribers))
            {
                foreach (var item in subscribers)
                {
                    if (auction.StartingBid < item.Price)
                        Notify(item, $"There is a new Auction for {auction.ItemName} with Starting bid {auction.StartingBid} ");
                }
            }
        }

        public void PriceState(ProductInfo info)
        {
            if (this.PriceLower.TryGetValue(info.ProductId, out List<SubscribeItem> subscribers))
            {
                foreach (var item in subscribers)
                {
                    if (info.QuickStatus.BuyPrice < item.Price)
                        Notify(item, $"{item.ItemTag} is at {info.QuickStatus.BuyPrice} at Bazaar ");
                }
            }
            if (this.PriceHigher.TryGetValue(info.ProductId, out List<SubscribeItem> higherSubscribers))
            {
                foreach (var item in higherSubscribers)
                {
                    if (info.QuickStatus.SellPrice > item.Price)
                        Notify(item, $"{item.ItemTag} is at {info.QuickStatus.SellPrice} at Bazaar ");
                }
            }
        }

        private void Notify(SubscribeItem subscription, string message)
        {
            Console.WriteLine("Notifications are not implemented yet");
        }

        private ConcurrentDictionary<string, List<SubLookup>> OnlineSubscriptions = new ConcurrentDictionary<string, List<SubLookup>>();
        private ConcurrentQueue<UnSub> ToUnsubscribe = new ConcurrentQueue<UnSub>();

        public void NotifyChange(string topic, SaveAuction auction)
        {
            if (OnlineSubscriptions.TryGetValue(topic.Truncate(32), out List<SubLookup> value))
                foreach (var sub in value)
                {
                    var resultJson = JsonConvert.SerializeObject(auction);
                    if (!SkyblockBackEnd.SendTo(new MessageData("updateAuction", resultJson), sub.Id))
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
            if(connection.Id == 0)
                throw new CoflnetException("id_not_set","There is no `id` set on this connection. To Subscribe you need to pass a random generated id (32 char long) via get parameter (/skyblock?id=uuid) or cookie id");
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
                        var item = value.Where(v=>v.Id == result.id).FirstOrDefault();
                        if(item.Id != 0)
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