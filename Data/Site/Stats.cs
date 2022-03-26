using System;

namespace Coflnet.Sky.Core
{
    public class Stats
    {
        public int NameRequests { get; set; }
        public int Indexed { get; set; }

        public DateTime LastIndexFinish { get; set; }

        public DateTime LastBazaarUpdate { get; set; }

        public DateTime LastNameUpdate { get; set; }
        public DateTime LastAuctionPull { get; set; }

        public int CacheSize { get; set; }
        public int QueueSize { get; set; }
        public int FlipSize { get; set; }
        public int LastUpdateSize { get; set; }
        public int SubscriptionTobics { get; set; }
        public int ConnectionCount { get; set; }


    }
}
