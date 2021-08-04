using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using MessagePack;
using WebSocketSharp;
using WebSocketSharp.Net;

namespace hypixel
{
    [MessagePackObject]
    public class MessageData
    {
        [IgnoreMember]
        [Newtonsoft.Json.JsonIgnore]
        public virtual int UserId { get; set; }


        [Key("type")]
        public string Type;

        [Key("data")]
        public string Data;

        [Key("mId")]
        public long mId;

        [Key("maxAge")]
        public int MaxAge;

        [IgnoreMember]
        [Newtonsoft.Json.JsonIgnore]
        public string CustomCacheKey;
        [IgnoreMember]
        [Newtonsoft.Json.JsonIgnore]
        public GoogleUser User => UserService.Instance.GetUserById(UserId);
        [IgnoreMember]
        [Newtonsoft.Json.JsonIgnore]
        public DateTime Created = DateTime.Now;

        public MessageData(string type, string data, int maxAge = 0)
        {
            Type = type;
            Data = data;
            MaxAge = maxAge;
        }
        public MessageData()
        {
        }

        public virtual T GetAs<T>()
        {
            if (String.IsNullOrEmpty(Data))
                return default(T);
            try
            {
                return MessagePackSerializer.Deserialize<T>(MessagePackSerializer.FromJson(Data));
            }
            catch (Exception)
            {
                if (typeof(T) == typeof(string))
                {
                    return (T)(object)Convert.ToBase64String(Encoding.UTF8.GetBytes(Data));
                }
                throw;
            }
        }


        public virtual Task SendBack(MessageData data, bool cache = true)
        {
            throw new Exception("Can't send back with default connection");

        }

        public virtual MessageData Create<T>(string type, T data, int maxAge = 0)
        {
            var d = new MessageData(type, MessagePackSerializer.ToJson(data), maxAge);
            return d;
        }

        public static MessageData Copy(MessageData original)
        {
            var d = new MessageData(original.Type, original.Data, original.MaxAge);
            return d;
        }

        /// <summary>
        /// Sends back an empty ok message
        /// </summary>
        public Task Ok()
        {
            return SendBack(Create("ok", ""));
        }
    }

    public class SocketMessageData : MessageData
    {

        [IgnoreMember]
        [Newtonsoft.Json.JsonIgnore]
        public SkyblockBackEnd Connection;
        private int responseCounter = 0;

        public override int UserId
        {
            get => Connection.UserId;
            set => Connection.UserId = value;
        }


        public SocketMessageData()
        {
        }

        public override Task SendBack(MessageData data, bool cache = true)
        {
            data.mId = mId;
            if (cache)
                CacheService.Instance.Save(this, data, responseCounter++);
            Connection.SendBack(data);
            if (this.Created < DateTime.Now - TimeSpan.FromSeconds(1))
            {
                // wow this took waaay to long
                Console.WriteLine($"slow response/long time ({DateTime.Now - data.Created} at {DateTime.Now}, cache: {cache}): {Newtonsoft.Json.JsonConvert.SerializeObject(this)} ");
            }
            return Task.CompletedTask;
        }
    }

    public class HttpMessageData : MessageData
    {
        public Server.RequestContext context { get; private set; }

        TaskCompletionSource<bool> source = new TaskCompletionSource<bool>();

        public TaskCompletionSource<bool> CompletionSource => source;

        public override int UserId
        {
            get => base.UserId;
            set
            {
                SetUserId(value);
                base.UserId = value;
            }
        }
        public Action<int> SetUserId { get; set; }

        public HttpMessageData(Server.RequestContext context)
        {
            Type = context.path.Split('/')[2];
            this.context = context;
            // default status code
            context.SetStatusCode(201);
            /*if (req.HttpMethod == "POST")
            {
                Data = new StreamReader(req.InputStream).ReadToEnd();
                return;
            } */
            try
            {
                var parts = context.path.Split('/');
                if (parts.Length > 3)
                    Data = Encoding.UTF8.GetString(Convert.FromBase64String(parts[3]));
            }
            catch (System.Exception e)
            {
                dev.Logger.Instance.Error($"received invalid command {context.path} {e.Message} {e.StackTrace}");
                this.SendBack(new MessageData("error", "commanddata was invalid"));
            }

        }

        public override async Task SendBack(MessageData data, bool cache = true)
        {
            var json = data.Data;
            context.SetStatusCode(200);

            // important for proxied commands
            if (data.Type == "error")
                context.SetStatusCode(500);

            context.AddHeader("cache-control", "public,max-age=" + data.MaxAge.ToString());
            if (string.IsNullOrEmpty(json))
            {
                Console.WriteLine("returned empty response on " + JSON.Stringify(this));
            }
            await context.WriteAsync(json);
            source.TrySetResult(true);

            if (cache)
                CacheService.Instance.Save(this, data, 0);
        }
    }
}
