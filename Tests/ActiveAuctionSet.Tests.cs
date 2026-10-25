using System;
using System.Collections.Generic;
using System.Linq;
using Coflnet.Sky.Core;
using NUnit.Framework;

namespace Tests
{
    public class ActiveAuctionSetTests
    {
        private static SaveAuction Make(long uid, int bids = 0, string tag = "ASPECT_OF_THE_END", string seller = null, DateTime? end = null)
            => ActiveAuctionTestData.Make(uid, bids, tag, seller, end);

        [Test]
        public void UpsertAddsAndOverwrites()
        {
            var set = new ActiveAuctionSet();
            Assert.That(set.Upsert(Make(1), "test"), Is.True);
            Assert.That(set.Count, Is.EqualTo(1));
            var newer = Make(1, bids: 2);
            Assert.That(set.Upsert(newer, "test"), Is.True);
            Assert.That(set.Count, Is.EqualTo(1));
            Assert.That(set.TryGet(1, out var stored), Is.True);
            Assert.That(stored, Is.SameAs(newer));
            Assert.That(stored.Context, Is.Null);
        }

        [Test]
        public void InvalidAuctionsRejected()
        {
            var set = new ActiveAuctionSet();
            Assert.That(set.Upsert(null, "t"), Is.False);
            Assert.That(set.Upsert(Make(0), "t"), Is.False);
            Assert.That(set.Upsert(Make(5, end: DateTime.UtcNow.AddSeconds(-1)), "t"), Is.False);
            Assert.That(set.Count, Is.EqualTo(0));
        }

        [Test]
        public void StaleCopyIgnored()
        {
            var set = new ActiveAuctionSet();
            set.Upsert(Make(1, bids: 3), "t");
            Assert.That(set.Upsert(Make(1, bids: 1), "newbid"), Is.False);
            set.TryGet(1, out var stored);
            Assert.That(stored.Bids.Count, Is.EqualTo(3));
            var lowerAmount = Make(1, bids: 3);
            lowerAmount.HighestBidAmount = 1;
            Assert.That(set.Upsert(lowerAmount, "newbid"), Is.False);
        }

        [Test]
        public void TombstoneBlocksLateUpsertUntilPurged()
        {
            var set = new ActiveAuctionSet();
            set.Upsert(Make(1), "t");
            Assert.That(set.Remove(1, RemoveReason.Sold), Is.True);
            Assert.That(set.Upsert(Make(1), "late"), Is.False);
            Assert.That(set.PurgeTombstones(DateTime.UtcNow.AddMinutes(11)), Is.EqualTo(1));
            Assert.That(set.Upsert(Make(1), "late"), Is.True);
        }

        [Test]
        public void ExpireEndedRemovesOnlyEnded()
        {
            var set = new ActiveAuctionSet();
            set.Upsert(Make(1, end: DateTime.UtcNow.AddMinutes(1)), "t");
            set.Upsert(Make(2, end: DateTime.UtcNow.AddHours(2)), "t");
            Assert.That(set.ExpireEnded(DateTime.UtcNow.AddMinutes(5)), Is.EqualTo(1));
            Assert.That(set.TryGet(1, out _), Is.False);
            Assert.That(set.TryGet(2, out _), Is.True);
            Assert.That(set.UidEndTicks().Keys, Is.EquivalentTo(new[] { 2L }));
        }

        [Test]
        public void SnapshotVersionTracksChanges()
        {
            var set = new ActiveAuctionSet();
            set.Upsert(Make(1), "t");
            var first = set.RebuildSnapshotIfDirty();
            Assert.That(set.RebuildSnapshotIfDirty(), Is.SameAs(first));
            Assert.That(set.Snapshot, Is.SameAs(first));
            set.Upsert(Make(2), "t");
            var second = set.GetFreshSnapshot();
            Assert.That(second.Version, Is.GreaterThan(first.Version));
            Assert.That(second.Count, Is.EqualTo(2));
        }

        [Test]
        public void SnapshotIndices()
        {
            var set = new ActiveAuctionSet();
            set.Upsert(Make(3, tag: "A", seller: "s1"), "t");
            set.Upsert(Make(1, tag: "A", seller: "s2"), "t");
            set.Upsert(Make(2, tag: "B", seller: "s1"), "t");
            var snap = set.GetFreshSnapshot();
            Assert.That(snap.All.Select(a => a.UId), Is.EqualTo(new[] { 1L, 2L, 3L }));
            Assert.That(snap.ByTag["A"].Select(i => snap.All[i].UId), Is.EquivalentTo(new[] { 1L, 3L }));
            Assert.That(snap.ByTag["B"].Select(i => snap.All[i].UId), Is.EquivalentTo(new[] { 2L }));
            Assert.That(set.BySeller("s1").Select(a => a.UId), Is.EquivalentTo(new[] { 2L, 3L }));
            Assert.That(set.BySeller("nobody"), Is.Empty);
        }

        [Test]
        public void InternedStringsAreShared()
        {
            var set = new ActiveAuctionSet();
            var a = Make(1);
            var b = Make(2);
            set.Upsert(a, "t");
            set.Upsert(b, "t");
            Assert.That(a.Tag, Is.SameAs(b.Tag));
            Assert.That(a.FlatenedNBT.Keys.First(), Is.SameAs(b.FlatenedNBT.Keys.First()));
        }

        [Test]
        public void EventsFire()
        {
            var set = new ActiveAuctionSet();
            var upserts = new List<string>();
            var removes = new List<(long, RemoveReason)>();
            set.OnUpserted += (a, s) => upserts.Add(s);
            set.OnRemoved += (u, r) => removes.Add((u, r));
            set.OnUpserted += (a, s) => throw new Exception("ignored");
            set.Upsert(Make(1), "src");
            set.Remove(1, RemoveReason.Cancelled);
            Assert.That(upserts, Is.EqualTo(new[] { "src" }));
            Assert.That(removes, Is.EqualTo(new[] { (1L, RemoveReason.Cancelled) }));
        }
    }
}
