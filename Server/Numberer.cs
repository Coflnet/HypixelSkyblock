using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace hypixel
{
    public class Numberer
    {
        internal static async Task NumberUsers()
        {

            Task bidNumberTask = null;
            using (var context = new HypixelContext())
            {
                var unindexedPlayers = await context.Players.Where(p => p.Id == 0).Take(2000).ToListAsync();
                if (unindexedPlayers.Any())
                {
                    Console.Write($"  numbering: {unindexedPlayers.Count()} ");
                    foreach (var player in unindexedPlayers)
                    {
                        player.Id = System.Threading.Interlocked.Increment(ref Indexer.highestPlayerId);
                        context.Players.Update(player);
                    }
                }
                else
                {
                    // all players in the db have an id now
                    bidNumberTask = Task.Run(NumberBids);
                    await NumberAuctions(context);
                }
                await context.SaveChangesAsync();
            }
            if (bidNumberTask != null)
                await bidNumberTask;
        }

        private static async Task NumberAuctions(HypixelContext context)
        {
            var auctionsWithoutSellerId = await context
                                    .Auctions.Where(a => a.SellerId == 0)
                                    .Include(a => a.Enchantments)
                                    .OrderByDescending(a => a.Id)
                                    .Take(5000).ToListAsync();
            if (auctionsWithoutSellerId.Count() > 0)
                Console.Write(" -#-");
            foreach (var auction in auctionsWithoutSellerId)
            {

                try
                {
                    NumberAuction(context, auction);

                }
                catch (Exception e)
                {
                    Console.WriteLine($"Problem with item {Newtonsoft.Json.JsonConvert.SerializeObject(auction)}");
                    Console.WriteLine($"Error occured while userIndexing: {e.Message} {e.StackTrace}\n {e.InnerException?.Message} {e.InnerException?.StackTrace}");
                }
            }
        }

        private static void NumberAuction(HypixelContext context, SaveAuction auction)
        {
            auction.SellerId = GetOrCreatePlayerId(context, auction.AuctioneerId);
            var id = ItemDetails.Instance.GetOrCreateItemIdForAuction(auction, context);
            auction.ItemId = id;


            foreach (var enchant in auction.Enchantments)
            {
                enchant.ItemType = auction.ItemId;
            }
            context.Auctions.Update(auction);
        }

        static int batchSize = 10000;

        private static async void NumberBids()
        {
            using (var context = new HypixelContext())
            {
                var bidsWithoutSellerId = await context.Bids.Where(a => a.BidderId == 0).Take(batchSize).ToListAsync();
                foreach (var bid in bidsWithoutSellerId)
                {

                    bid.BidderId = GetOrCreatePlayerId(context, bid.Bidder);
                    context.Bids.Update(bid);
                }

                await context.SaveChangesAsync();
            }
        }

        private static int GetOrCreatePlayerId(HypixelContext context, string uuid)
        {
            var id = context.Players.Where(p => p.UuId == uuid).Select(p => p.Id).FirstOrDefault();
            if (id == 0)
            {
                id = Program.AddPlayer(context, uuid, ref Indexer.highestPlayerId);
                Console.WriteLine($"Adding player {id} ");
            }
            return id;
        }
    }
}