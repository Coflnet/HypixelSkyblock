using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Coflnet.Sky.Core;
using Coflnet.Sky.Core.Archive;
using MessagePack;
using NUnit.Framework;

namespace Tests
{
    /// <summary>Scylla-only columns (keys 31-38) of <see cref="ArchivedAuctionBlock"/> and the byte compatibility of the old overload.</summary>
    public class ArchivedAuctionBlockExtrasTests
    {
        private const int Month = 202609;
        private static readonly DateTime MonthStart = new(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);

        /// <summary>
        /// SHA-256 of the MessagePack bytes of <see cref="Sample"/> computed with the core library before keys 38+ and the
        /// extras overload existed. Every phase-1 block in R2 carries a hash computed that way.
        /// </summary>
        private const string GoldenSampleSha256 = "ffebaa918770fe6cf945d772545266adb1ac928e8305e27afe296f077a3fbbfb";

        private static List<SaveAuction> SampleRows() => new()
        {
            new ArchiveAuctionBuilder(20, MonthStart.AddDays(9).AddHours(3)).Full().Build(),
            new ArchiveAuctionBuilder(10, MonthStart.AddDays(2)).With(a => { a.Bin = true; a.HighestBidAmount = 0; }).Build(),
            new ArchiveAuctionBuilder(15, MonthStart.AddDays(29).AddHours(23).AddMinutes(59).AddSeconds(59)).With(a => a.NbtData = new NbtData { data = new byte[] { 1, 2, 3 } }).Build(),
        };

        private static ArchivedAuctionBlock Sample() => ArchivedAuctionBlock.FromAuctions(ArchiveAuctionBuilder.Tag, Month, 0, ArchivedAuctionBlock.SourceMariaDb, SampleRows());

        private static ArchivedAuctionBlock Codec(ArchivedAuctionBlock block)
        {
            using var ms = new MemoryStream();
            ArchiveBlobCodec.Write(ms, block);
            ms.Position = 0;
            return ArchiveBlobCodec.ReadAsync<ArchivedAuctionBlock>(ms).GetAwaiter().GetResult();
        }

        private static readonly Guid ItemGuid = Guid.Parse("0123456789abcdef0123456789abcdef");

        /// <summary>Row 20 has every extra, row 10 null color / empty lookup / no unknown enchantments, row 15 an empty item id.</summary>
        private static List<(SaveAuction, ScyllaAuctionExtras)> RowsWithExtras()
        {
            var rows = SampleRows();
            return new List<(SaveAuction, ScyllaAuctionExtras)>
            {
                (rows[0], new ScyllaAuctionExtras
                {
                    HighestBidderName = "Bidder",
                    ProfileName = "Mango",
                    CoopName = "Coop Name",
                    Color = 0x123456,
                    ItemUid = 0x1234_5678_9abc,
                    ItemId = ScyllaAuctionExtras.EncodeItemId(ItemGuid),
                    NbtLookupStrings = new Dictionary<string, string> { ["uid"] = "56789abc", ["anvil_uses"] = "4", ["Zeta"] = "z" },
                    UnknownEnchantments = new Dictionary<string, int> { ["unknown7"] = 3, ["future_ench"] = 9 },
                }),
                (rows[1], new ScyllaAuctionExtras
                {
                    Color = null,
                    ItemUid = 42,
                    ItemId = ScyllaAuctionExtras.EncodeItemId(ItemGuid),
                    NbtLookupStrings = new Dictionary<string, string>(),
                    UnknownEnchantments = new Dictionary<string, int>(),
                }),
                (rows[2], new ScyllaAuctionExtras { ItemId = ScyllaAuctionExtras.EncodeItemId(Guid.Empty), NbtLookupStrings = null }),
            };
        }

