using System.Collections.Generic;
using System.Linq;
using MessagePack;
using Microsoft.EntityFrameworkCore;
using WebSocketSharp;

namespace hypixel
{
    public class ClientProxy
    {
        WebSocket socket = new WebSocket("wss://skyblock-backend.coflnet.com");

        public void Sync()
        {
            using (var context = new HypixelContext())
            {
                var done = false;
                var index = 0;
                var batchAmount = 5000;
                while (!done)
                {
                    var response = context.Auctions.Skip(batchAmount * index++).Take(batchAmount).Select(a => new { a.Uuid, a.HighestBidAmount }).ToList();
                    if (response.Count == 0)
                        return;
                    
                   // socket.Send()
                   // data.SendBack(data.Create("playerSyncResponse", response));
                }
            }
        }
    }

    [MessagePackObject]
    public class AuctionSync
    {
        [Key(0)]
        public string Id;
        [Key(1)]
        public int HighestBid;
    }

    public class AuctionSyncCommand : Command
    {
        public override void Execute(MessageData data)
        {
            var auctions = data.GetAs<List<AuctionSync>>();
            using (var context = new HypixelContext())
            {


                List<string> incomplete = new List<string>();

                foreach (var auction in auctions)
                {
                    var a = context.Auctions.Where(p => p.Uuid == auction.Id).Select(a => new { a.Id, a.HighestBidAmount }).FirstOrDefault();
                    if (a.HighestBidAmount == auction.HighestBid)
                        continue;
                    incomplete.Add(auction.Id);
                }
            }
        }
    }


    public class PlayerSyncCommand : Command
    {
        public override void Execute(MessageData data)
        {
            using (var context = new HypixelContext())
            {
                var done = false;
                var index = 0;
                var batchAmount = 10000;
                while (!done)
                {
                    var response = context.Players.Skip(batchAmount * index++).Take(batchAmount).ToList();
                    if (response.Count == 0)
                        return;
                    data.SendBack(data.Create("playerSyncResponse", response));
                }
            }
        }
    }

    public class ItemSyncCommand : Command
    {
        public override void Execute(MessageData data)
        {
            using (var context = new HypixelContext())
            {
                var response = context.Items.Include(i => i.Names).ToList();
                data.SendBack(data.Create("playerSyncResponse", response));
            }
        }
    }

    public class PlayerSyncResponse : Command
    {
        public override void Execute(MessageData data)
        {
            var players = data.GetAs<List<Player>>();
            using (var context = new HypixelContext())
            {
                foreach (var player in players)
                {
                    if (context.Players.Any(p => p.UuId == player.UuId))
                        continue;
                    context.Players.Add(player);
                }
                context.SaveChanges();
            }
        }
    }

    public class ItemsSyncResponse : Command
    {
        public override void Execute(MessageData data)
        {
            var items = data.GetAs<List<DBItem>>();
            using (var context = new HypixelContext())
            {
                foreach (var item in items)
                {
                    if (context.Items.Any(p => p.Tag == item.Tag))
                        continue;
                    context.Items.Add(item);
                }
                context.SaveChanges();
            }
        }
    }
}
