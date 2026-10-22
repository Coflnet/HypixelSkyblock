using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Coflnet.Sky.Core;
using Coflnet.Sky.Core.Archive;
using MessagePack;
using NUnit.Framework;

namespace Tests
{
    public class ArchivedAuctionBlockTests
    {
        private const int Month = 202609;
        private static readonly DateTime MonthStart = new(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);

        private static ArchivedAuctionBlock RoundTrip(ArchivedAuctionBlock block)
            => MessagePackSerializer.Deserialize<ArchivedAuctionBlock>(MessagePackSerializer.Serialize(block));

        private static List<SaveAuction> SampleRows() => new()
        {
            new ArchiveAuctionBuilder(20, MonthStart.AddDays(9).AddHours(3)).Full().Build(),
            new ArchiveAuctionBuilder(10, MonthStart.AddDays(2)).With(a => { a.Bin = true; a.HighestBidAmount = 0; }).Build(),
            new ArchiveAuctionBuilder(15, MonthStart.AddDays(29).AddHours(23).AddMinutes(59).AddSeconds(59)).With(a => a.NbtData = new NbtData { data = new byte[] { 1, 2, 3 } }).Build(),
        };

        private static ArchivedAuctionBlock Sample() => ArchivedAuctionBlock.FromAuctions(ArchiveAuctionBuilder.Tag, Month, 0, ArchivedAuctionBlock.SourceMariaDb, SampleRows());

        [Test]
        public void RoundTripKeepsEveryColumn()
        {
            var source = SampleRows();
            var restored = RoundTrip(Sample()).ToAuctions().ToList();
            Assert.That(restored.Select(r => r.UId), Is.EqualTo(new long[] { 10, 20, 15 }), "sorted by End then uid");
            foreach (var r in restored)
            {
                var s = source.Single(x => x.UId == r.UId);
                Assert.Multiple(() =>
                {
                    Assert.That(r.Uuid, Is.EqualTo(s.Uuid));
                    Assert.That(r.Tag, Is.EqualTo(s.Tag));
                    Assert.That(r.End, Is.EqualTo(s.End));
                    Assert.That(r.End.Kind, Is.EqualTo(DateTimeKind.Utc));
                    Assert.That(r.Start, Is.EqualTo(s.Start));
                    Assert.That(r.Bin, Is.EqualTo(s.Bin));
                    Assert.That(r.Claimed, Is.EqualTo(s.Claimed));
                    Assert.That(r.AuctioneerId, Is.EqualTo(s.AuctioneerId));
                    Assert.That(r.ProfileId, Is.EqualTo(s.ProfileId));
                    Assert.That(r.HighestBidAmount, Is.EqualTo(s.HighestBidAmount));
                    Assert.That(r.StartingBid, Is.EqualTo(s.StartingBid));
                    Assert.That(r.Count, Is.EqualTo(s.Count));
                    Assert.That(r.Tier, Is.EqualTo(s.Tier));
                    Assert.That(r.Category, Is.EqualTo(s.Category));
                    Assert.That(r.Reforge, Is.EqualTo(s.Reforge));
                    Assert.That(r.AnvilUses, Is.EqualTo(s.AnvilUses));
                    Assert.That(r.ItemName, Is.EqualTo(s.ItemName));
                    Assert.That(r.ItemCreatedAt, Is.EqualTo(s.ItemCreatedAt));
                    Assert.That(r.NbtData?.data, Is.EqualTo(s.NbtData?.data));
                    Assert.That(r.Bids.Select(b => (b.Bidder, b.ProfileId, b.Amount, b.Timestamp, b.AuctionId)),
                        Is.EqualTo(s.Bids.Select(b => (b.Bidder, b.ProfileId, b.Amount, b.Timestamp, s.Uuid))));
                    Assert.That(r.Enchantments.Select(e => (e.Type, e.Level)), Is.EqualTo(s.Enchantments.Select(e => (e.Type, e.Level))));
                    Assert.That(r.NBTLookup.Select(l => (l.KeyId, l.Value)), Is.EquivalentTo(s.NBTLookup.Select(l => (l.KeyId, l.Value))));
                    Assert.That(r.CoopMembers.Select(c => c.value), Is.EqualTo((s.CoopMembers ?? new()).Select(c => c.value)));
                    Assert.That(r.ClaimedBids.Select(c => c.value), Is.EqualTo((s.ClaimedBids ?? new()).Select(c => c.value)));
                });
            }
        }