        [Test]
        public void OldOverloadKeepsTheBytesOfBeforeTheExtrasExisted()
        {
            var block = Sample();
            Assert.That(block.Sha256(), Is.EqualTo(GoldenSampleSha256));
            var bytes = MessagePackSerializer.Serialize(block);
            Assert.That(bytes.Take(3), Is.EqualTo(new byte[] { 0xdc, 0x00, 38 }), "array of 38 elements, keys 0-37");

            // the extras overload with only null extras takes the same code path and must not differ
            var viaExtras = ArchivedAuctionBlock.FromAuctions(ArchiveAuctionBuilder.Tag, Month, 0, ArchivedAuctionBlock.SourceMariaDb,
                SampleRows().Select(r => (r, (ScyllaAuctionExtras)null)));
            Assert.That(viaExtras.Sha256(), Is.EqualTo(GoldenSampleSha256));
            Assert.That(viaExtras.HighestBidderName, Is.Null);
            Assert.That(viaExtras.UnknownEnchantments, Is.Null);

            // a stored phase-1 block read back and re-hashed still matches the hash recorded in the tag index
            Assert.That(Codec(block).Sha256(), Is.EqualTo(GoldenSampleSha256));
        }

        [Test]
        public void ExtrasRoundTripEveryColumn()
        {
            var block = ArchivedAuctionBlock.FromAuctions(ArchiveAuctionBuilder.Tag, Month, 0, ArchivedAuctionBlock.SourceScylla, RowsWithExtras());
            var read = Codec(block);
            Assert.That(block.SameRows(read), Is.True, block.FirstDifference(read));
            Assert.That(read.AuctionUid, Is.EqualTo(new long[] { 10, 20, 15 }), "extras follow their rows through the sort");

            var extras = read.ToExtras();
            Assert.That(extras, Has.Count.EqualTo(3));
            var full = extras[1];
            Assert.Multiple(() =>
            {
                Assert.That(full.HighestBidderName, Is.EqualTo("Bidder"));
                Assert.That(full.ProfileName, Is.EqualTo("Mango"));
                Assert.That(full.CoopName, Is.EqualTo("Coop Name"));
                Assert.That(full.Color, Is.EqualTo(0x123456));
                Assert.That(full.ItemUid, Is.EqualTo(0x1234_5678_9abc));
                Assert.That(ArchiveIds.Decode(full.ItemId), Is.EqualTo(ItemGuid.ToString("N")));
                Assert.That(full.ItemId, Has.Length.EqualTo(16));
                Assert.That(full.NbtLookupStrings.Keys, Is.EqualTo(new[] { "Zeta", "anvil_uses", "uid" }), "sorted ordinal");
                Assert.That(full.NbtLookupStrings["uid"], Is.EqualTo("56789abc"));
                Assert.That(full.UnknownEnchantments, Is.EquivalentTo(new Dictionary<string, int> { ["future_ench"] = 9, ["unknown7"] = 3 }));

                var sparse = extras[0];
                Assert.That(sparse.Color, Is.Null);
                Assert.That(sparse.HighestBidderName, Is.Null);
                Assert.That(sparse.ItemUid, Is.EqualTo(42));
                Assert.That(sparse.NbtLookupStrings, Is.Empty);
                Assert.That(sparse.UnknownEnchantments, Is.Empty);
                Assert.That(read.NbtLookupStrings[0], Is.Null, "empty lookup is stored as null");
                Assert.That(read.UnknownEnchantments[0], Is.Null);

                Assert.That(extras[2].ItemId, Is.Null, "Guid.Empty item id is stored as null");
            });

            // the SaveAuction columns are the same as without extras
            var plain = ArchivedAuctionBlock.FromAuctions(ArchiveAuctionBuilder.Tag, Month, 0, ArchivedAuctionBlock.SourceScylla, SampleRows());
            Assert.That(read.ToAuctions().Select(a => (a.UId, a.Uuid, a.End, a.HighestBidAmount)), Is.EqualTo(plain.ToAuctions().Select(a => (a.UId, a.Uuid, a.End, a.HighestBidAmount))));
        }

