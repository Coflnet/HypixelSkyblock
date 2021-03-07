using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace hypixel
{
    public class CacheService
    {
        public static CacheService Instance { get; protected set; }

        private ConcurrentDictionary<string, CacheElement> cache = new ConcurrentDictionary<string, CacheElement>();

        public int CacheSize => cache.Count;

        static CacheService()
        {
            Instance = new CacheService();
        }

        public void Save(MessageData request, MessageData response, int index)
        {
            if (response.MaxAge == 0)
                return;

            string key = GetCacheKey(request);
            AddOrUpdateCache(response, index, key);
        }

        private void AddOrUpdateCache(MessageData response, int index, string key)
        {
            var newEntry = new CacheElement(DateTime.Now + TimeSpan.FromSeconds(response.MaxAge), new List<MessageData>() { response });
            cache.AddOrUpdate(key, newEntry, (key, item) =>
            {
                if (index == 0)
                    return newEntry;
                item.Add(response);
                return item;
            });
        }

        public void Save(string type, string data, MessageData response)
        {
            string key = GetCacheKey(type, data);
            AddOrUpdateCache(response, 0, key);
        }

        public bool TryFromCache(MessageData request)
        {
            var key = GetCacheKey(request);
            if (!cache.TryGetValue(key, out CacheElement responses))
                return false;

            if (responses.Expires < DateTime.Now)
                return false;

            foreach (var response in responses.Responses)
            {
                // adjust the cache time to when it expires on the server
                response.MaxAge = (int)(responses.Expires - DateTime.Now).TotalSeconds;
                request.SendBack(response, false);
            }
            return true;
        }

        public bool GetFromCache(string command, string data, out string value)
        {
            var key = GetCacheKey(command, data);
            value = null;
            if (!cache.TryGetValue(key, out CacheElement responses))
                return false;
            if (responses.Expires < DateTime.Now)
                return false;
            value = responses.Responses.First().Data;
            return true;
        }

        public void ClearStale()
        {
            var toRemove = cache.Where(item => item.Value.Expires < DateTime.Now)
                            .Select(item => item.Key).ToList();
            foreach (var item in toRemove)
            {
                cache.TryRemove(item, out CacheElement value);
            }
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

        public class CacheElement
        {
            public DateTime Expires;
            public IEnumerable<MessageData> Responses => Reduced.Select(e=>new MessageData(e.type,Unzip(e.data)));

            private List<ReducedCommandData> Reduced;

            public CacheElement(DateTime expires, List<MessageData> responses)
            {
                Expires = expires;
                Reduced = responses.Select(CreateItem)
                            .ToList();
            }

            public void Add(MessageData data)
            {
                Reduced.Add(CreateItem(data));
            }

            private static ReducedCommandData CreateItem(MessageData m)
            {
                var compressed = Zip(m.Data);
                Console.WriteLine($"Compressed {m.Data.Length} to {compressed.Length}");
                return new ReducedCommandData(m.Type, compressed);
            }
        }

        public class ReducedCommandData
        {
            public string type;
            public byte[] data;

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
            });
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
            if(bytes.Length < 100)
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
            if(bytes.Length < 100)
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
    }
}