        [Test]
        public void HighestBidderIsTheMaxBidAndNullWithoutBids()
        {
            var block = Sample();
            Assert.That(ArchiveIds.Decode(block.HighestBidder[1]), Is.EqualTo(ArchiveAuctionBuilder.Hex(4000)));
            Assert.That(block.HighestBidder[0], Is.Null);
            Assert.That(block.IsSold, Is.EqualTo(new[] { false, true, false }));
            Assert.That(block.Uuid[0], Has.Length.EqualTo(16));
        }

        [Test]
        public void SameRowsDetectsChangedItemByteAndMissingRow()
        {
            var block = Sample();
            Assert.That(block.SameRows(RoundTrip(block)), Is.True);

            var changed = RoundTrip(block);
            changed.ItemBytes[1][3] ^= 1;
            Assert.That(block.SameRows(changed), Is.False);
            Assert.That(block.FirstDifference(changed), Is.EqualTo(nameof(ArchivedAuctionBlock.ItemBytes)));

            var missing = ArchivedAuctionBlock.FromAuctions(ArchiveAuctionBuilder.Tag, Month, 0, ArchivedAuctionBlock.SourceMariaDb, SampleRows().Take(2));
            Assert.That(block.SameRows(missing), Is.False);
            Assert.That(block.SameRows(null), Is.False);
        }

        /// <summary>Guards against a new column that <see cref="ArchivedAuctionBlock.SameRows"/> does not compare.</summary>
        [Test]
        public void SameRowsComparesEveryKeyedColumn()
        {
            var block = Sample();
            var keyed = typeof(ArchivedAuctionBlock).GetProperties().Where(p => p.GetCustomAttribute<KeyAttribute>() != null).ToList();
            Assert.That(keyed, Has.Count.EqualTo(38));
            foreach (var property in keyed)
            {
                var copy = RoundTrip(block);
                Mutate(copy, property, block.RowCount);
                Assert.That(block.SameRows(copy), Is.False, property.Name + " is not compared");
            }
        }

        private static void Mutate(ArchivedAuctionBlock target, PropertyInfo property, int rows)
        {
            var value = property.GetValue(target);
            var type = property.PropertyType;
            if (type == typeof(int))
                property.SetValue(target, (int)value + 1);
            else if (type == typeof(long))
                property.SetValue(target, (long)value + 1);
            else if (type == typeof(string))
                property.SetValue(target, value + "x");
            else if (type.IsArray)
                property.SetValue(target, value == null ? Array.CreateInstance(type.GetElementType(), rows) : null);
            else
                Assert.Fail("Unhandled column type " + type);
        }

        [Test]
        public void FromAuctionsRejectsRowOutsideMonthOrOfAnotherTag()
        {
            var outside = SampleRows();
            outside.Add(new ArchiveAuctionBuilder(99, MonthStart.AddMonths(1)).Build());
            Assert.Throws<ArgumentException>(() => ArchivedAuctionBlock.FromAuctions(ArchiveAuctionBuilder.Tag, Month, 0, ArchivedAuctionBlock.SourceMariaDb, outside));

            var before = new List<SaveAuction> { new ArchiveAuctionBuilder(98, MonthStart.AddTicks(-TimeSpan.TicksPerSecond)).Build() };
            Assert.Throws<ArgumentException>(() => ArchivedAuctionBlock.FromAuctions(ArchiveAuctionBuilder.Tag, Month, 0, ArchivedAuctionBlock.SourceMariaDb, before));

            var otherTag = SampleRows();
            otherTag[0].Tag = "TERMINATOR";
            Assert.Throws<ArgumentException>(() => ArchivedAuctionBlock.FromAuctions(ArchiveAuctionBuilder.Tag, Month, 0, ArchivedAuctionBlock.SourceMariaDb, otherTag));
        }

