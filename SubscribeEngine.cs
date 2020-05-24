using System;
using System.Collections.Generic;
using System.Linq;
using dev;
using FirebaseAdmin;

namespace hypixel
{
    public class SubscribeEngine {

        public static void AddNew(SubscribeItem subscription)
        {
            using(var context = new HypixelContext())
            {
                context.SubscribeItem.Add(subscription);
                context.SaveChanges();
            }
        }

        private Dictionary<string,List<SubscribeItem>> outbid;
        private Dictionary<string,List<SubscribeItem>> PriceHigher;
        private Dictionary<string,List<SubscribeItem>> PriceLower;
        
        static SubscribeEngine()
        {
            
            
        }


        public SubscribeEngine()
        {
            using(var context = new HypixelContext())
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

        public void Outbid(SaveAuction auction, SaveBids oldBid,SaveBids newBid)
        {
            if(this.outbid.TryGetValue(oldBid.Bidder,out List<SubscribeItem> subscribers))
            {
                foreach (var item in subscribers)
                {
                    Notify(item,$"You got outbid by {newBid} on {auction.ItemName}");
                }
            }
        }

        public void NewAuction(SaveAuction auction)
        {
            if(this.PriceLower.TryGetValue(auction.Tag,out List<SubscribeItem> subscribers))
            {
                foreach (var item in subscribers)
                {
                    if(auction.StartingBid < item.Price)
                        Notify(item,$"There is a new Auction for {auction.ItemName} with Starting bid {auction.StartingBid} ");
                }
            }
        }

        public void PriceState(ProductInfo info)
        {
            if(this.PriceLower.TryGetValue(info.ProductId,out List<SubscribeItem> subscribers))
            {
                foreach (var item in subscribers)
                {
                    if(info.QuickStatus.BuyPrice < item.Price)
                        Notify(item,$"{item.ItemTag} is at {info.QuickStatus.BuyPrice} at Bazaar ");
                }
            }
            if(this.PriceHigher.TryGetValue(info.ProductId,out List<SubscribeItem> higherSubscribers))
            {
                foreach (var item in higherSubscribers)
                {
                    if(info.QuickStatus.SellPrice > item.Price)
                        Notify(item,$"{item.ItemTag} is at {info.QuickStatus.SellPrice} at Bazaar ");
                }
            }
        }

        private void Notify(SubscribeItem subscription, string message)
        {
            Console.WriteLine("Notifications are not implemented yet");
        }
    }

}