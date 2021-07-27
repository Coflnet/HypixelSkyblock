using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MessagePack;
using WebSocketSharp;

namespace hypixel
{
    public class ClientProxy
    {
        WebSocket socket;

        public static Dictionary<string, Command> ClientComands = new Dictionary<string, Command>();

        private ConcurrentDictionary<long, TaskCompletionSource<MessageData>> WaitingResponse = new ConcurrentDictionary<long, TaskCompletionSource<MessageData>>();
        private long lastMessageId = 0;

        public static ClientProxy Instance { get; }

        private ConcurrentQueue<MessageData> SendQueue = new ConcurrentQueue<MessageData>();

        public ClientProxy(string backendAdress)
        {
            Reconect(backendAdress);
        }

        class ClientMessageData : MessageData
        {
            [IgnoreMember]
            public byte[] data;


            public override T GetAs<T>()
            {
                return MessagePackSerializer.Deserialize<T>(data);
            }

            public override Task SendBack(MessageData data, bool cache = true)
            {
                ClientProxy.Instance.Send(data);
            return Task.CompletedTask;
            }

            public override MessageData Create<T>(string type, T data, int maxAge = 0)
            {
                return new MessageData(type, Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(MessagePackSerializer.ToJson(data))), maxAge);
            }
        }

        public async Task Proxy(MessageData data)
        {
            data.mId = System.Threading.Interlocked.Increment(ref lastMessageId);
            data.Data = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(data.Data));
            Console.WriteLine($"Proxying {data.Type} {data.mId} {MessagePackSerializer.ToJson(data)}");
            var task = new TaskCompletionSource<MessageData>();
            WaitingResponse[data.mId] = task;
            Send(data);
            var result = await task.Task;
            await data.SendBack(result);
        }

        private void Reconect(string backendAdress)
        {
            socket = new WebSocket(backendAdress);
            //socket.Log.Level = LogLevel.Debug;
            socket.OnMessage += (sender, e) =>
            {
                ProcessMessage(e);
            };

            socket.OnOpen += (sender, e) =>
            {
                System.Console.WriteLine("opened socket");
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
            socket.Connect();
        }

        private void ProcessMessage(MessageEventArgs e)
        {
            System.Console.WriteLine(e.Data.Truncate(100));
            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    var data = MessagePackSerializer.Deserialize<MessageData>(MessagePackSerializer.FromJson(e.Data));
                    if (ClientComands.ContainsKey(data.Type))
                    {
                        data = new ClientMessageData()
                        {
                            Type = data.Type,
                            data = System.Convert.FromBase64String(data.Data)
                        };
                        ClientComands[data.Type].Execute(data);
                    }
                    else
                    {
                        if (WaitingResponse.TryRemove(data.mId, out TaskCompletionSource<MessageData> original))
                        {
                            original.TrySetResult(data);
                        }
                        else
                        {
                            dev.Logger.Instance.Log($"received command with no request {data.Type}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    dev.Logger.Instance.Error($"Could not execute client command {ex.Message} \n {ex.StackTrace} \n{ex.InnerException?.Message} {ex.InnerException?.StackTrace}");
                }
            }).ConfigureAwait(false);
        }

        static ClientProxy()
        {
            ClientComands.Add("playerSyncResponse", new PlayerSyncResponse());
            ClientComands.Add("itemSyncResponse", new ItemsSyncResponse());
            ClientComands.Add("pricesSyncResponse", new PricesSyncResponse());
            var adress = SimplerConfig.Config.Instance["BACKEND_URL"];
            if (adress == null)
                adress = $"wss://skyblock-backend.coflnet.com/skyblock?id={Program.InstanceId}";
            Instance = new ClientProxy(adress);
        }

        public void InitialSync()
        {
            Send(new MessageData("itemSync", null));
            Send(new MessageData("playerSync", null));
            Send(new MessageData("pricesSync", null));
            while (!Program.Migrated)
            {
                System.Threading.Thread.Sleep(TimeSpan.FromSeconds(10));
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

            while (SendQueue.TryDequeue(out MessageData result))
            {
                Console.WriteLine($"{DateTime.Now} sent {result.Type} {result.Data.Truncate(20)}");
                socket.Send(MessagePackSerializer.ToJson(result));
            }
            if (SendQueue.Count > 1 && !socket.Ping())
                Console.WriteLine("did not receive pong");
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
        public override async Task Execute(MessageData data)
        {
            var response = data.GetAs<PlayerSyncCommand.PlayerSyncData>();
            var players = response.Players;
            var ids = players.Select(p => p.UuId);
            if (players.Count == 0)
                return; // done
            int count = 0;
            using (var context = new HypixelContext())
            {
                var existingPlayers = context.Players.Where(p => ids.Contains(p.UuId));
                var existing = existingPlayers.ToDictionary(p => p.UuId);
                foreach (var player in players)
                {
                    if (existing.ContainsKey(player.UuId))
                    {
                        var existingPlayer = existingPlayers.Where(p => p.UuId == player.UuId).FirstOrDefault();
                        if (existingPlayer.Name == null)
                        {
                            existingPlayer.Name = player.Name;
                            context.Update(existingPlayer);
                        }
                        continue;
                    }
                    context.Players.Add(player);
                    count++;
                    if (count % 1000 == 0)
                        await context.SaveChangesAsync();
                }
                try
                {
                    await context.SaveChangesAsync();
                }
                catch (Exception e)
                {
                    dev.Logger.Instance.Error(e, "playerSync");
                }
                count = context.Players.Count();
            }
            await data.SendBack(data.Create("playerSync", response.Offset));
        }
    }

    public class ItemsSyncResponse : Command
    {
        public override async Task Execute(MessageData data)
        {
            //data.Data = CacheService.Unzip(data.GetAs<byte[]>());
            var items = data.GetAs<List<DBItem>>();
            using (var context = new HypixelContext())
            {
                foreach (var item in items)
                {
                    if (context.Items.Any(p => p.Tag == item.Tag || p.Id == item.Id))
                        continue;
                    context.Items.Add(item);
                }
                var affected = await context.SaveChangesAsync();
                Console.WriteLine($"Synced items {affected}");

                if (context.Players.Count() > 20_000)
                    Program.Migrated = true;
            }
        }
    }

    public class PricesSyncResponse : Command
    {
        static int ReceivedCount = 0;
        public override async Task Execute(MessageData data)
        {
            var items = data.GetAs<List<PricesSyncCommand.AveragePriceSync>>()
                .ConvertAll(p => p.GetBack())
                .OrderBy(p => p.Date)
                .ToList();
            int count = 0;
            ReceivedCount += items.Count;
            if (items.Count == 0)
                return;
            using (var context = new HypixelContext())
            {
                var batchsize = 200;
                for (int i = 0; i < items.Count / batchsize; i++)
                {
                    await DoBatch(items.Skip(i * batchsize).Take(batchsize), count, context);
                }
                count = context.Prices.Count();
            }
            if (count > 200_000 && Environment.ProcessorCount > 9)
                return; // break early on my dev machine
            await data.SendBack(data.Create("pricesSync", ReceivedCount));
        }

        private static async Task<int> DoBatch(IEnumerable<AveragePrice> items, int count, HypixelContext context)
        {
            var lookup = items.Select(p => p.Date).ToList();
            var exising = context.Prices.Where(p => lookup.Any(l => l == p.Date)).ToList();
            Console.WriteLine($"loaded a total of {exising.Count} prices to check against");
            foreach (var item in items)
            {
                if (context.Prices.Any(p => p.ItemId == item.ItemId && p.Date == item.Date))
                    continue;
                item.Id = 0;
                context.Prices.Add(item);

                count++;
            }
            await context.SaveChangesAsync();
            if (context.Items.Any() && context.Players.Count() > 20_000)
                Program.Migrated = true;
            return count;
        }
    }
}
