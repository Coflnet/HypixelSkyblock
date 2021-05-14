using System;
using System.IO;
using System.Text;
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
            return MessagePackSerializer.Deserialize<T>(MessagePackSerializer.FromJson(Data));
        }


        public virtual void SendBack(MessageData data, bool cache = true)
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
        public void Ok()
        {
            SendBack(Create("ok", ""));
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

        public override void SendBack(MessageData data, bool cache = true)
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
        }
    }

    public class HttpMessageData : MessageData
    {
        private HttpListenerResponse res;

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

        public HttpMessageData(HttpListenerRequest req, HttpListenerResponse res)
        {
            Type = req.RawUrl.Split('/')[2];
            this.res = res;
            if (req.HttpMethod == "POST")
            {
                Data = new StreamReader(req.InputStream).ReadToEnd();
                return;
            }
            try
            {
                Data = Encoding.UTF8.GetString(Convert.FromBase64String(req.RawUrl.Split('/')[3]));
            }
            catch (System.Exception e)
            {
                dev.Logger.Instance.Error($"received invalid command {req.RawUrl} {e.Message} {e.StackTrace}");
                this.SendBack(new MessageData("error", "commanddata was invalid"));
            }

        }

        public override void SendBack(MessageData data, bool cache = true)
        {
            if (cache)
                CacheService.Instance.Save(this, data, 0);
            var json = data.Data;
            res.StatusCode = 200;
            res.AppendHeader("cache-control", "public,max-age=" + data.MaxAge.ToString());
            res.WriteContent(Encoding.UTF8.GetBytes(json));
        }
    }
}
