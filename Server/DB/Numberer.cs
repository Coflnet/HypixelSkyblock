using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace hypixel
{
    public class Numberer
    {
        public static async Task NumberUsers()
        {

            Task bidNumberTask = null;
            using (var context = new HypixelContext())
            {
                for (int i = 0; i < 3; i++)
                {
                    var doublePlayersId = await context.Players.GroupBy(p => p.Id).Where(p => p.Count() > 1).Select(p => p.Key).FirstOrDefaultAsync();
                    if (doublePlayersId == 0)
                        break;

                    await ResetDoublePlayers(context, doublePlayersId);
                }

                await context.SaveChangesAsync();
                var unindexedPlayers = await context.Players.Where(p => p.Id == 0).Take(2000).ToListAsync();
                if (unindexedPlayers.Any())
                {
                    Console.Write($"  numbering: {unindexedPlayers.Count()} ");
                    foreach (var player in unindexedPlayers)
                    {
                        player.Id = System.Threading.Interlocked.Increment(ref Indexer.highestPlayerId);
                        context.Players.Update(player);
                    }
                    // save all the ids
                    await context.SaveChangesAsync();
                }

                if (unindexedPlayers.Count < 2000)
                {
                    // all players in the db have an id now
                    bidNumberTask = Task.Run(NumberBids);
                    await NumberAuctions(context);

                    await context.SaveChangesAsync();
                }

                // temp migration
                foreach (var item in context.Auctions.Where(a=>a.UId == 0)
                                    .OrderByDescending(a => a.Id).Take(5000))
                {
                    item.UId = AuctionService.Instance.GetId(item.Uuid);
                    context.Update(item);
                }
                await context.SaveChangesAsync();

            }
            if (bidNumberTask != null)
                await bidNumberTask;

            // give the db a moment to store everything
            await Task.Delay(2000);

        }

        private static async Task ResetDoublePlayers(HypixelContext context, int doublePlayersId)
        {
            if (doublePlayersId % 3 == 0)
                Console.WriteLine($"Found Double player id: {doublePlayersId}, renumbering, highestId: {Indexer.highestPlayerId}");

            foreach (var item in context.Players.Where(p => p.Id == doublePlayersId))
            {
                item.Id = 0;
                context.Update(item);
            }
            foreach (var item in context.Auctions.Where(p => p.SellerId == doublePlayersId))
            {
                item.SellerId = 0;
                context.Update(item);
            }
            foreach (var item in context.Bids.Where(p => p.BidderId == doublePlayersId))
            {
                item.BidderId = 0;
                context.Update(item);
            }
            await context.SaveChangesAsync();
        }

        private static async Task NumberAuctions(HypixelContext context)
        {
            var auctionsWithoutSellerId = await context
                                    .Auctions.Where(a => a.SellerId == 0)
                                    .Include(a => a.Enchantments)
                                    .Include(a => a.NBTLookup)
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

            if (auction.SellerId == 0)
                // his player has not yet received his number
                return;
                
            if (auction.ItemId == 0)
            {
                var id = ItemDetails.Instance.GetOrCreateItemIdForAuction(auction, context);
                auction.ItemId = id;


                foreach (var enchant in auction.Enchantments)
                {
                    enchant.ItemType = auction.ItemId;
                }
            }


            context.Auctions.Update(auction);
        }

        static int batchSize = 10000;

        private static async void NumberBids()
        {
            using (var context = new HypixelContext())
            {
                try
                {
                    var bidsWithoutSellerId = await context.Bids.Where(a => a.BidderId == 0).Take(batchSize).ToListAsync();
                    foreach (var bid in bidsWithoutSellerId)
                    {

                        bid.BidderId = GetOrCreatePlayerId(context, bid.Bidder);
                        if (bid.BidderId == 0)
                            // his player has not yet received his number
                            continue;

                        context.Bids.Update(bid);
                    }

                    await context.SaveChangesAsync();
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Ran into error on numbering bids {e.Message} {e.StackTrace}");
                }

            }
        }

        private static int GetOrCreatePlayerId(HypixelContext context, string uuid)
        {
            if(uuid == null)
                return -1;
            var id = context.Players.Where(p => p.UuId == uuid).Select(p => p.Id).FirstOrDefault();
            if (id == 0)
            {
                id = Program.AddPlayer(context, uuid, ref Indexer.highestPlayerId);
                if (id != 0 && id % 10 == 0)
                    Console.WriteLine($"Adding player {id} {uuid} {Indexer.highestPlayerId}");
            }
            return id;
        }


    }
}