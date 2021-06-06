using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using MessagePack;
using WebSocketSharp;

namespace hypixel
{
    public class ClientProxy
    {
        WebSocket socket;

        public static Dictionary<string, Command> ClientComands = new Dictionary<string, Command>();

        public static ClientProxy Instance { get; }

        private ConcurrentQueue<MessageData> SendQueue = new ConcurrentQueue<MessageData>();

        public ClientProxy(string backendAdress)
        {
            Reconect(backendAdress);
        }

        private void Reconect(string backendAdress)
        {
            socket = new WebSocket(backendAdress);
            socket.Log.Level = LogLevel.Debug;
            socket.OnMessage += (sender, e) =>
            {
                System.Console.WriteLine(e.Data);
                try
                {
                    var data = MessagePackSerializer.Deserialize<MessageData>(MessagePackSerializer.FromJson(e.Data));
                    ClientComands[data.Type].Execute(data);
                } catch(Exception ex)
                {
                    dev.Logger.Instance.Error($"Could not execute client command {ex.Message} \n {ex.StackTrace}");
                }
            };

            socket.OnOpen += (sender, e) =>
            {
                System.Console.WriteLine("opened scoket");
                ProcessSendQueue();
            };

            socket.OnClose += (sender, e) =>
            {
                System.Console.WriteLine("closed socket");
                System.Console.WriteLine(e.Reason);
                Reconnect();
            };

            socket.OnError += (sender, e) =>
            {
                System.Console.WriteLine("socket error");
                System.Console.WriteLine(e.Message);
                Reconnect();
            };
            socket.ConnectAsync();
        }

        static ClientProxy()
        {
            ClientComands.Add("playerSyncResponse", new PlayerSyncResponse());
            ClientComands.Add("itemSyncResponse", new ItemsSyncResponse());
            ClientComands.Add("pricesSyncResponse", new PricesSyncResponse());
            var adress = SimplerConfig.Config.Instance["BACKEND_URL"];
            if (adress == null)
                adress = "wss://skyblock-backend.coflnet.com/skyblock?id=clusterClient";
            Instance = new ClientProxy(adress);
        }

        public void InitialSync()
        {
            Send(new MessageData("itemSync", null));
            Send(new MessageData("playerSync", null));
            Send(new MessageData("pricesSync", null));
            while(!Program.Migrated)
            {
                System.Threading.Thread.Sleep(TimeSpan.FromMinutes(1));
                ProcessSendQueue();
            }
        }

        public void Send(MessageData data)
        {
            SendQueue.Enqueue(data);
            ProcessSendQueue();
        }

        private void ProcessSendQueue()
        {
            if (socket.ReadyState != WebSocketState.Open)
            {
                Console.WriteLine("websocket is " + socket.ReadyState);
                if (socket.ReadyState != WebSocketState.Connecting)
                    Reconnect();
                return;
            }
            Console.WriteLine("processing send queue");
            while (SendQueue.TryDequeue(out MessageData result))
            {
                socket.Send(MessagePackSerializer.ToJson(result));
            }
        }

        private void Reconnect()
        {
            Reconect(socket.Url.AbsoluteUri);
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
                var affected = context.SaveChanges();
                Console.WriteLine($"Synced items {affected}");
            }
        }
    }

    public class PricesSyncResponse : Command
    {
        public override void Execute(MessageData data)
        {
            data.Data = CacheService.Unzip(data.GetAs<byte[]>());
            var items = data.GetAs<List<AveragePrice>>();
            using (var context = new HypixelContext())
            {
                foreach (var item in items)
                {
                    if (context.Prices.Any(p => p.ItemId == item.ItemId && p.Date == item.Date))
                        continue;
                    context.Prices.Add(item);
                }
                context.SaveChanges();
                if(context.Items.Any() && context.Players.Count() > 2_000_000)
                    Program.Migrated = true;
            }
        }
    }
}