        [Test]
        public void UnknownEnchantmentsGrowTheArrayOnlyWhenPresent()
        {
            var with = ArchivedAuctionBlock.FromAuctions(ArchiveAuctionBuilder.Tag, Month, 0, ArchivedAuctionBlock.SourceScylla, RowsWithExtras());
            Assert.That(MessagePackSerializer.Serialize(with).Take(3), Is.EqualTo(new byte[] { 0xdc, 0x00, 39 }));
            Assert.That(Codec(with).Sha256(), Is.EqualTo(with.Sha256()));

            var rows = RowsWithExtras();
            foreach (var (_, extras) in rows)
                extras.UnknownEnchantments = null;
            var without = ArchivedAuctionBlock.FromAuctions(ArchiveAuctionBuilder.Tag, Month, 0, ArchivedAuctionBlock.SourceScylla, rows);
            Assert.That(without.UnknownEnchantments, Is.Null);
            Assert.That(without.ItemUid, Is.Not.Null);
            Assert.That(MessagePackSerializer.Serialize(without).Take(3), Is.EqualTo(new byte[] { 0xdc, 0x00, 38 }));
        }

        [Test]
        public void SameRowsDetectsChangedUnknownEnchantmentsAndItemUid()
        {
            var block = ArchivedAuctionBlock.FromAuctions(ArchiveAuctionBuilder.Tag, Month, 0, ArchivedAuctionBlock.SourceScylla, RowsWithExtras());

            var rows = RowsWithExtras();
            rows[0].Item2.UnknownEnchantments = new Dictionary<string, int> { ["unknown7"] = 4, ["future_ench"] = 9 };
            var changedEnchantment = ArchivedAuctionBlock.FromAuctions(ArchiveAuctionBuilder.Tag, Month, 0, ArchivedAuctionBlock.SourceScylla, rows);
            Assert.That(block.FirstDifference(changedEnchantment), Is.EqualTo(nameof(ArchivedAuctionBlock.UnknownEnchantments)));

            rows = RowsWithExtras();
            rows[1].Item2.ItemUid = 43;
            var changedUid = ArchivedAuctionBlock.FromAuctions(ArchiveAuctionBuilder.Tag, Month, 0, ArchivedAuctionBlock.SourceScylla, rows);
            Assert.That(block.FirstDifference(changedUid), Is.EqualTo(nameof(ArchivedAuctionBlock.ItemUid)));
        }

        [Test]
        public void ToExtrasIsAllNullForABlockWithoutExtras()
        {
            var extras = Codec(Sample()).ToExtras();
            Assert.That(extras, Has.Count.EqualTo(3));
            Assert.That(extras, Has.All.Null);
        }

        [Test]
        public void FormatterSkipsUnknownTrailingKeys()
        {
            // a future writer may append keys; this version must still read the block
            var block = Sample();
            var bytes = MessagePackSerializer.Serialize(block);
            var buffer = new System.Buffers.ArrayBufferWriter<byte>();
            var writer = new MessagePackWriter(buffer);
            writer.WriteArrayHeader(41);
            writer.WriteRaw(bytes.AsSpan(3)); // the 38 elements
            writer.WriteNil();                // key 38
            writer.Write("future column");    // key 39
            writer.Write(7);                  // key 40
            writer.Flush();
            var read = MessagePackSerializer.Deserialize<ArchivedAuctionBlock>(buffer.WrittenMemory);
            Assert.That(block.SameRows(read), Is.True);
        }
    }

    public class ArchivedTagIndexETagTests
    {
        [Test]
        public void ETagIsNotSerialised()
        {
            var index = new ArchivedTagIndex { Tag = "HYPERION" }.Upsert(new ArchivedMonthEntry { Month = 202009, Rows = 1 });
            var without = MessagePackSerializer.Serialize(index);
            index.ETag = "\"abc\"";
            Assert.That(MessagePackSerializer.Serialize(index), Is.EqualTo(without), "existing index objects keep their bytes");
            Assert.That(MessagePackSerializer.Deserialize<ArchivedTagIndex>(without).ETag, Is.Null);
        }
    }

