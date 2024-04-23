using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using MessagePack;
using Newtonsoft.Json;
using RestSharp;
using System.Diagnostics;

namespace Coflnet.Sky.Core
{
    public partial class CoreServer
    {
        static RestClient client;

        public static CoreServer Instance = new CoreServer();


        public static Task<TRes> ExecuteCommandWithCache<TReq, TRes>(string command, TReq reqdata)
        {
            return Instance.ExecuteCommandWithCacheInternal<TReq, TRes>(command, reqdata);
        }

        public virtual async Task<TRes> ExecuteCommandWithCacheInternal<TReq, TRes>(string command, TReq reqdata)
        {
            try
            {
                if (client == null)
                {
                    var url = SimplerConfig.SConfig.Instance["SKYCOMMANDS_BASE_URL"] ?? "http://" + SimplerConfig.SConfig.Instance["SKYCOMMANDS_HOST"];
                    if (string.IsNullOrEmpty(url))
                    {
                        throw new Exception("The enviroment variable SKYCOMMANDS_BASE_URL is not set to a valid url");
                    }
                    client = new RestClient(url.Replace(":8008", "") + ":8008");

                }
                var source = new TaskCompletionSource<TRes>();
                var data = new ProxyMessageData<TReq, TRes>(command, reqdata, source);
                var request = new RestRequest($"/command/{command}/{Convert.ToBase64String(Encoding.UTF8.GetBytes(MessagePackSerializer.ConvertToJson(MessagePackSerializer.Serialize(reqdata))))}");
                request.Timeout = 10000;
                var result = await client.ExecuteAsync(request);
                if (result.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    Console.WriteLine($"cache response for {client.BuildUri(request)}: " + result.StatusCode);
                    return default(TRes);

                }
                try
                {
                    return JsonConvert.DeserializeObject<TRes>(result.Content);

                }
                catch (Exception e)
                {
                    dev.Logger.Instance.Error(e, "deserialize command response \n" + result.Content);
                    return MessagePackSerializer.Deserialize<TRes>(MessagePackSerializer.ConvertFromJson(result.Content));
                }
            }
            catch (Exception e)
            {
                dev.Logger.Instance.Error(e, "execute with cache");
                throw e;
            }
        }


        public static void AddPremiumTime(int days, GoogleUser user)
        {
            if (user.PremiumExpires > DateTime.Now)
                user.PremiumExpires += TimeSpan.FromDays(days);
            else
                user.PremiumExpires = DateTime.Now + TimeSpan.FromDays(days);
        }
    }

    public class ProxyMessageData<Treq, TRes> : MessageData
    {
        private readonly Treq request;
        TaskCompletionSource<TRes> source;

        public ProxyMessageData(string type, Treq request, TaskCompletionSource<TRes> source) : base(type, "", 0)
        {
            this.request = request;
            Type = type;
            this.source = source;
            Data = MessagePackSerializer.ConvertToJson(MessagePackSerializer.Serialize(request));
        }

        public override T GetAs<T>()
        {
            return (T)(object)request;
        }

        public override MessageData Create<T>(string type, T a, int maxAge = 0)
        {
            source.TrySetResult((TRes)(object)a);
            var d = base.Create<T>(type, a, maxAge);
            d.Data = MessagePackSerializer.ConvertToJson(MessagePackSerializer.Serialize(a));
            return d;
        }


        public override Task SendBack(MessageData data, bool cache = true)
        {
            try
            {
                if (source.TrySetResult(MessagePackSerializer.Deserialize<TRes>(MessagePackSerializer.ConvertFromJson(data.Data))))
                { /* nothing to do, already set */ }
            }
            catch (Exception)
            {
                // thrown excpetion, looks like it isn't messagepack
                if (source.TrySetResult(JsonConvert.DeserializeObject<TRes>(data.Data)))
                { /* nothing to do, already set */ }
            }

            if (cache)
                CacheService.Instance.Save(this, data, 0);
            return Task.CompletedTask;
        }
    }

    public abstract class RequestContext
    {
        public abstract string path { get; }
        public abstract string HostName { get; }
        public abstract Task WriteAsync(string data);
        public abstract Task WriteAsync(byte[] data);
        public abstract void SetContentType(string type);
        public abstract void SetStatusCode(int code);
        public abstract void AddHeader(string name, string value);
        public abstract void Redirect(string uri);
        public abstract IDictionary<string, string> QueryString { get; }
        public abstract string UserAgent { get; }
        public Activity Span;

        public virtual void ForceSend(bool close = false)
        {
            // do nothing
        }
    }
}