        [Test]
        public void SortsByEndThenUid()
        {
            var end = MonthStart.AddDays(3);
            var rows = new[] { 7L, 3L, 5L }.Select(uid => new ArchiveAuctionBuilder(uid, end).Build()).ToList();
            rows.Add(new ArchiveAuctionBuilder(1, end.AddSeconds(1)).Build());
            var block = ArchivedAuctionBlock.FromAuctions(ArchiveAuctionBuilder.Tag, Month, 0, ArchivedAuctionBlock.SourceMariaDb, rows);
            Assert.That(block.AuctionUid, Is.EqualTo(new long[] { 3, 5, 7, 1 }));
            Assert.That(block.FirstEndTicks, Is.EqualTo(end.Ticks));
            Assert.That(block.EndDeltasMs, Is.EqualTo(new long[] { 0, 0, 0, 1000 }));
        }

        [Test]
        public void MonthLongDeltasDoNotOverflow()
        {
            var rows = new List<SaveAuction>
            {
                new ArchiveAuctionBuilder(1, new DateTime(2026, 8, 1)).Build(),
                new ArchiveAuctionBuilder(2, new DateTime(2026, 8, 31, 23, 59, 59)).Build(),
            };
            rows.ForEach(r => r.Tag = ArchiveAuctionBuilder.Tag);
            var block = ArchivedAuctionBlock.FromAuctions(ArchiveAuctionBuilder.Tag, 202608, 0, ArchivedAuctionBlock.SourceMariaDb, rows);
            Assert.That(block.EndDeltasMs[1], Is.GreaterThan(int.MaxValue));
            Assert.That(RoundTrip(block).ToAuctions().Last().End, Is.EqualTo(new DateTime(2026, 8, 31, 23, 59, 59)));
        }

        [Test]
        public void CoversUidsAndEstimatedBytes()
        {
            var block = Sample();
            Assert.That(block.CoversUids(new long[] { 10, 15, 20 }), Is.True);
            Assert.That(block.CoversUids(new long[] { 10, 11 }), Is.False);

            var small = block.EstimatedBytes();
            var bigger = SampleRows();
            bigger[0].NbtData = new NbtData { data = new byte[10_000] };
            var big = ArchivedAuctionBlock.FromAuctions(ArchiveAuctionBuilder.Tag, Month, 0, ArchivedAuctionBlock.SourceMariaDb, bigger).EstimatedBytes();
            Assert.That(big, Is.GreaterThanOrEqualTo(small + 10_000 - 16));
        }

        [Test]
        public void Sha256IsStableAndChangesWithData()
        {
            var block = Sample();
            Assert.That(block.Sha256(), Has.Length.EqualTo(64));
            Assert.That(RoundTrip(block).Sha256(), Is.EqualTo(block.Sha256()));
            var changed = RoundTrip(block);
            changed.StartingBid[0]++;
            Assert.That(changed.Sha256(), Is.Not.EqualTo(block.Sha256()));
            Assert.That(ArchivedAuctionBlock.CombinedSha256(new[] { block.Sha256() }), Is.EqualTo(block.Sha256()));
            Assert.That(ArchivedAuctionBlock.CombinedSha256(new[] { "a", "b" }), Is.Not.EqualTo(ArchivedAuctionBlock.CombinedSha256(new[] { "b", "a" })));
        }

        [Test]
        public void CodecRoundTripsCompressedBlock()
        {
            var block = Sample();
            using var ms = new MemoryStream();
            ArchiveBlobCodec.Write(ms, block);
            ms.Position = 0;
            var read = ArchiveBlobCodec.ReadAsync<ArchivedAuctionBlock>(ms).GetAwaiter().GetResult();
            Assert.That(block.SameRows(read), Is.True);
        }

        [TestCase("0123456789abcdef0123456789abcdef", 16)]
        [TestCase("0123456789ABCDEF0123456789ABCDEF", 34)]
        [TestCase("unknown", 9)]
        [TestCase("", 2)]
        [TestCase("exactly14bytes", 17)] // 2 + 14 would be 16 and look like a uuid without the pad byte
        public void IdsRoundTripLosslessly(string id, int encodedLength)
        {
            var encoded = ArchiveIds.Encode(id);
            Assert.That(encoded, Has.Length.EqualTo(encodedLength));
            Assert.That(ArchiveIds.Decode(encoded), Is.EqualTo(id));
            Assert.That(ArchiveIds.Encode(null), Is.Null);
            Assert.That(ArchiveIds.Decode(null), Is.Null);
        }
    }

