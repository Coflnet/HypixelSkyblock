using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MessagePack;
using StackExchange.Redis;

namespace Coflnet.Sky.Core
{
    public class CacheService
    {
        public static CacheService Instance { get; protected set; }

        public int CacheSize => HotCache.Sum(e => e.Value.Length);
        /// <summary>
        /// event executed when the cache should be refreshed
        /// </summary>
        public event Action<MessageData> OnCacheRefresh;
        private ConnectionMultiplexer _con;

        public ConnectionMultiplexer RedisConnection
        {
            get
            {
                if (_con == null)
                    ConnectToRedis();
                return _con;
            }
            private set
            {
                _con = value;
            }
        }

        private ConcurrentDictionary<string, byte[]> HotCache = new();
        private DateTime lastReconnect;

        static CacheService()
        {
            Instance = new CacheService();
        }

        public CacheService()
        {
            try
            {
                ConnectToRedis();
            }
            catch (Exception e)
            {
                dev.Logger.Instance.Error(e, "Cache service constructor (could not connect)");
            }
        }

        private void ConnectToRedis()
        {
            if (lastReconnect > DateTime.Now - TimeSpan.FromSeconds(10))
                return;
            lastReconnect = DateTime.Now;
            var conName = SimplerConfig.SConfig.Instance["REDIS_HOST"] ?? SimplerConfig.SConfig.Instance["redisCon"];
            ConfigurationOptions options = ConfigurationOptions.Parse(conName);
            RedisConnection = ConnectionMultiplexer.Connect(options);
            RedisConnection.IncludePerformanceCountersInExceptions = true;
            RedisConnection.GetSubscriber().Subscribe("cofl-cache-update", (channel, message) =>
            {
                var prefix = GetHostprefix();
                if (message.StartsWith(prefix))
                    return;
                message = message.ToString().Substring(prefix.Length);
                HotCache.TryRemove(message, out byte[] val);
            });
        }

        public async Task<T> GetFromRedis<T>(RedisKey key)
        {
            try
            {
                if (HotCache.TryGetValue(key, out byte[] val))
                {
                    return MessagePackSerializer.Deserialize<T>(val);
                }
                if (RedisConnection == null)
                {
                    dev.Logger.Instance.Info("no redis connection");
                    return default(T);
                }
                var value = await RedisConnection.GetDatabase().StringGetAsync(key);
                if (value == RedisValue.Null)
                    return default(T);
                return MessagePackSerializer.Deserialize<T>(value);
            }
            catch (RedisConnectionException)
            {
                ConnectToRedis();
                dev.Logger.Instance.Error("Redis timeout, reconnecting");
            }
            catch (RedisTimeoutException e)
            {
                if (new Random().Next() % 16 == 0)
                    dev.Logger.Instance.Error(e, $"Redis timeout");
            }
            catch (Exception e)
            {
                dev.Logger.Instance.Error(e, $"Redis error when getting key: {key.ToString().Truncate(40)}");
            }
            return default(T);
        }

        public async Task DeleteInRedis(RedisKey key)
        {
            try
            {
                var value = await RedisConnection.GetDatabase().KeyDeleteAsync(key);
                HotCache.TryRemove(key, out byte[] val);
            }
            catch (Exception e)
            {
                dev.Logger.Instance.Error(e, $"error on deleting key: {key.ToString().Truncate(40)}");
            }
        }

        public async Task SaveInRedis<T>(RedisKey key, T obj, TimeSpan timeout = default(TimeSpan))
        {
            if (timeout == default(TimeSpan))
                timeout = TimeSpan.FromDays(1);
            try
            {
                var data = MessagePackSerializer.Serialize(obj);
                if (HotCache.Count > 350)
                    HotCache.Clear();
                HotCache.AddOrUpdate(key, data, (_, _) => data);
                await RedisConnection.GetDatabase().StringSetAsync(key, data, timeout, When.Always, CommandFlags.FireAndForget);
                string hostPrefix = GetHostprefix();
                await RedisConnection.GetSubscriber().PublishAsync("cofl-cache-update", hostPrefix + key.ToString(), CommandFlags.FireAndForget);
            }
            catch (Exception e)
            {
                dev.Logger.Instance.Error(e, "Saving into redis " + key.ToString());
            }
        }

        private static string GetHostprefix()
        {
            var fullHostName = System.Net.Dns.GetHostName();
            var hostPrefix = fullHostName.Substring(Math.Max(0, fullHostName.Length - 5)).PadRight(5, '0');
            return hostPrefix;
        }

        public async Task ModifyInRedis<T>(RedisKey key, Func<T, T> modifier, TimeSpan timeout = default(TimeSpan))
        {
            var val = modifier(await GetFromRedis<T>(key));
            await SaveInRedis(key, val, timeout);
        }

        public async void Save(MessageData request, MessageData response, int index = 0)
        {
            if (response.MaxAge == 0)
                return;

            string key = GetCacheKey(request);
            try
            {
                await AddOrUpdateCache(response, index, key);
            }
            catch (Exception e)
            {
                dev.Logger.Instance.Error(e, "saving into cache");
            }
        }

        private async Task AddOrUpdateCache(MessageData response, int index, string key)
        {
            var span = TimeSpan.FromSeconds(response.MaxAge);
            var newEntry = new CacheElement(DateTime.Now + span, new List<MessageData>() { response });
            await SaveInRedis(key, newEntry, span);
        }


