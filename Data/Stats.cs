using System;

namespace hypixel
{
    public class Stats
        {
            public int NameRequests {get;set;}
            public int Indexed {get;set;}

            public DateTime LastIndexFinish {get;set;}

            public DateTime LastBazaarUpdate {get;set;}

            public DateTime LastNameUpdate {get;set;}

            public int CacheSize {get;set;}
            public int QueueSize {get;set;}
        }
}
