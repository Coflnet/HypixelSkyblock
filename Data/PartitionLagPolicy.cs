using System;
using System.Collections.Generic;
using System.Linq;
using Confluent.Kafka;

namespace Coflnet.Sky.Core
{
    /// <summary>
    /// Lag calculation and pause/resume decisions of the partitioned consumer, kept broker free for tests.
    /// </summary>
    public static class PartitionLagPolicy
    {
        /// <summary>
        /// The cached watermark is only trustworthy when it is a real offset and not behind the committed offset.
        /// </summary>
        public static bool NeedsBrokerQuery(long cachedHigh, long processedThrough)
        {
            if (processedThrough < 0)
                return false;
            return cachedHigh < 0 || cachedHigh < processedThrough;
        }

        /// <returns>the lag or null when it could not be determined (skip the partition this round)</returns>
        public static long? Lag(long cachedHigh, long processedThrough, Func<long?> queryHigh)
        {
            var high = cachedHigh;
            if (NeedsBrokerQuery(cachedHigh, processedThrough))
            {
                var queried = queryHigh();
                if (queried == null)
                    return null;
                high = queried.Value;
            }
            return Math.Max(0, high - Math.Max(0, processedThrough));
        }

        /// <summary>
        /// Newly assigned partitions without a running batch may still carry a pause from an earlier ownership.
        /// </summary>
        public static List<TopicPartition> ResumeOnAssign(IEnumerable<TopicPartition> assigned, Func<TopicPartition, bool> hasInFlightBatch)
            => assigned.Where(partition => !hasInFlightBatch(partition)).ToList();

        /// <summary>
        /// A partition must be resumed when it has no more work, or when it is no longer assigned
        /// (librdkafka keeps the pause flag for the lifetime of the handle).
        /// </summary>
        public static bool ShouldResumeOnDispatch(bool isAssigned, bool hasPendingMessages)
            => !isAssigned || !hasPendingMessages;
    }
}
