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
        public virtual int UserId {get;set;}


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

        public T GetAs<T>()
        {
            return MessagePackSerializer.Deserialize<T>(MessagePackSerializer.FromJson(Data));
        }


        public virtual void SendBack(MessageData data, bool cache = true)
        {
            throw new Exception("Can't send back with default connection");
            
        }

        public static MessageData Create<T>(string type, T data, int maxAge = 0)
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
            SendBack(MessageData.Create("ok", ""));
        }
    }

    public class SocketMessageData : MessageData
    {
        
        [IgnoreMember]
        [Newtonsoft.Json.JsonIgnore]
        public SkyblockBackEnd Connection;
        private int responseCounter = 0;

        public SocketMessageData()
        {
        }

        public override void SendBack(MessageData data, bool cache = true)
        {
            data.mId = mId;
            if (cache )
                CacheService.Instance.Save(this, data, responseCounter++);
            Connection.SendBack(data);
            if(this.Created < DateTime.Now - TimeSpan.FromSeconds(1))
            {
                // wow this took waaay to long
                Console.WriteLine($"slow response/long time ({DateTime.Now-data.Created} at {DateTime.Now}, cache: {cache}): {Newtonsoft.Json.JsonConvert.SerializeObject(this)} ");
            }
        }
    }

    public class HttpMessageData : MessageData
    {
        private HttpListenerResponse res;

        public override int UserId { 
            get => base.UserId; 
            set  {
                SetUserId(value);
                base.UserId = value; 
            }
        }
        public Action<int> SetUserId { get; set; }

        public HttpMessageData(HttpListenerRequest req, HttpListenerResponse res)
        {
            Type = req.RawUrl.Split('/')[2];
            Data = new StreamReader(req.InputStream).ReadToEnd();
            this.res = res;
        }

        public override void SendBack(MessageData data, bool cache = true)
        {
            if (cache )
                CacheService.Instance.Save(this, data, 0);
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(data.Data);
            res.StatusCode = 200;
            res.WriteContent(Encoding.UTF8.GetBytes(json));
        }
    }
}
