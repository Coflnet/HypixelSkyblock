using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using RateLimiter;
using ComposableAsync;

namespace hypixel
{
    /// <summary>
    /// Limits induvidual ips from spamming
    /// </summary>
    public class IpRateLimiter
    {
        public static IpRateLimiter Instance { get; set; }
        private ConcurrentDictionary<string, TimeLimiter> Limiters = new ConcurrentDictionary<string, TimeLimiter>();

        static IpRateLimiter()
        {
            Instance = new IpRateLimiter();
        }

        public async Task WaitUntilAllowed(string ip)
        {
            var limiter = Limiters.GetOrAdd(ip, (id) =>
            {
                var constraint = new CountByIntervalAwaitableConstraint(1, TimeSpan.FromSeconds(1));
                var constraint2 = new CountByIntervalAwaitableConstraint(5, TimeSpan.FromSeconds(10));
                var heavyUsage = new CountByIntervalAwaitableConstraint(10, TimeSpan.FromMinutes(1));
                var abuse = new CountByIntervalAwaitableConstraint(50, TimeSpan.FromMinutes(20));

                // Compose the two constraints
                return TimeLimiter.Compose(constraint, constraint2, heavyUsage,abuse);
            });

            await limiter;
        }
    }
}
