using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MessagePack;
using StackExchange.Redis;

namespace hypixel
{
    public class CacheService
    {
        public static CacheService Instance { get; protected set; }

        private static int MaxCacheSize = Int32.Parse(SimplerConfig.Config.Instance["MaxCacheItems"]);

        public int CacheSize => -1;

        public ConnectionMultiplexer RedisConnection { get; }

        static CacheService()
        {
            Instance = new CacheService();
        }

        public CacheService()
        {
            try
            {
                ConfigurationOptions options = ConfigurationOptions.Parse(SimplerConfig.Config.Instance["redisCon"]);
                options.Password = SimplerConfig.Config.Instance["redisPassword"];
                options.AsyncTimeout = 10000;
                RedisConnection = ConnectionMultiplexer.Connect(options);
            }
            catch (Exception e)
            {
                dev.Logger.Instance.Error(e, "Cache service constructor ");
                Task.Run(async () =>
                {
                    await Task.Delay(TimeSpan.FromMinutes(1));
                    Instance = new CacheService();
                }).ConfigureAwait(false);
            }
        }

        public async Task<T> GetFromRedis<T>(RedisKey key)
        {
            try
            {
                var value = await RedisConnection.GetDatabase().StringGetAsync(key);
                if (value == RedisValue.Null)
                    return default(T);
                return MessagePack.MessagePackSerializer.Deserialize<T>(value);
            }
            catch (Exception e)
            {
                dev.Logger.Instance.Error($"Redis error {e.Message} {e.StackTrace} \n on key {key}");
                return default(T);
            }

        }

        public async Task SaveInRedis<T>(RedisKey key, T obj, TimeSpan timeout = default(TimeSpan))
        {
            if (timeout == default(TimeSpan))
                timeout = TimeSpan.FromDays(1);
            try
            {
                await RedisConnection.GetDatabase().StringSetAsync(key, MessagePack.MessagePackSerializer.Serialize(obj), timeout);
            }
            catch (Exception e)
            {
                dev.Logger.Instance.Error(e, "Saving into redis");
            }
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
            return TryFromCacheAsync(request).GetAwaiter().GetResult().HasFlag(CacheStatus.VALID);
        }

        public async Task<CacheStatus> TryFromCacheAsync(MessageData request)
        {
            var key = GetCacheKey(request);
            var responses = await GetFromRedis<CacheElement>(key);
            if (responses == null)
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
                await request.SendBack(response, false);
            }
            if ((responses.Expires - responses.Created).TotalSeconds / 2 > maxAgeLeft)
            {
                RefreshResponse(request);
                return CacheStatus.REFRESH;
            }

            return CacheStatus.RECENT;
        }

        private static void RefreshResponse(MessageData request)
        {
            var proxyReq = new CacheMessageData(request.Type, request.Data);
            var task = Task.Run(() =>
            {
                try
                {
                    Console.WriteLine("renewing cache for " + request.Type);
                    Server.ExecuteCommandHeadless(proxyReq);
                }
                catch (Exception e)
                {
                    dev.Logger.Instance.Error(e, "cache refresh failed");
                }
            }).ConfigureAwait(false);
        }

        public void ClearStale()
        {/*
            var toRemove = cache.Where(item => item.Value.Expires < DateTime.Now)
                            .Select(item => item.Key).ToList();
            foreach (var item in toRemove)
            {
                cache.TryRemove(item, out CacheElement value);
            }*/
        }

        private static string GetCacheKey(MessageData request)
        {
            var key = request.CustomCacheKey;
            if (key == null)
                key = GetCacheKey(request.Type, request.Data);
            return key;
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
            public IEnumerable<MessageData> Responses => Reduced.Select(e => new MessageData(e.type, Unzip(e.data)));

            [Key(1)]
            public List<ReducedCommandData> Reduced;

            [Key(2)]
            public DateTime Created;

            public CacheElement(DateTime expires, List<MessageData> responses)
            {
                Expires = expires;
                Reduced = responses.Select(CreateItem)
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

        internal void RunForEver()
        {
            var tenMin = (int)TimeSpan.FromMinutes(10).TotalMilliseconds;
            Task.Run(async () =>
            {
                while (true)
                {
                    await Task.Delay(tenMin);
                    ClearStale();
                }
            }).ConfigureAwait(false);
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
            using (var msi = new MemoryStream(bytes))
            using (var mso = new MemoryStream())
            {
                using (var gs = new GZipStream(msi, CompressionMode.Decompress))
                {
                    //gs.CopyTo(mso);
                    CopyTo(gs, mso);
                }

                return Encoding.UTF8.GetString(mso.ToArray());
            }
        }

        public class CacheMessageData : MessageData
        {
            public CacheMessageData(string type, string data)
            {
                this.Type = type;
                this.Data = data;
            }

            public override Task SendBack(MessageData data, bool cache = true)
            {
                CacheService.Instance.Save(this, data);
                return Task.CompletedTask;
            }
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