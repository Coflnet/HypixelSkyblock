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
        private Func<string, TimeLimiter> NewLimiter;

        static IpRateLimiter()
        {
            Instance = new IpRateLimiter(DefaultLimiter());
        }

        public IpRateLimiter(Func<string, TimeLimiter> newLimiter)
        {
            this.NewLimiter = newLimiter;
        }

        public async Task WaitUntilAllowed(string ip)
        {
            var limiter = Limiters.GetOrAdd(ip.Truncate(10), DefaultLimiter());
            await limiter;
        }

        private static Func<string, TimeLimiter> DefaultLimiter()
        {
            return (id) =>
            {
                var constraint = new CountByIntervalAwaitableConstraint(1, TimeSpan.FromSeconds(1));
                var constraint2 = new CountByIntervalAwaitableConstraint(5, TimeSpan.FromSeconds(10));
                var heavyUsage = new CountByIntervalAwaitableConstraint(10, TimeSpan.FromMinutes(1));
                var abuse = new CountByIntervalAwaitableConstraint(50, TimeSpan.FromMinutes(20));

                // Compose the two constraints
                return TimeLimiter.Compose(constraint, constraint2, heavyUsage, abuse);
            };
        }
    }
}