    public class ArchiveMonthsTests
    {
        [Test]
        public void MonthOfUsesUtcBoundaries()
        {
            Assert.That(ArchiveMonths.MonthOf(new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc)), Is.EqualTo(202610));
            Assert.That(ArchiveMonths.MonthOf(new DateTime(2026, 9, 30, 23, 59, 59, 999, DateTimeKind.Utc)), Is.EqualTo(202609));
            Assert.That(ArchivedAuctionBlock.MonthRange(202612), Is.EqualTo((new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc))));
        }

        [Test]
        public void MonthIsClosedFourteenDaysAfterItsEnd()
        {
            var end = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
            Assert.That(ArchiveMonths.IsClosed(202609, end.AddDays(14)), Is.True);
            Assert.That(ArchiveMonths.IsClosed(202609, end.AddDays(14).AddSeconds(-1)), Is.False);
            Assert.That(ArchiveMonths.IsClosed(202610, end.AddDays(20)), Is.False);
            Assert.That(ArchiveMonths.NewestClosed(new DateTime(2026, 10, 21, 0, 0, 0, DateTimeKind.Utc)), Is.EqualTo(202609));
            Assert.That(ArchiveMonths.NewestClosed(new DateTime(2026, 10, 14, 0, 0, 0, DateTimeKind.Utc)), Is.EqualTo(202608));
        }

        [Test]
        public void OldestInWindowAndIndexConversion()
        {
            Assert.That(ArchiveMonths.OldestInWindow(new DateTime(2026, 10, 21, 0, 0, 0, DateTimeKind.Utc), 3), Is.EqualTo(202310));
            Assert.That(ArchiveMonths.ToIndex(201901), Is.EqualTo(0));
            Assert.That(ArchiveMonths.ToIndex(202610), Is.EqualTo(93));
            Assert.That(ArchiveMonths.FromIndex(93), Is.EqualTo(202610));
            Assert.Throws<ArgumentOutOfRangeException>(() => ArchiveMonths.ToIndex(201812));
            Assert.That(ArchiveMonths.Parse("2019-01"), Is.EqualTo(201901));
            Assert.That(ArchiveMonths.Format(201901), Is.EqualTo("2019-01"));
            Assert.That(ArchiveMonths.Next(202612), Is.EqualTo(202701));
            Assert.That(ArchiveMonths.Previous(202701), Is.EqualTo(202612));
        }

        [Test]
        public void BlobKeys()
        {
            Assert.That(AuctionBlobKeys.Block("HYPERION", 202610, 7), Is.EqualTo("auctions/v2/HYPERION/2026-10/007.msgpack.br"));
            Assert.That(AuctionBlobKeys.Index("HYPERION"), Is.EqualTo("auctions/v2/HYPERION/index.msgpack.br"));
            Assert.That(AuctionBlobKeys.Block("PET_SKIN_A-B", 201907, 0), Is.EqualTo("auctions/v2/PET_SKIN_A-B/2019-07/000.msgpack.br"));
            Assert.That(AuctionBlobKeys.Index("A/B"), Is.EqualTo("auctions/v2/A%2FB/index.msgpack.br"));
        }

        [Test]
        public void TagIndexUpsertReplacesAndKeepsOrder()
        {
            var index = new ArchivedTagIndex { Tag = "HYPERION" };
            index.Upsert(new ArchivedMonthEntry { Month = 202602, Rows = 1 })
                 .Upsert(new ArchivedMonthEntry { Month = 202601, Rows = 2 })
                 .Upsert(new ArchivedMonthEntry { Month = 202602, Rows = 3, PartSha256 = new() { "abc" } });
            Assert.That(index.Months.Select(m => (m.Month, m.Rows)), Is.EqualTo(new[] { (202601, 2), (202602, 3) }));
            var read = MessagePackSerializer.Deserialize<ArchivedTagIndex>(MessagePackSerializer.Serialize(index));
            Assert.That(read.Find(202602).PartSha256, Is.EqualTo(new[] { "abc" }));
        }
    }
}