        public bool TryFromCache(MessageData request)
        {
            return TryFromCacheAsync(request).GetAwaiter().GetResult().IsFlagSet(CacheStatus.VALID);
        }

        public async Task<CacheStatus> TryFromCacheAsync(MessageData request)
        {
            var key = GetCacheKey(request);
            var responses = await GetFromRedis<CacheElement>(key);
            if (responses?.Responses == null)
                return CacheStatus.MISS;

            if (responses.Expires < DateTime.Now)
            {
                // stale
                return CacheStatus.STALE;
            }

            var maxAgeLeft = (int)(responses.Expires - DateTime.Now).TotalSeconds;
            foreach (var response in responses.Responses)
            {
                // adjust the cache time to when it expires on the server
                response.MaxAge = maxAgeLeft;
                try
                {
                    await request.SendBack(response, false);
                }
                catch (Exception e)
                {
                    await DeleteInRedis(key);
                    dev.Logger.Instance.Error(e, "Try from cache return failed");
                    return CacheStatus.MISS;
                }
            }
            var lifetime = (responses.Expires - responses.Created).TotalSeconds;
            if (lifetime / 2 > maxAgeLeft)
            {
                Activity.Current?.AddEvent(new ActivityEvent($"refresh stale {lifetime} {request.Type.Truncate(10)}"));
                RefreshResponse(request);
                return CacheStatus.REFRESH;
            }

            return CacheStatus.RECENT;
        }

        private void RefreshResponse(MessageData request)
        {
            var proxyReq = new CacheMessageData(request.Type, request.Data);
            var task = Task.Run(() =>
            {
                try
                {
                    OnCacheRefresh?.Invoke(proxyReq);
                }
                catch (Exception e)
                {
                    dev.Logger.Instance.Error(e, "cache refresh failed");
                }
            }).ConfigureAwait(false);
        }


        private static string GetCacheKey(MessageData request)
        {
            return request.CustomCacheKey ?? GetCacheKey(request.Type, request.Data);
        }

        private static string GetCacheKey(string type, string data)
        {
            return type + data;
        }

        [MessagePackObject]
        public class CacheElement
        {
            [Key(0)]
            public DateTime Expires;
            [IgnoreMember]
            public IEnumerable<MessageData> Responses => Reduced?.Select(e => new MessageData(e.type, Unzip(e.data)));

            [Key(1)]
            public readonly List<ReducedCommandData> Reduced;

            [Key(2)]
            public DateTime Created;

            public CacheElement(DateTime expires, List<MessageData> responses)
            {
                Expires = expires;
                Reduced = responses?.Select(CreateItem)
                            .ToList();
                Created = DateTime.Now - TimeSpan.FromSeconds(20);
            }

            public CacheElement()
            {
            }

            public void Add(MessageData data)
            {
                Reduced.Add(CreateItem(data));
            }

            private static ReducedCommandData CreateItem(MessageData m)
            {
                var compressed = Zip(m.Data);
                return new ReducedCommandData(m.Type, compressed);
            }
        }

        [MessagePackObject]
        public class ReducedCommandData
        {
            [Key(0)]
            public string type;
            [Key(1)]
            public byte[] data;

            public ReducedCommandData()
            {
            }

            public ReducedCommandData(string type, byte[] data)
            {
                this.type = type;
                this.data = data;
            }


        }

        /// <summary>
        /// Copies a stream
        /// https://stackoverflow.com/a/7343623
        /// </summary>
        /// <param name="src"></param>
        /// <param name="dest"></param>
        public static void CopyTo(Stream src, Stream dest)
        {
            byte[] bytes = new byte[4096];

            int cnt;

            while ((cnt = src.Read(bytes, 0, bytes.Length)) != 0)
            {
                dest.Write(bytes, 0, cnt);
            }
        }

        public static byte[] Zip(string str)
        {
            var bytes = Encoding.UTF8.GetBytes(str);
            if (bytes.Length < 100)
                return bytes;

            using (var msi = new MemoryStream(bytes))
            using (var mso = new MemoryStream())
            {
                using (var gs = new GZipStream(mso, CompressionMode.Compress))
                {
                    //msi.CopyTo(gs);
                    CopyTo(msi, gs);
                }

                return mso.ToArray();
            }
        }

        public static string Unzip(byte[] bytes)
        {
            if (bytes.Length < 100)
                return Encoding.UTF8.GetString(bytes);
            using var msi = new MemoryStream(bytes);
            using var mso = new MemoryStream();
            using (var gs = new GZipStream(msi, CompressionMode.Decompress))
            {
                //gs.CopyTo(mso);
                CopyTo(gs, mso);
            }

            return Encoding.UTF8.GetString(mso.ToArray());
        }

        public class CacheMessageData : MessageData
        {
            public CacheMessageData(string type, string data)
            {
                Type = type;
                Data = data;
            }

            public override Task SendBack(MessageData data, bool cache = true)
            {
                Instance.Save(this, data);
                return Task.CompletedTask;
            }
        }


    }
    public static class CacheExtentions
    {
        public static bool IsFlagSet<T>(this T value, T flag) where T : struct
        {
            if (!typeof(T).IsEnum)
                throw new ArgumentException($"{typeof(T).FullName} is not an enum");
            long lValue = Convert.ToInt64(value);
            long lFlag = Convert.ToInt64(flag);
            return (lValue & lFlag) != 0;
        }
    }
    public enum CacheStatus
    {
        MISS = 1,
        STALE = 2,
        REFRESH = 4,
        RECENT = 8,
        VALID = RECENT | REFRESH
    }

}