    public class ArchiveScyllaMonthManifestTests
    {
        [Test]
        public void UpsertReplacesTheEntryOfATagAndKeepsTagsSorted()
        {
            var manifest = new ScyllaMonthManifest { Month = 202009 }
                .Upsert(new ScyllaTagImport { Tag = "TERMINATOR", NewParts = 1 })
                .Upsert(new ScyllaTagImport { Tag = "HYPERION", NewParts = 1, NewPartSha256 = new() { "a" } })
                .Upsert(new ScyllaTagImport { Tag = "HYPERION", NewParts = 2, NewPartSha256 = new() { "b", "c" } });
            Assert.That(manifest.Tags.Select(t => (t.Tag, t.NewParts)), Is.EqualTo(new[] { ("HYPERION", 2), ("TERMINATOR", 1) }), "a re-run replaces, never duplicates");
            var read = MessagePackSerializer.Deserialize<ScyllaMonthManifest>(MessagePackSerializer.Serialize(manifest));
            Assert.That(read.Find("HYPERION").NewPartSha256, Is.EqualTo(new[] { "b", "c" }));
            Assert.That(read.CompletedAt, Is.Null);
        }

        [Test]
        public void ManifestAndMetaKeys()
        {
            Assert.That(AuctionBlobKeys.ScyllaManifest(202009), Is.EqualTo("auctions/v2/_meta/scylla/2020-09.msgpack.br"));
            Assert.That(AuctionBlobKeys.ScyllaManifestMonth("auctions/v2/_meta/scylla/2020-09.msgpack.br"), Is.EqualTo(202009));
            Assert.That(AuctionBlobKeys.ScyllaManifestMonth("auctions/v2/_meta/scylla/notes.txt"), Is.Null);
            Assert.That(AuctionBlobKeys.ScyllaManifestMonth("auctions/v2/_meta/scylla/2020-13.msgpack.br"), Is.Null);
            Assert.That(AuctionBlobKeys.Meta("scylla_partitions.msgpack.br"), Is.EqualTo("auctions/v2/_meta/scylla_partitions.msgpack.br"));
            Assert.That(AuctionBlobKeys.Meta(AuctionBlobKeys.ScyllaManifest(202009)), Is.EqualTo(AuctionBlobKeys.ScyllaManifest(202009)));
        }
    }

    public class ArchiveIndexSqlTests
    {
        [Test]
        public void LocationsAreBatchedAndRepointedByDefault()
        {
            var locations = Enumerable.Range(0, ArchiveIndexSql.BatchSize + 1).Select(i => ((long)i - 5, (byte)1)).ToList();
            var sql = ArchiveIndexSql.BuildLocationInserts(7, 93, locations).ToList();
            Assert.That(sql, Has.Count.EqualTo(2));
            Assert.That(sql[0], Does.StartWith("INSERT INTO auction_locations (uid, tag_id, month, part) VALUES (-5,7,93,1),(-4,7,93,1)"));
            Assert.That(sql[1], Is.EqualTo("INSERT INTO auction_locations (uid, tag_id, month, part) VALUES (4995,7,93,1) ON DUPLICATE KEY UPDATE tag_id = VALUES(tag_id), month = VALUES(month), part = VALUES(part)"));
        }

        [Test]
        public void KeepOtherTagMonthsOnlyRepointsTheSameTagMonth()
        {
            var sql = ArchiveIndexSql.BuildLocationInserts(7, 93, new[] { (11L, (byte)2) }, keepOtherTagMonths: true).Single();
            Assert.That(sql, Is.EqualTo("INSERT INTO auction_locations (uid, tag_id, month, part) VALUES (11,7,93,2) ON DUPLICATE KEY UPDATE part = IF(tag_id = VALUES(tag_id) AND month = VALUES(month), VALUES(part), part)"));
            Assert.That(sql, Does.Not.Contain("tag_id = VALUES(tag_id),"));
        }

        [Test]
        public void BiddersUseInsertIgnore()
        {
            var sql = ArchiveIndexSql.BuildBidderInserts(new[] { new AuctionBidder { BidderId = 3, End = 1700000000, Uid = -9, Amount = 12 } }).Single();
            Assert.That(sql, Is.EqualTo("INSERT IGNORE INTO auction_bidders (bidder_id, `end`, uid, amount) VALUES (3,1700000000,-9,12)"));
            Assert.That(ArchiveIndexSql.BuildBidderInserts(Array.Empty<AuctionBidder>()), Is.Empty);
        }
    }
}
