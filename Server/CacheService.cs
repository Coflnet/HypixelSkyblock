using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace hypixel
{
    public class CacheService
    {
        public static CacheService Instance {get;protected set;}

        private ConcurrentDictionary<string,CacheElement> cache = new ConcurrentDictionary<string, CacheElement>();

        public int CacheSize => cache.Count;

        static CacheService()
        {
            Instance = new CacheService();
        }

        public void Save(MessageData request, MessageData response, int index)
        {
            string key = GetCacheKey(request);

            var newEntry = new CacheElement(DateTime.Now+TimeSpan.FromSeconds(response.MaxAxe), new List<MessageData>(){response});
            cache.AddOrUpdate(key,newEntry,(key,item)=>{
                if(index == 0)
                    return newEntry;
                item.Responses.Add(response);
                return item;
            });
        }

        public bool TryFromCache(MessageData request)
        {
            var key = GetCacheKey(request);
            if(!cache.TryGetValue(key,out CacheElement responses))
                return false;
            
            if(responses.Expires < DateTime.Now)
                return false;

            foreach (var item in responses.Responses)
            {
                // copy to prevent another thread from modifying te mId
                var response = MessageData.Copy(item);
                // adjust the cache time to when it expires on the server
                response.MaxAxe = (int)(responses.Expires - DateTime.Now).TotalSeconds;
                request.SendBack(response,false);
            }
            return true;
        }

        public void ClearStale()
        {
            var toRemove = cache.Where(item=>item.Value.Expires < DateTime.Now)
                            .Select(item=>item.Key).ToList();
            foreach (var item in toRemove)
            {
                cache.TryRemove(item,out CacheElement value);
            }
        }

        private static string GetCacheKey(MessageData request)
        {
            var key = request.CustomCacheKey;
            if (key == null)
                key = request.Type + request.Data;
            return key;
        }

        public class CacheElement
        {
            public DateTime Expires;
            public List<MessageData> Responses;

            public CacheElement(DateTime expires, List<MessageData> responses)
            {
                Expires = expires;
                Responses = responses;
            }
        }

        internal void RunForEver()
        {
            var tenMin = (int)TimeSpan.FromMinutes(10).TotalMilliseconds;
            Task.Run(async ()=>{
                while(true)
                {
                    await Task.Delay(tenMin);
                    ClearStale();
                }
            });
        }
    }
}