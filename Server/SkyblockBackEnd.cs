using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MessagePack;
using WebSocketSharp;
using WebSocketSharp.Server;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace hypixel
{
    public class SkyblockBackEnd : WebSocketBehavior
    {
        public static Dictionary<string,Command> Commands = new Dictionary<string, Command>();
        private static ConcurrentDictionary<long,SkyblockBackEnd> Subscribers = new ConcurrentDictionary<long, SkyblockBackEnd>();
        public static int ConnectionCount => Subscribers.Count;

        public long Id;

        private Task secondThread = null;

        static SkyblockBackEnd()
        {
            Commands.Add("search",new SearchCommand());
            Commands.Add("itemPrices",new ItemPricesCommand());
            Commands.Add("playerDetails",new PlayerDetailsCommand());
            Commands.Add("version",new GetVersionCommand());
            Commands.Add("auctionDetails",new AuctionDetails());
            Commands.Add("itemDetails",new ItemDetailsCommand());
            Commands.Add("clearCache",new ClearCacheCommand());
            Commands.Add("playerAuctions",new PlayerAuctionsCommand());
            Commands.Add("playerBids",new PlayerBidsCommand());
            Commands.Add("allItemNames",new AllItemNamesCommand());
            Commands.Add("bazaarPrices",new BazaarPricesCommand());
            Commands.Add("getAllEnchantments",new GetAllEnchantmentsCommand());
            Commands.Add("fullSearch",new FullSearchCommand());
            Commands.Add("trackSearch",new TrackSearchCommand());
            Commands.Add("playerName",new PlayerNameCommand());
            Commands.Add("subscribe",new SubscribeCommand());
            Commands.Add("unsubscribe",new UnsubscribeCommand());
            Commands.Add("pricerdicer",new NewItemPricesCommand());

            Subscribers[5] = null;
        }

        protected override void OnMessage (MessageEventArgs e)
        {
            long mId = 0;
            try{
                var data = MessagePackSerializer.Deserialize<MessageData>(MessagePackSerializer.FromJson(e.Data));
                mId = data.mId;
                data.Connection = this;
                 data.Data = System.Text.Encoding.UTF8.GetString(System.Convert.FromBase64String(data.Data));
                // Console.WriteLine(data.Data);

                if(!Commands.ContainsKey(data.Type))
                {
                    data.SendBack(new MessageData("error",$"The command `{data.Type}` is Unkown, please check your spelling"));
                    return;
                }
                
                if(CacheService.Instance.TryFromCache(data))
                    return;

                Action command = ()=>{Commands[data.Type].Execute(data);};
                if(this.Id != 0)
                {
                    UseSecondThread(command);
                    return;
                }

                command();
            } catch(CoflnetException ex)
            {

                SendBack(new MessageData("error",JsonConvert.SerializeObject(new {ex.Slug,ex.Message})){mId =mId});
            }catch (Exception ex)
            {
                Console.WriteLine(ex.StackTrace);
                SendBack(new MessageData("error","The Message has to follow the format {\"type\":\"SomeType\",\"data\":\"\"}"){mId =mId});
               
                throw ex;
            }
        }

        private void UseSecondThread(Action command)
        {
            if (this.secondThread == null)
            {
                secondThread = Task.Run(command);
            }
            else
            {
                command();
                try {
                    secondThread.Wait();
                } catch(Exception)
                {

                }
                secondThread.Dispose();
                secondThread = null;
            }
        }

        protected override void OnError(ErrorEventArgs e)
        {
            base.OnError(e);
            Console.WriteLine("=============================\nclosed socket because error");
            Console.WriteLine(e.Message);
            Console.WriteLine(e.Exception.Message);
            Close();
        }

        protected override void OnClose(CloseEventArgs e)
        {
            base.OnClose(e);
            Console.WriteLine("=============================\nclosed socket");
            Console.WriteLine(e.Reason);
            Close();
        }

        private void Close()
        {
            Subscribers.TryRemove(Id,out SkyblockBackEnd value);
        }

        protected override void OnOpen()
        {
            base.OnOpen();
            long id = GetSessionId();
            this.Id = id;
            if (id == 0)
                return;

            if (Subscribers.TryRemove(id, out SkyblockBackEnd value))
            {
                // there was an old session, clean up
                // Todo (currently nothing to clean)
            }

            Subscribers.AddOrUpdate(id, this, (key, old) => this);
            Console.WriteLine(ConnectionCount);
        }

        private long GetSessionId()
        {
            var stringId = this.Context.CookieCollection["id"]?.Value;
            stringId = stringId ?? this.Context.QueryString["id"];

            long id = 0;
            if (stringId != null && stringId.Length > 4)
                id = ((long)stringId.Substring(0, stringId.Length / 2).GetHashCode()) << 32 + stringId.Substring(stringId.Length / 2, stringId.Length / 2).GetHashCode();

            Console.WriteLine($" got connection, id: {stringId} {id} ");
            return id;
        }
        public static bool SendTo(MessageData data, long connectionId)
        {
            var connected = Subscribers.TryGetValue(connectionId,out SkyblockBackEnd value);
            if(connected)
                value.SendBack(data);

            return connected;
        }

        public void SendBack(MessageData data)
        {
            Send(MessagePackSerializer.ToJson(data));
        }
    }
}
