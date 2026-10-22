using System;
using Coflnet.Sky.Core;
using NUnit.Framework;

namespace Tests
{
    public class PartitionStallPolicyTests
    {
        private static readonly DateTime Now = new(2026, 10, 22, 16, 0, 0, DateTimeKind.Utc);
        private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(3);

        [Test]
        public void FailingCommitWithRecentBatchesIsNotAStall()
        {
            // "Not coordinator": commits fail for 10 minutes but batches keep completing
            Assert.That(PartitionStallPolicy.IsStalled(Now, Now.AddSeconds(-5), Now.AddMinutes(-5), Timeout), Is.False);
        }

        [Test]
        public void NoActivityForLongerThanTimeoutIsAStall()
        {
            Assert.That(PartitionStallPolicy.IsStalled(Now, Now.AddMinutes(-4), null, Timeout), Is.True);
        }

        [Test]
        public void CommitFailingForever_EventuallyRestarts()
        {
            Assert.That(PartitionStallPolicy.IsStalled(Now, Now.AddSeconds(-1), Now.AddMinutes(-7), Timeout), Is.True);
        }
    }
}
