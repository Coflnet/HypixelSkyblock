using System.Collections.Generic;
using System.Linq;
using MessagePack;
using WebSocketSharp;

namespace hypixel
{
    public class ClientProxy
    {
        WebSocket socket = new WebSocket("wss://skyblock-backend.coflnet.com");

        public static Dictionary<string, Command> ClientComands = new Dictionary<string, Command>();

        public ClientProxy()
        {
            socket.OnMessage += (sender, e) =>
            {
                System.Console.WriteLine(e.Data);
            };
        }

        static ClientProxy()
        {
            ClientComands.Add("playerSyncResponse", new PlayerSyncResponse());
            ClientComands.Add("itemSyncResponse", new ItemsSyncResponse());
        }

        public void InitialSync()
        {
            socket.Send(MessagePackSerializer.ToJson(new MessageData("itemSync", null)));
            socket.Send(MessagePackSerializer.ToJson(new MessageData("playerSync", null)));
        }

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
            data.Data = CacheService.Unzip(data.GetAs<byte[]>());
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
