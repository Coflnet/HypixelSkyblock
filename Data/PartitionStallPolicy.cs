using System;

namespace Coflnet.Sky.Core
{
    /// <summary>
    /// Decides when a lagging kafka partition counts as really stalled (and the consumer host should restart).
    /// </summary>
    public static class PartitionStallPolicy
    {
        /// <summary>
        /// A commit that keeps failing is only fatal after this many multiples of the stall timeout.
        /// </summary>
        public const int CommitFailureMultiplier = 2;

        /// <param name="lastActivity">last time a batch finished processing or a commit succeeded</param>
        /// <param name="commitFailingSince">null when no commit is waiting, otherwise when the last successful commit happened</param>
        public static bool IsStalled(DateTime now, DateTime lastActivity, DateTime? commitFailingSince, TimeSpan timeout)
        {
            if (now - lastActivity > timeout)
                return true;
            return commitFailingSince.HasValue && now - commitFailingSince.Value > timeout * CommitFailureMultiplier;
        }
    }
}
