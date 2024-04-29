using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using MessagePack;
using System.Diagnostics;

namespace Coflnet.Sky.Core
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

        [IgnoreMember]
        [Newtonsoft.Json.JsonIgnore]
        public Activity Span { get; set; }

        public void Log(string message)
        {
            if(Span != null)
                Span.AddTag("message", message);
        }

        public void LogError(Exception e, string message)
        {
            if(Span != null)
            {
                Span.AddTag("message", message);
                Span.AddTag("exception", e.ToString());
            }
            else
                dev.Logger.Instance.Error(e,message);
        }

        private void LogException(Exception e, int index = 0)
        {
            Span.SetTag("errorMessage"+index, e.Message);
            Span.SetTag("errorTrace"+index, e.StackTrace);
            if(e.InnerException != null)
                LogException(e.InnerException,index+1);
        }

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
                return MessagePackSerializer.Deserialize<T>(MessagePackSerializer.ConvertFromJson(Data));
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
            var d = new MessageData(type, MessagePackSerializer.ConvertToJson(MessagePackSerializer.Serialize(data)), maxAge);
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


    public class HttpMessageData : MessageData
    {
        public RequestContext context { get; private set; }

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

        public HttpMessageData(RequestContext context)
        {
            var parts = context.path.Split('/', '?');
            Type = parts[2];
            Span = context.Span;
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
                if (parts.Length > 3)
                    Data = Encoding.UTF8.GetString(Convert.FromBase64String(parts[3]));
            }
            catch (Exception e)
            {
                dev.Logger.Instance.Error($"received invalid command {context.path} {e.Message} {e.StackTrace}");
                SendBack(new MessageData("error", "commanddata was invalid")).Wait();
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

            Span?.SetTag("result", data.Type);

            if (cache)
                CacheService.Instance.Save(this, data, 0);
        }
    }
}
