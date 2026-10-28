using System.Collections.Generic;
using Coflnet.Sky.Core;
using Confluent.Kafka;
using NUnit.Framework;

namespace Tests
{
    public class PartitionLagPolicyTests
    {
        [Test]
        public void UnsetCachedHigh_UsesBrokerAndReportsRealLag()
        {
            var queried = 0;
            var lag = PartitionLagPolicy.Lag(Offset.Unset.Value, 1000, () => { queried++; return 5000; });
            Assert.That(queried, Is.EqualTo(1));
            Assert.That(lag, Is.EqualTo(4000));
        }

        [Test]
        public void CachedHighBelowCommitted_UsesBroker()
        {
            Assert.That(PartitionLagPolicy.NeedsBrokerQuery(500, 1000), Is.True);
            Assert.That(PartitionLagPolicy.Lag(500, 1000, () => 1200), Is.EqualTo(200));
        }

        [Test]
        public void NormalCache_DoesNotQueryBroker()
        {
            var lag = PartitionLagPolicy.Lag(1500, 1000, () => throw new System.Exception("must not query"));
            Assert.That(lag, Is.EqualTo(500));
            Assert.That(PartitionLagPolicy.Lag(1000, 1000, () => throw new System.Exception("must not query")), Is.EqualTo(0));
        }

        [Test]
        public void BrokerQueryFailure_SkipsPartition()
        {
            Assert.That(PartitionLagPolicy.Lag(Offset.Unset.Value, 1000, () => null), Is.Null);
        }

        [Test]
        public void RevokedWhileInFlight_ResumesOnCompletionAndOnReassign()
        {
            // paused for an in-flight batch, revoked, batch finishes while unassigned
            Assert.That(PartitionLagPolicy.ShouldResumeOnDispatch(isAssigned: false, hasPendingMessages: false), Is.True);
            Assert.That(PartitionLagPolicy.ShouldResumeOnDispatch(isAssigned: false, hasPendingMessages: true), Is.True);
            // assigned with more queued work stays paused for the next batch
            Assert.That(PartitionLagPolicy.ShouldResumeOnDispatch(isAssigned: true, hasPendingMessages: true), Is.False);

            // reassigned later with nothing in flight
            var partition = new TopicPartition("topic", 0);
            var resume = PartitionLagPolicy.ResumeOnAssign(new[] { partition }, _ => false);
            Assert.That(resume, Is.EqualTo(new List<TopicPartition> { partition }));
        }

        [Test]
        public void ReassignedWithBatchStillInFlight_IsLeftToCompletionPath()
        {
            var partition = new TopicPartition("topic", 1);
            Assert.That(PartitionLagPolicy.ResumeOnAssign(new[] { partition }, _ => true), Is.Empty);
        }
    }
}
