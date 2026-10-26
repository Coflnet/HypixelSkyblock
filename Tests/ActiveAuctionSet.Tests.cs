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

        private static readonly DateTime FixedEnd = DateTime.UtcNow.AddHours(2);

        [Test]
        public void IdenticalReUpsertDoesNotBump()
        {
            var set = new ActiveAuctionSet();
            var first = Make(1, bids: 1, end: FixedEnd);
            Assert.That(set.Upsert(first, "t"), Is.True);
            var version = set.Version;
            var changed = set.LastChange;
            Assert.That(set.Upsert(Make(1, bids: 1, end: FixedEnd), "t"), Is.False);
            Assert.That(set.Version, Is.EqualTo(version));
            Assert.That(set.LastChange, Is.EqualTo(changed));
            set.TryGet(1, out var stored);
            Assert.That(stored, Is.SameAs(first));
        }

        [Test]
        public void NewBidBumps()
        {
            var set = new ActiveAuctionSet();
            set.Upsert(Make(1, bids: 1, end: FixedEnd), "t");
            var version = set.Version;
            Assert.That(set.Upsert(Make(1, bids: 2, end: FixedEnd), "t"), Is.True);
            Assert.That(set.Version, Is.EqualTo(version + 1));
        }

        [Test]
        public void ChangedFieldsBump()
        {
            var set = new ActiveAuctionSet();
            var bare = Make(1, end: FixedEnd);
            bare.FlatenedNBT = null;
            set.Upsert(bare, "t");
            var noItem = Make(1, end: FixedEnd);
            noItem.ItemId = 0;
            noItem.FlatenedNBT = null;
            Assert.That(set.WouldAccept(noItem), Is.False, "unknown item id must not count as a change");
            var other = Make(1, end: FixedEnd.AddMinutes(1));
            other.FlatenedNBT = null;
            Assert.That(set.Upsert(other, "t"), Is.True);
            var renamed = Make(1, end: FixedEnd.AddMinutes(1));
            renamed.FlatenedNBT = null;
            renamed.ItemName = "something else";
            Assert.That(set.Upsert(renamed, "t"), Is.True);
            var withNbt = Make(1, end: FixedEnd.AddMinutes(1));
            withNbt.ItemName = "something else";
            withNbt.FlatenedNBT = new Dictionary<string, string> { ["color"] = "1:2:3" };
            Assert.That(set.Upsert(withNbt, "t"), Is.True);
            var again = Make(1, end: FixedEnd.AddMinutes(1));
            again.ItemName = "something else";
            again.FlatenedNBT = new Dictionary<string, string> { ["color"] = "1:2:3" };
            Assert.That(set.Upsert(again, "t"), Is.False);
        }

        [Test]
        public void ItemIdSetAfterZeroBumps()
        {
            var set = new ActiveAuctionSet();
            var first = Make(1, end: FixedEnd);
            first.ItemId = 0;
            set.Upsert(first, "t");
            Assert.That(set.Upsert(Make(1, end: FixedEnd), "t"), Is.True);
        }

        [Test]
        public void RemoveAndExpireOnlyBumpWhenRemoving()
        {
            var set = new ActiveAuctionSet();
            set.Upsert(Make(1, end: FixedEnd), "t");
            var version = set.Version;
            Assert.That(set.Remove(99, RemoveReason.Manual), Is.False);
            Assert.That(set.ExpireEnded(DateTime.UtcNow), Is.EqualTo(0));
            Assert.That(set.Version, Is.EqualTo(version));
            Assert.That(set.ExpireEnded(FixedEnd.AddMinutes(1)), Is.EqualTo(1));
            Assert.That(set.Version, Is.EqualTo(version + 1));
            set.Upsert(Make(2, end: FixedEnd), "t");
            version = set.Version;
            Assert.That(set.Remove(2, RemoveReason.Sold), Is.True);
            Assert.That(set.Version, Is.EqualTo(version + 1));
        }

        [Test]
        public void DirtySetDoesNotRebuildTwiceWithinInterval()
        {
            var set = new ActiveAuctionSet();
            set.Upsert(Make(1, end: FixedEnd), "t");
            var first = set.RebuildSnapshotIfDirty();
            Assert.That(set.RebuildCount, Is.EqualTo(1));
            set.Upsert(Make(2, end: FixedEnd), "t");
            Assert.That(set.RebuildSnapshotIfDirty(), Is.SameAs(first));
            Assert.That(set.RebuildCount, Is.EqualTo(1));
            set.SnapshotMinInterval = TimeSpan.Zero;
            Assert.That(set.RebuildSnapshotIfDirty().Count, Is.EqualTo(2));
            Assert.That(set.RebuildCount, Is.EqualTo(2));
        }

        [Test]
        public void FirstBuildAfterMarkReadyIsNotThrottled()
        {
            var set = new ActiveAuctionSet();
            set.Upsert(Make(1, end: FixedEnd), "t");
            set.RebuildSnapshotIfDirty();
            set.Upsert(Make(2, end: FixedEnd), "t");
            set.MarkReady();
            Assert.That(set.RebuildSnapshotIfDirty().Count, Is.EqualTo(2));
        }

        [Test]
        public void OrderByUidPreservedAfterInsertsAndRemoves()
        {
            var set = new ActiveAuctionSet { SnapshotMinInterval = TimeSpan.Zero };
            foreach (var uid in new long[] { 50, 10, 30, 20, 40, 60, 5 })
                set.Upsert(Make(uid, end: FixedEnd), "t");
            Assert.That(set.RebuildSnapshotIfDirty().All.Select(a => a.UId), Is.EqualTo(new long[] { 5, 10, 20, 30, 40, 50, 60 }));
            set.Remove(30, RemoveReason.Sold);
            set.Upsert(Make(25, end: FixedEnd), "t");
            set.Upsert(Make(70, end: FixedEnd), "t");
            set.Upsert(Make(10, bids: 2, end: FixedEnd), "t"); // replace keeps position
            Assert.That(set.RebuildSnapshotIfDirty().All.Select(a => a.UId), Is.EqualTo(new long[] { 5, 10, 20, 25, 40, 50, 60, 70 }));
        }

        [Test]
        public void RebuildEventReportsDuration()
        {
            var set = new ActiveAuctionSet();
            var seen = 0;
            set.OnSnapshotRebuilt += _ => seen++;
            set.Upsert(Make(1, end: FixedEnd), "t");
            set.RebuildSnapshotIfDirty();
            Assert.That(seen, Is.EqualTo(1));
            Assert.That(set.LastRebuildDuration, Is.GreaterThanOrEqualTo(TimeSpan.Zero));
        }

        private static string Anon(string suffix) => PlayerOptOut.AnonymousUuidPrefix + suffix;
        private const string RealSeller = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

        [Test]
        public void MaskedResightingOfRealAuctioneerIsAccepted()
        {
            var set = new ActiveAuctionSet();
            set.Upsert(Make(1, seller: RealSeller, end: FixedEnd), "t");
            var masked = Make(1, seller: Anon("0A"), end: FixedEnd);
            Assert.That(set.WouldAccept(masked), Is.True);
            Assert.That(set.Upsert(masked, "t"), Is.True);
            set.TryGet(1, out var stored);
            Assert.That(stored, Is.SameAs(masked));
            Assert.That(set.BySeller(RealSeller), Is.Empty);
            var maskedAgain = Make(1, seller: Anon("0B"), end: FixedEnd);
            Assert.That(set.WouldAccept(maskedAgain), Is.False, "a different random anonymous uuid is no change");
            Assert.That(set.Upsert(maskedAgain, "t"), Is.False);
        }

        [Test]
        public void MaskedProfileIdIsAccepted()
        {
            var set = new ActiveAuctionSet();
            set.Upsert(Make(1, seller: RealSeller, end: FixedEnd), "t");
            var masked = Make(1, seller: RealSeller, end: FixedEnd);
            masked.ProfileId = null;
            Assert.That(set.Upsert(masked, "t"), Is.True);
        }

        [Test]
        public void MaskedBidderIsAcceptedOnceOnly()
        {
            var set = new ActiveAuctionSet();
            var first = Make(1, bids: 1, end: FixedEnd);
            first.Bids[0].Bidder = RealSeller;
            set.Upsert(first, "t");
            var masked = Make(1, bids: 1, end: FixedEnd);
            masked.Bids[0].Bidder = Anon("11");
            Assert.That(set.Upsert(masked, "t"), Is.True);
            var maskedAgain = Make(1, bids: 1, end: FixedEnd);
            maskedAgain.Bids[0].Bidder = Anon("22");
            Assert.That(set.Upsert(maskedAgain, "t"), Is.False);
        }

        [Test]
        public void GetFreshSnapshotIgnoresInterval()
        {
            var set = new ActiveAuctionSet();
            set.Upsert(Make(1, end: FixedEnd), "t");
            set.RebuildSnapshotIfDirty();
            set.Upsert(Make(2, end: FixedEnd), "t");
            Assert.That(set.RebuildSnapshotIfDirty().Count, Is.EqualTo(1));
            Assert.That(set.GetFreshSnapshot().Count, Is.EqualTo(2));
        }

        [Test]
        public void RandomOrderBulkLoadIsOrderedAfterMarkReady()
        {
            var set = new ActiveAuctionSet { SnapshotMinInterval = TimeSpan.Zero };
            var uids = Enumerable.Range(1, 1000).Select(i => (long)i).OrderBy(_ => Random.Shared.Next()).ToList();
            foreach (var uid in uids)
                set.Upsert(Make(uid, end: FixedEnd), "t");
            set.Remove(500, RemoveReason.Manual); // removal while unsorted
            set.MarkReady();
            var expected = Enumerable.Range(1, 1000).Where(i => i != 500).Select(i => (long)i).ToList();
            Assert.That(set.RebuildSnapshotIfDirty().All.Select(a => a.UId), Is.EqualTo(expected));
            set.Upsert(Make(1500, end: FixedEnd), "t");
            set.Upsert(Make(2000, end: FixedEnd), "t");
            set.Upsert(Make(1100, end: FixedEnd), "t");
            Assert.That(set.RebuildSnapshotIfDirty().All.Select(a => a.UId).Skip(999), Is.EqualTo(new long[] { 1100, 1500, 2000 }));
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
            var set = new ActiveAuctionSet { SnapshotMinInterval = TimeSpan.Zero };
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
