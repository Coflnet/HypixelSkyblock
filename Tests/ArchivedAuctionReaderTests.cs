using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Coflnet.Sky.Core;
using Coflnet.Sky.Core.Archive;
using fNbt;
using fNbt.Tags;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Tests
{
    /// <summary>Dictionary-backed <see cref="IAuctionBlobStore"/> storing the real compressed wire format and logging every call.</summary>
    internal class InMemoryArchiveStore : IAuctionBlobStore
    {
        public readonly Dictionary<string, byte[]> Objects = new();
        public readonly List<string> Events = new();
        /// <summary>When set, every block read flips a bit of row 0's highest bid, like a corrupted upload.</summary>
        public bool CorruptReads;

        public bool IsEnabled => true;

        public Task<ArchivedAuctionBlock> ReadBlockAsync(string tag, int month, int part, bool uncached = false, CancellationToken cancellationToken = default)
        {
            Events.Add($"read:{tag}:{month}:{part}:{(uncached ? "uncached" : "cached")}");
            var block = Get<ArchivedAuctionBlock>(AuctionBlobKeys.Block(tag, month, part));
            if (block != null && CorruptReads && block.RowCount > 0)
                block.HighestBidAmount[0] ^= 1;
            return Task.FromResult(block);
        }

        public Task WriteBlockAsync(ArchivedAuctionBlock block, CancellationToken cancellationToken = default)
        {
            Put(AuctionBlobKeys.Block(block.Tag, block.Month, block.Part), block);
            return Task.CompletedTask;
        }

        public Task<bool> BlockExistsAsync(string tag, int month, int part, CancellationToken cancellationToken = default)
            => Task.FromResult(Objects.ContainsKey(AuctionBlobKeys.Block(tag, month, part)));

        public Task<ArchivedTagIndex> ReadIndexAsync(string tag, CancellationToken cancellationToken = default)
        {
            Events.Add($"index:{tag}");
            return Task.FromResult(Get<ArchivedTagIndex>(AuctionBlobKeys.Index(tag)));
        }

        public Task WriteIndexAsync(ArchivedTagIndex index, CancellationToken cancellationToken = default)
        {
            Put(AuctionBlobKeys.Index(index.Tag), index);
            return Task.CompletedTask;
        }

        public IEnumerable<string> BlockReads => Events.Where(e => e.StartsWith("read:", StringComparison.Ordinal));

        private void Put<T>(string key, T value)
        {
            using var ms = new MemoryStream();
            ArchiveBlobCodec.Write(ms, value);
            Objects[key] = ms.ToArray();
        }

        private T Get<T>(string key) where T : class
        {
            if (!Objects.TryGetValue(key, out var bytes))
                return null;
            using var ms = new MemoryStream(bytes);
            return ArchiveBlobCodec.ReadAsync<T>(ms).GetAwaiter().GetResult();
        }
    }

    /// <summary>In-memory <see cref="IArchiveLocationLookup"/> (the MariaDB tables).</summary>
    internal class InMemoryLocationLookup : IArchiveLocationLookup
    {
        public readonly Dictionary<ushort, string> Tags = new();
        public readonly Dictionary<long, AuctionLocation> Locations = new();
        public readonly List<AuctionBidder> Bidders = new();
        public readonly List<AuctionSeller> Sellers = new();
        public readonly Dictionary<(string, int), ArchivedMonth> Months = new();
        public readonly List<string> Calls = new();

        public Task<AuctionLocation> GetLocationAsync(long uid, CancellationToken cancellationToken = default)
        {
            Calls.Add("location:" + uid);
            return Task.FromResult(Locations.GetValueOrDefault(uid));
        }

        public Task<IReadOnlyDictionary<long, AuctionLocation>> GetLocationsAsync(IReadOnlyCollection<long> uids, CancellationToken cancellationToken = default)
        {
            Calls.Add("locations:" + uids.Count);
            IReadOnlyDictionary<long, AuctionLocation> result = uids.Where(Locations.ContainsKey).Distinct().ToDictionary(u => u, u => Locations[u]);
            return Task.FromResult(result);
        }

        public Task<string> GetTagNameAsync(ushort tagId, CancellationToken cancellationToken = default)
            => Task.FromResult(Tags.GetValueOrDefault(tagId));

        public Task<IReadOnlyList<AuctionBidder>> GetBidderRowsAsync(uint bidderId, uint fromUnixSeconds, uint toUnixSeconds, int limit, AuctionIndexCursor before = null, CancellationToken cancellationToken = default)
        {
            Calls.Add($"bidders:{bidderId}:{fromUnixSeconds}:{toUnixSeconds}:{limit}");
            IReadOnlyList<AuctionBidder> rows = Bidders.Where(b => b.BidderId == bidderId && b.End >= fromUnixSeconds && b.End < toUnixSeconds && (before == null || b.End < before.End || b.End == before.End && b.Uid < before.Uid))
                .OrderByDescending(b => b.End).ThenByDescending(b => b.Uid).Take(limit).ToList();
            return Task.FromResult(rows);
        }

        public Task<IReadOnlyList<AuctionSeller>> GetSellerRowsAsync(uint sellerId, uint fromUnixSeconds, uint toUnixSeconds, int limit, AuctionIndexCursor before = null, CancellationToken cancellationToken = default)
        {
            Calls.Add($"sellers:{sellerId}:{fromUnixSeconds}:{toUnixSeconds}:{limit}");
            IReadOnlyList<AuctionSeller> rows = Sellers.Where(b => b.SellerId == sellerId && b.End >= fromUnixSeconds && b.End < toUnixSeconds && (before == null || b.End < before.End || b.End == before.End && b.Uid < before.Uid))
                .OrderByDescending(b => b.End).ThenByDescending(b => b.Uid).Take(limit).ToList();
            return Task.FromResult(rows);
        }

        public Task<ArchivedMonth> GetArchivedMonthAsync(string tag, int month, CancellationToken cancellationToken = default)
            => Task.FromResult(Months.GetValueOrDefault((tag, month)));
    }

    public class ArchivedAuctionReaderTests
    {
        private const string Tag = ArchiveAuctionBuilder.Tag;
        private const ushort TagId = 7;
        private const int Sep = 202009;
        private static readonly DateTime SepStart = new(2020, 9, 1, 0, 0, 0, DateTimeKind.Utc);

        private InMemoryArchiveStore store;
        private InMemoryLocationLookup lookup;
        private ArchivedAuctionReader reader;

        [SetUp]
        public void SetUp()
        {
            store = new InMemoryArchiveStore();
            lookup = new InMemoryLocationLookup();
            lookup.Tags[TagId] = Tag;
            reader = new ArchivedAuctionReader(store, lookup, NullLogger.Instance);
        }

        private static SaveAuction Row(long uid, int day, Action<SaveAuction> change = null)
        {
            var builder = new ArchiveAuctionBuilder(uid, SepStart.AddDays(day).AddHours(uid % 24));
            if (change != null)
                builder.With(change);
            return builder.Build();
        }

        /// <summary>Write the parts, the tag index entry, the locations and a matching archived_months row.</summary>
        private List<ArchivedAuctionBlock> Archive(int month, params ArchivedAuctionBlock[] parts)
        {
            var written = new List<ArchivedAuctionBlock>();
            foreach (var block in parts)
            {
                store.WriteBlockAsync(block).Wait();
                written.Add(block);
                foreach (var uid in block.AuctionUid)
                    lookup.Locations[uid] = new AuctionLocation { Uid = uid, TagId = TagId, Month = ArchiveMonths.ToIndex(month), Part = (byte)block.Part };
            }
            var hashes = written.Select(b => b.Sha256()).ToList();
            var index = (store.ReadIndexAsync(Tag).Result ?? new ArchivedTagIndex { Tag = Tag }).Upsert(new ArchivedMonthEntry
            {
                Month = month,
                Parts = written.Count,
                Rows = written.Sum(b => b.RowCount),
                SoldRows = written.Sum(b => b.IsSold.Count(s => s)),
                PartSha256 = hashes,
                Source = written.Count > 1 ? ArchivedAuctionBlock.SourceMerged : written[0].Source,
                VerifiedAt = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
            });
            store.WriteIndexAsync(index).Wait();
            lookup.Months[(Tag, month)] = new ArchivedMonth
            {
                TagId = TagId,
                Month = ArchiveMonths.ToIndex(month),
                Parts = (byte)written.Count,
                Rows = (uint)written.Sum(b => b.RowCount),
                Sha256 = ArchivedAuctionBlock.CombinedSha256(hashes),
                Source = index.Find(month).Source,
            };
            return written;
        }

        /// <summary>
        /// Phase-1 part 0 (uids 1-4, MariaDB) and a phase-2 part 1 (uids 5-7, Scylla extras) whose Ends interleave with part 0,
        /// like a merged month.
        /// </summary>
        private void ArchiveMergedSeptember()
        {
            var part0 = ArchivedAuctionBlock.FromAuctions(Tag, Sep, 0, ArchivedAuctionBlock.SourceMariaDb, new[]
            {
                Row(1, 1, a => { a.HighestBidAmount = 1000; a.Bin = true; }),
                Row(2, 5, a => { a.HighestBidAmount = 0; a.StartingBid = 3000; a.ItemName = "Withered Hyperion"; }),
                Row(3, 10, a => { a.HighestBidAmount = 5000; a.AuctioneerId = ArchiveAuctionBuilder.Hex(77); }),
                Row(4, 20, a => { a.HighestBidAmount = 0; a.StartingBid = 100; a.Bin = true; }),
            });
            var part1 = ArchivedAuctionBlock.FromAuctions(Tag, Sep, 1, ArchivedAuctionBlock.SourceScylla, new[]
            {
                (Row(5, 3, a => a.HighestBidAmount = 2000), Extras("heroic", "Alice")),
                (Row(6, 12, a => a.HighestBidAmount = 0), Extras("fabled", null)),
                (Row(7, 25, a => { a.HighestBidAmount = 7000; a.Bin = true; }), Extras("heroic", "Bob")),
            });
            Archive(Sep, part0, part1);
        }

        private static ScyllaAuctionExtras Extras(string modifier, string bidderName) => new()
        {
            HighestBidderName = bidderName,
            NbtLookupStrings = new Dictionary<string, string> { ["modifier"] = modifier, ["uid"] = "abc" },
            UnknownEnchantments = bidderName == null ? new Dictionary<string, int>() : new Dictionary<string, int> { ["future_ench"] = 2 },
        };

        private async Task<List<long>> Uids(ArchivedAuctionQuery query, int month = Sep)
        {
            var result = new List<long>();
            await foreach (var a in reader.ReadMonthAsync(Tag, month, query))
                result.Add(a.Row.UId);
            return result;
        }

        // ------------------------------------------------------------ month reads

        [Test]
        public async Task MonthStreamsBothPartsMergedByEnd()
        {
            ArchiveMergedSeptember();
            var rows = new List<ArchivedAuction>();
            await foreach (var a in reader.ReadMonthAsync(Tag, Sep))
                rows.Add(a);

            Assert.That(rows.Select(r => r.Row.UId), Is.EqualTo(new long[] { 1, 5, 2, 3, 6, 4, 7 }), "sorted by End across overlapping parts");
            Assert.That(rows.Select(r => r.Part), Is.EqualTo(new[] { 0, 1, 0, 0, 1, 0, 1 }));
            Assert.That(rows.Select(r => r.Source).Distinct(), Is.EquivalentTo(new[] { ArchivedAuctionBlock.SourceMariaDb, ArchivedAuctionBlock.SourceScylla }));
            Assert.That(rows.Single(r => r.Row.UId == 1).Extras, Is.Null, "MariaDB block has no extras");
            Assert.That(rows.Single(r => r.Row.UId == 5).Extras.HighestBidderName, Is.EqualTo("Alice"));
            Assert.That(rows.All(r => r.Month == Sep));
            Assert.That(store.BlockReads, Is.EqualTo(new[] { $"read:{Tag}:{Sep}:0:cached", $"read:{Tag}:{Sep}:1:cached" }), "each part read once");
        }

        [Test]
        public async Task DescendingWithLimitTakesTheNewestAcrossParts()
        {
            ArchiveMergedSeptember();
            Assert.That(await Uids(new ArchivedAuctionQuery { Descending = true, Limit = 3 }), Is.EqualTo(new long[] { 7, 4, 6 }));
            Assert.That(await Uids(new ArchivedAuctionQuery { Limit = 2 }), Is.EqualTo(new long[] { 1, 5 }));
            Assert.That(await Uids(new ArchivedAuctionQuery { Limit = 0 }), Is.Empty);
        }

        [Test]
        public async Task LazyMergeOfLargeMonthsGivesTheSameOrder()
        {
            ArchiveMergedSeptember();
            reader.SinglePassMergeMaxRows = 0;
            Assert.That(await Uids(null), Is.EqualTo(new long[] { 1, 5, 2, 3, 6, 4, 7 }));
            Assert.That(await Uids(new ArchivedAuctionQuery { Descending = true }), Is.EqualTo(new long[] { 7, 4, 6, 3, 2, 5, 1 }));
            // range pass + merge pass, two reads per part per query
            Assert.That(store.BlockReads.Count(), Is.EqualTo(8));
        }

        [Test]
        public async Task LazyMergeOfDisjointPartsHoldsOnePartAtATime()
        {
            // two phase-1 parts that do not overlap: the second is only loaded once the first is exhausted
            var part0 = ArchivedAuctionBlock.FromAuctions(Tag, Sep, 0, ArchivedAuctionBlock.SourceMariaDb, new[] { Row(1, 1), Row(2, 2) });
            var part1 = ArchivedAuctionBlock.FromAuctions(Tag, Sep, 1, ArchivedAuctionBlock.SourceMariaDb, new[] { Row(3, 10), Row(4, 11) });
            Archive(Sep, part0, part1);
            reader.SinglePassMergeMaxRows = 0;
            var seen = new List<string>();
            await foreach (var a in reader.ReadMonthAsync(Tag, Sep, new ArchivedAuctionQuery { Limit = 2 }))
                seen.Add(a.Row.UId.ToString());
            Assert.That(seen, Is.EqualTo(new[] { "1", "2" }));
            Assert.That(store.BlockReads, Is.EqualTo(new[]
            {
                $"read:{Tag}:{Sep}:0:cached", $"read:{Tag}:{Sep}:1:cached", // range pass
                $"read:{Tag}:{Sep}:0:cached", // merge: part 1 starts after part 0's rows, limit reached first
            }));
        }

        [Test]
        public async Task EveryFilterFieldAppliesToTheMonth()
        {
            ArchiveMergedSeptember();
            Assert.That(await Uids(new ArchivedAuctionQuery { EndFrom = SepStart.AddDays(5), EndTo = SepStart.AddDays(20) }), Is.EqualTo(new long[] { 2, 3, 6 }));
            Assert.That(await Uids(new ArchivedAuctionQuery { Sold = true }), Is.EqualTo(new long[] { 1, 5, 3, 7 }));
            Assert.That(await Uids(new ArchivedAuctionQuery { Sold = false }), Is.EqualTo(new long[] { 2, 6, 4 }));
            Assert.That(await Uids(new ArchivedAuctionQuery { Bin = true }), Is.EqualTo(new long[] { 1, 4, 7 }));
            // unsold rows are priced by their starting bid (2: 3000, 4: 100, 6: 500)
            Assert.That(await Uids(new ArchivedAuctionQuery { MinPrice = 2000, MaxPrice = 5000 }), Is.EqualTo(new long[] { 5, 2, 3 }));
            Assert.That(await Uids(new ArchivedAuctionQuery { Auctioneer = "00000000-0000-0000-0000-00000000004D" }), Is.EqualTo(new long[] { 3 }));
            Assert.That(await Uids(new ArchivedAuctionQuery { ItemNameContains = "WITHERED" }), Is.EqualTo(new long[] { 2 }));
            Assert.That(await Uids(new ArchivedAuctionQuery { NbtLookup = new Dictionary<string, string> { ["modifier"] = "heroic" } }), Is.EqualTo(new long[] { 5, 7 }));
            Assert.That(await Uids(new ArchivedAuctionQuery { Sold = true, Bin = true, Descending = true }), Is.EqualTo(new long[] { 7, 1 }));
        }

        [Test]
        public async Task MonthsOutsideTheIndexOrTheEndWindowReadNothing()
        {
            ArchiveMergedSeptember();
            Assert.That(await Uids(null, 202010), Is.Empty);
            Assert.That(await Uids(new ArchivedAuctionQuery { EndFrom = new DateTime(2020, 10, 1) }), Is.Empty);
            Assert.That(store.BlockReads, Is.Empty, "no part read when nothing can match");
            Assert.That(await reader.GetMonthsAsync("UNKNOWN"), Is.Empty);
            Assert.That(await reader.GetTagIndexAsync("UNKNOWN"), Is.Null);
            var months = await reader.GetMonthsAsync(Tag);
            Assert.That(months.Select(m => (m.Month, m.Parts, m.Rows, m.SoldRows)), Is.EqualTo(new[] { (Sep, 2, 7, 4) }));
        }

        [Test]
        public void PartListedInTheIndexButMissingThrows()
        {
            ArchiveMergedSeptember();
            store.Objects.Remove(AuctionBlobKeys.Block(Tag, Sep, 1));
            Assert.ThrowsAsync<InvalidDataException>(async () => await Uids(null));
        }

        // ------------------------------------------------------------ query

        [Test]
        public void QueryMatchesEachField()
        {
            var sold = Row(1, 3, a => { a.HighestBidAmount = 900; a.StartingBid = 50; a.Bin = false; a.ItemName = "Heroic Hyperion ✪"; });
            var unsold = Row(2, 3, a => { a.HighestBidAmount = 0; a.StartingBid = 2000; a.Bin = true; });

            Assert.That(new ArchivedAuctionQuery().Matches(sold, null), Is.True);
            Assert.That(new ArchivedAuctionQuery().Matches(null, null), Is.False);
            Assert.That(new ArchivedAuctionQuery { EndFrom = sold.End }.Matches(sold, null), Is.True, "EndFrom inclusive");
            Assert.That(new ArchivedAuctionQuery { EndTo = sold.End }.Matches(sold, null), Is.False, "EndTo exclusive");
            Assert.That(new ArchivedAuctionQuery { EndFrom = DateTime.SpecifyKind(sold.End.AddMilliseconds(1), DateTimeKind.Unspecified) }.Matches(sold, null), Is.False, "Unspecified is UTC");
            Assert.That(new ArchivedAuctionQuery { Sold = true }.Matches(sold, null), Is.True);
            Assert.That(new ArchivedAuctionQuery { Sold = true }.Matches(unsold, null), Is.False);
            Assert.That(new ArchivedAuctionQuery { Bin = true }.Matches(unsold, null), Is.True);
            Assert.That(new ArchivedAuctionQuery { Bin = true }.Matches(sold, null), Is.False);
            Assert.That(new ArchivedAuctionQuery { MinPrice = 900, MaxPrice = 900 }.Matches(sold, null), Is.True, "sold: highest bid");
            Assert.That(new ArchivedAuctionQuery { MaxPrice = 100 }.Matches(sold, null), Is.False, "not the starting bid when sold");
            Assert.That(new ArchivedAuctionQuery { MinPrice = 2000 }.Matches(unsold, null), Is.True, "unsold: starting bid");
            Assert.That(new ArchivedAuctionQuery { MinPrice = 2001 }.Matches(unsold, null), Is.False);
            Assert.That(new ArchivedAuctionQuery { Auctioneer = sold.AuctioneerId.ToUpperInvariant() }.Matches(sold, null), Is.True);
            Assert.That(new ArchivedAuctionQuery { Auctioneer = unsold.AuctioneerId }.Matches(sold, null), Is.False);
            Assert.That(new ArchivedAuctionQuery { ItemNameContains = "hYpErIoN ✪" }.Matches(sold, null), Is.True);
            Assert.That(new ArchivedAuctionQuery { ItemNameContains = "Valkyrie" }.Matches(sold, null), Is.False);

            var extras = new ScyllaAuctionExtras { NbtLookupStrings = new Dictionary<string, string> { ["modifier"] = "heroic", ["rarity_upgrades"] = "1" } };
            Assert.That(new ArchivedAuctionQuery { NbtLookup = new Dictionary<string, string> { ["modifier"] = "heroic", ["rarity_upgrades"] = "1" } }.Matches(sold, extras), Is.True);
            Assert.That(new ArchivedAuctionQuery { NbtLookup = new Dictionary<string, string> { ["modifier"] = "Heroic" } }.Matches(sold, extras), Is.False, "values are ordinal");
            Assert.That(new ArchivedAuctionQuery { NbtLookup = new Dictionary<string, string> { ["missing"] = "x" } }.Matches(sold, extras), Is.False);
            Assert.That(new ArchivedAuctionQuery { NbtLookup = new Dictionary<string, string> { ["modifier"] = "heroic" } }.Matches(sold, null), Is.False, "no map, no item bytes");
            Assert.That(new ArchivedAuctionQuery { NbtLookup = new Dictionary<string, string>() }.Matches(sold, null), Is.True, "empty lookup filter is no filter");
        }

        [Test]
        public void NbtLookupFallsBackToTheItemBytesWithoutScyllaMap()
        {
            var bytes = new NbtFile(new NbtCompound("") { new NbtString("modifier", "heroic"), new NbtInt("anvil_uses", 3) }).SaveToBuffer(NbtCompression.None);
            var row = Row(1, 3, a => a.NbtData = new NbtData { data = bytes });
            var query = new ArchivedAuctionQuery { NbtLookup = new Dictionary<string, string> { ["modifier"] = "heroic", ["anvil_uses"] = "3" } };

            Assert.That(query.Matches(row, null), Is.True, "MariaDB row: flattened from the item bytes");
            Assert.That(query.Matches(row, new ScyllaAuctionExtras { NbtLookupStrings = new Dictionary<string, string>() }), Is.True, "empty Scylla map falls back too");
            Assert.That(query.Matches(row, new ScyllaAuctionExtras { NbtLookupStrings = new Dictionary<string, string> { ["modifier"] = "fabled" } }), Is.False, "Scylla map wins when present");
            var garbage = Row(2, 3, a => a.NbtData = new NbtData { data = new byte[] { 10, 0, 1, 2, 3 } });
            Assert.That(query.Matches(garbage, null), Is.False, "unparsable bytes: no map, no exception");
        }

        // ------------------------------------------------------------ lookups

        [Test]
        public async Task FindByUidReadsExactlyOneBlobAndNoIndex()
        {
            ArchiveMergedSeptember();
            store.Events.Clear();

            var found = await reader.FindByUidAsync(6);

            Assert.That(found.Row.UId, Is.EqualTo(6));
            Assert.That(found.Part, Is.EqualTo(1));
            Assert.That(found.Extras.NbtLookupStrings["modifier"], Is.EqualTo("fabled"));
            Assert.That(store.Events, Is.EqualTo(new[] { $"read:{Tag}:{Sep}:1:cached" }));
            Assert.That(lookup.Calls, Is.EqualTo(new[] { "location:6" }));
            Assert.That(await reader.FindByUidAsync(99), Is.Null);
        }

        [Test]
        public async Task FindByUuidMapsTheUuidToItsUid()
        {
            var uuid = "a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6";
            var uid = AuctionService.Instance.GetId(uuid);
            var block = ArchivedAuctionBlock.FromAuctions(Tag, Sep, 0, ArchivedAuctionBlock.SourceMariaDb, new[] { Row(uid, 4, a => a.Uuid = uuid) });
            Archive(Sep, block);

            var found = await reader.FindByUuidAsync("A1B2C3D4-E5F6-A7B8-C9D0-E1F2A3B4C5D6");
            Assert.That(found?.Row.Uuid, Is.EqualTo(uuid));

            // same uid prefix, different uuid: a collision is not a hit
            Assert.That(await reader.FindByUuidAsync("a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5ff"), Is.Null);
            Assert.That(await reader.FindByUuidAsync("not-a-uuid"), Is.Null);
            Assert.That(await reader.FindByUuidAsync(null), Is.Null);
        }

        [Test]
        public async Task FindByBidderReadsEachBlockOnce()
        {
            ArchiveMergedSeptember();
            uint End(long uid) => (uint)new DateTimeOffset(lookup.Locations.ContainsKey(uid) ? SepStart.AddDays(uid) : SepStart).ToUnixTimeSeconds();
            foreach (var uid in new long[] { 1, 2, 3, 5, 6 })
                lookup.Bidders.Add(new AuctionBidder { BidderId = 42, Uid = uid, End = End(uid) });
            lookup.Bidders.Add(new AuctionBidder { BidderId = 42, Uid = 12345, End = End(4) }); // no location
            lookup.Bidders.Add(new AuctionBidder { BidderId = 43, Uid = 4, End = End(4) });
            store.Events.Clear();

            var found = await reader.FindByBidderAsync(42, null, null, 100);

            Assert.That(found.Select(a => a.Row.UId), Is.EqualTo(new long[] { 6, 3, 2, 5, 1 }), "newest End first");
            Assert.That(store.BlockReads, Is.EquivalentTo(new[] { $"read:{Tag}:{Sep}:0:cached", $"read:{Tag}:{Sep}:1:cached" }));
            Assert.That(lookup.Calls.Count(c => c.StartsWith("locations:")), Is.EqualTo(1), "one batched location lookup");

            Assert.That((await reader.FindByBidderAsync(42, null, null, 2)).Count, Is.EqualTo(2));
            Assert.That(await reader.FindByBidderAsync(42, 202010, 202012, 100), Is.Empty);
            Assert.That(lookup.Calls.Last(), Is.EqualTo($"bidders:42:{new DateTimeOffset(2020, 10, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds()}:{new DateTimeOffset(2021, 1, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds()}:100"));
            Assert.That(await reader.FindByBidderAsync(42, null, null, 0), Is.Empty);
        }

        [Test]
        public async Task FindBySellerReadsEachBlockOnce()
        {
            ArchiveMergedSeptember();
            uint End(long uid) => (uint)new DateTimeOffset(lookup.Locations.ContainsKey(uid) ? SepStart.AddDays(uid) : SepStart).ToUnixTimeSeconds();
            foreach (var uid in new long[] { 1, 2, 3, 5, 6 })
                lookup.Sellers.Add(new AuctionSeller { SellerId = 42, Uid = uid, End = End(uid) });
            lookup.Sellers.Add(new AuctionSeller { SellerId = 42, Uid = 12345, End = End(4) }); // no location
            lookup.Sellers.Add(new AuctionSeller { SellerId = 43, Uid = 4, End = End(4) });
            lookup.Bidders.Add(new AuctionBidder { BidderId = 42, Uid = 4, End = End(4) }); // bids are not sells
            store.Events.Clear();

            var found = await reader.FindBySellerAsync(42, null, null, 100);

            Assert.That(found.Select(a => a.Row.UId), Is.EqualTo(new long[] { 6, 3, 2, 5, 1 }), "newest End first");
            Assert.That(store.BlockReads, Is.EquivalentTo(new[] { $"read:{Tag}:{Sep}:0:cached", $"read:{Tag}:{Sep}:1:cached" }));
            Assert.That(lookup.Calls.Count(c => c.StartsWith("locations:")), Is.EqualTo(1), "one batched location lookup");

            Assert.That((await reader.FindBySellerAsync(42, null, null, 2)).Count, Is.EqualTo(2));
            Assert.That(await reader.FindBySellerAsync(42, 202010, 202012, 100), Is.Empty);
            Assert.That(lookup.Calls.Last(), Is.EqualTo($"sellers:42:{new DateTimeOffset(2020, 10, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds()}:{new DateTimeOffset(2021, 1, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds()}:100"));
            Assert.That(await reader.FindBySellerAsync(42, null, null, 0), Is.Empty);
            Assert.That(await reader.FindBySellerAsync(99, null, null, 10), Is.Empty);
        }

        [Test]
        public async Task FindBySellerAndBidderPageWithACursor()
        {
            ArchiveMergedSeptember();
            // index ends equal the archived rows' ends (day of month per uid, see ArchiveMergedSeptember)
            uint End(long uid, int day) => (uint)new DateTimeOffset(SepStart.AddDays(day).AddHours(uid % 24)).ToUnixTimeSeconds();
            foreach (var (uid, end) in new[] { (1L, End(1, 1)), (2L, End(2, 5)), (3L, End(3, 10)), (5L, End(5, 3)), (6L, End(6, 12)) })
            {
                lookup.Sellers.Add(new AuctionSeller { SellerId = 42, Uid = uid, End = end });
                lookup.Bidders.Add(new AuctionBidder { BidderId = 42, Uid = uid, End = end });
            }

            var all = (await reader.FindBySellerAsync(42, null, null, 100)).Select(a => a.Row.UId).ToList();
            Assert.That(all, Is.EqualTo(new long[] { 6, 3, 2, 5, 1 }));

            var seen = new List<long>();
            AuctionIndexCursor cursor = null;
            for (var page = 0; page < 6; page++)
            {
                var got = await reader.FindBySellerAsync(42, null, null, 2, cursor);
                if (got.Count == 0)
                    break;
                seen.AddRange(got.Select(a => a.Row.UId));
                cursor = AuctionIndexCursor.After(got[^1].Row.End, got[^1].Row.UId);
            }
            Assert.That(seen, Is.EqualTo(all), "pages of 2 cover everything exactly once");

            var bidders = await reader.FindByBidderAsync(42, null, null, 100, new AuctionIndexCursor(End(2, 5), 2));
            Assert.That(bidders.Select(a => a.Row.UId), Is.EqualTo(new long[] { 5, 1 }), "strictly after the cursor row");
        }

        /// <summary>Opted-out players never show up on any read path, even though the stored part still holds them.</summary>
        [Test]
        public async Task OptedOutParticipantsAreMaskedOnEveryReadPath()
        {
            var optedOut = PlayerOptOut.LegacyPlayerUuids[0];
            var secret = Row(1, 1, a =>
            {
                a.AuctioneerId = optedOut;
                a.ProfileId = optedOut;
                a.HighestBidAmount = 100;
                a.Bids = new List<SaveBids> { new() { Bidder = optedOut, ProfileId = optedOut, BidderId = 5, Amount = 100, Timestamp = SepStart } };
            });
            var other = Row(2, 2, a => a.HighestBidAmount = 50);
            var part = ArchivedAuctionBlock.FromAuctions(Tag, Sep, 0, ArchivedAuctionBlock.SourceScylla, new[]
            {
                (secret, new ScyllaAuctionExtras { HighestBidderName = "Secret", ProfileName = "SecretProfile", NbtLookupStrings = new Dictionary<string, string>() }),
                (other, new ScyllaAuctionExtras { HighestBidderName = "Visible", NbtLookupStrings = new Dictionary<string, string>() }),
            });
            Archive(Sep, part);
            lookup.Sellers.Add(new AuctionSeller { SellerId = 3, Uid = 1, End = (uint)new DateTimeOffset(secret.End, TimeSpan.Zero).ToUnixTimeSeconds() });

            void AssertMasked(ArchivedAuction a)
            {
                Assert.That(a.Row.AuctioneerId, Does.StartWith(PlayerOptOut.AnonymousUuidPrefix));
                Assert.That(a.Row.SellerId, Is.Zero);
                Assert.That(a.Row.ProfileId, Is.Null);
                Assert.That(a.HighestBidder, Does.StartWith(PlayerOptOut.AnonymousUuidPrefix));
                Assert.That(a.Row.Bids.Single().BidderId, Is.Zero);
                Assert.That(a.Extras.HighestBidderName, Is.Null);
                Assert.That(a.Extras.ProfileName, Is.Null);
            }
            AssertMasked(await reader.FindByUidAsync(1));
            AssertMasked(await reader.FindByUuidAsync(secret.Uuid));
            AssertMasked((await reader.FindBySellerAsync(3, null, null, 10)).Single());
            var month = new List<ArchivedAuction>();
            await foreach (var a in reader.ReadMonthAsync(Tag, Sep))
                month.Add(a);
            AssertMasked(month.Single(a => a.Row.UId == 1));
            Assert.That(month.Single(a => a.Row.UId == 2).Extras.HighestBidderName, Is.EqualTo("Visible"), "others are untouched");
            var byAuctioneer = new List<ArchivedAuction>();
            await foreach (var a in reader.ReadMonthAsync(Tag, Sep, new ArchivedAuctionQuery { Auctioneer = optedOut }))
                byAuctioneer.Add(a);
            Assert.That(byAuctioneer, Is.Empty, "filtering by an opted-out seller finds nothing");
        }

        [Test]
        public void CursorParsesAndRoundTrips()
        {
            Assert.That(AuctionIndexCursor.TryParse("1599048000:-5", out var c), Is.True);
            Assert.That(c, Is.EqualTo(new AuctionIndexCursor(1599048000, -5)));
            Assert.That(c.ToString(), Is.EqualTo("1599048000:-5"));
            foreach (var bad in new[] { null, "", "5", ":5", "x:5", "5:x", "-1:5", "99999999999:5" })
                Assert.That(AuctionIndexCursor.TryParse(bad, out _), Is.False, bad);
        }

        // ------------------------------------------------------------ verify

        [Test]
        public async Task VerifyMatchesIndexAndDatabase()
        {
            ArchiveMergedSeptember();
            store.Events.Clear();

            var result = await reader.VerifyMonthAsync(Tag, Sep);

            Assert.That(result.FirstDifference, Is.Null);
            Assert.That((result.Parts, result.Rows, result.SoldRows), Is.EqualTo((2, 7, 4)));
            Assert.That(result.HashesMatchIndex, Is.True);
            Assert.That(result.HashesMatchDb, Is.True);
            Assert.That(store.BlockReads, Is.EqualTo(new[] { $"read:{Tag}:{Sep}:0:uncached", $"read:{Tag}:{Sep}:1:uncached" }));
        }

        [Test]
        public async Task VerifyReportsCorruptedBlocks()
        {
            ArchiveMergedSeptember();
            store.CorruptReads = true;

            var result = await reader.VerifyMonthAsync(Tag, Sep);

            Assert.That(result.HashesMatchIndex, Is.False);
            Assert.That(result.HashesMatchDb, Is.False);
            Assert.That(result.FirstDifference, Does.StartWith("part 0: tag index sha256"));
        }

        [Test]
        public async Task VerifyComparesTheDatabaseHashOverTheImportedPartsOnly()
        {
            ArchiveMergedSeptember();
            // SkyIndexer has not imported the phase-2 part yet: the row still describes part 0 alone
            var part0Hash = (await reader.VerifyMonthAsync(Tag, Sep)).PartSha256[0];
            lookup.Months[(Tag, Sep)] = new ArchivedMonth { Parts = 1, Rows = 4, Sha256 = part0Hash, Source = ArchivedAuctionBlock.SourceMariaDb };

            var result = await reader.VerifyMonthAsync(Tag, Sep);

            Assert.That(result.HashesMatchDb, Is.True);
            Assert.That(result.DbParts, Is.EqualTo(1));
            Assert.That(result.FirstDifference, Is.Null);
        }

        [Test]
        public async Task VerifyWithoutIndexEntryOrRow()
        {
            var result = await reader.VerifyMonthAsync(Tag, Sep);
            Assert.That(result.Parts, Is.EqualTo(0));
            Assert.That(result.HashesMatchIndex, Is.False);
            Assert.That(result.HashesMatchDb, Is.Null);
            Assert.That(result.FirstDifference, Is.EqualTo("tag index missing"));
        }

        // ------------------------------------------------------------ export

        [Test]
        public void EmptyCsvExportIsTheHeaderOnly()
        {
            var writer = new StringWriter();
            Assert.That(ArchiveExport.WriteCsv(Array.Empty<ArchivedAuction>(), writer), Is.EqualTo(0));
            Assert.That(writer.ToString(), Is.EqualTo(string.Join(",", ArchiveExport.StandardColumns) + "\r\n"));
            var jsonl = new StringWriter();
            Assert.That(ArchiveExport.WriteJsonLines(Array.Empty<ArchivedAuction>(), jsonl), Is.EqualTo(0));
            Assert.That(jsonl.ToString(), Is.Empty);
        }

        [Test]
        public void CsvQuotesCommasQuotesAndLineBreaks()
        {
            Assert.That(ArchiveExport.CsvField("plain"), Is.EqualTo("plain"));
            Assert.That(ArchiveExport.CsvField(null), Is.EqualTo(""));
            Assert.That(ArchiveExport.CsvField("a,b"), Is.EqualTo("\"a,b\""));
            Assert.That(ArchiveExport.CsvField("say \"hi\""), Is.EqualTo("\"say \"\"hi\"\"\""));
            Assert.That(ArchiveExport.CsvField("two\nlines"), Is.EqualTo("\"two\nlines\""));
            Assert.That(ArchiveExport.CsvField("cr\r"), Is.EqualTo("\"cr\r\""));
            Assert.That(ArchiveExport.CsvLine(new[] { "a", null, "b,c" }), Is.EqualTo("a,,\"b,c\"\r\n"));
        }

        [Test]
        public async Task CsvAndJsonLinesRoundTripAMergedMonth()
        {
            ArchiveMergedSeptember();
            var itemName = "Heroic \"Hyperion\", ✪\nline";
            var withQuotes = ArchivedAuctionBlock.FromAuctions(Tag, 202010, 0, ArchivedAuctionBlock.SourceScylla, new[]
            {
                (new ArchiveAuctionBuilder(50, new DateTime(2020, 10, 2, 3, 4, 5, 6, DateTimeKind.Utc)).Full().With(a => a.ItemName = itemName).Build(), Extras("heroic", "Carol")),
            });
            Archive(202010, withQuotes);
            var rows = new List<ArchivedAuction>();
            await foreach (var a in reader.ReadMonthAsync(Tag, Sep))
                rows.Add(a);
            await foreach (var a in reader.ReadMonthAsync(Tag, 202010))
                rows.Add(a);

            var csv = new StringWriter();
            Assert.That(ArchiveExport.WriteCsv(rows, csv, ExportColumns.ItemBytes), Is.EqualTo(8));
            var records = ParseCsv(csv.ToString());
            Assert.That(records[0], Is.EqualTo(ArchiveExport.Header(ExportColumns.ItemBytes)));
            Assert.That(records.Count, Is.EqualTo(9));
            for (var i = 0; i < rows.Count; i++)
                Assert.That(records[i + 1], Is.EqualTo(ArchiveExport.Fields(rows[i], ExportColumns.ItemBytes).Select(f => f ?? "")), $"row {i}");

            var full = records[8];
            string Col(string name) => full[ArchiveExport.Header(ExportColumns.ItemBytes).ToList().IndexOf(name)];
            Assert.That(Col("itemName"), Is.EqualTo(itemName));
            Assert.That(Col("end"), Is.EqualTo("2020-10-02T03:04:05.006Z"));
            Assert.That(Col("tier"), Is.EqualTo("MYTHIC"));
            Assert.That(Col("sold"), Is.EqualTo("true"));
            Assert.That(Col("highestBidder"), Is.EqualTo(ArchiveAuctionBuilder.Hex(4000)));
            Assert.That(Col("enchantments"), Is.EqualTo("sharpness:7;transylvanian:5;future_ench:2"));
            Assert.That(Col("nbt"), Is.EqualTo("modifier=heroic;uid=abc"));
            Assert.That(Col("coop"), Is.EqualTo("2"));
            Assert.That(Col("bids"), Is.EqualTo("3"));
            Assert.That(Col("source"), Is.EqualTo("scylla"));
            Assert.That(Convert.FromBase64String(Col("itemBytes")), Is.EqualTo(new byte[] { 10, 0, 1, 2, 3, 4, 5, 250, 0 }));

            var jsonl = new StringWriter();
            Assert.That(ArchiveExport.WriteJsonLines(rows, jsonl), Is.EqualTo(8));
            var lines = jsonl.ToString().Split('\n');
            Assert.That(lines.Length, Is.EqualTo(9));
            Assert.That(lines[8], Is.Empty, "every line is terminated");
            var last = ParseJson(lines[7]);
            Assert.That(last.Properties().Select(p => p.Name), Is.EqualTo(ArchiveExport.StandardColumns));
            Assert.That((string)last["itemName"], Is.EqualTo(itemName));
            Assert.That((long)last["uid"], Is.EqualTo(50));
            Assert.That((bool)last["sold"], Is.True);
            Assert.That((string)last["end"], Is.EqualTo("2020-10-02T03:04:05.006Z"));
            Assert.That((int)last["enchantments"]["future_ench"], Is.EqualTo(2));
            Assert.That((int)last["enchantments"]["sharpness"], Is.EqualTo(7));
            Assert.That((string)last["nbt"]["modifier"], Is.EqualTo("heroic"));
            var first = ParseJson(lines[0]);
            Assert.That((string)first["source"], Is.EqualTo("mariadb"));
            Assert.That(((JObject)first["nbt"]).Count, Is.EqualTo(0), "MariaDB row without parsable item bytes");
        }

        [Test]
        public async Task DtoCarriesExtrasAndOnlyRequestedBytesAndBids()
        {
            var block = ArchivedAuctionBlock.FromAuctions(Tag, Sep, 0, ArchivedAuctionBlock.SourceScylla, new[]
            {
                (new ArchiveAuctionBuilder(50, SepStart.AddDays(2)).Full().Build(), Extras("heroic", "Carol")),
            });
            Archive(Sep, block);
            var found = await reader.FindByUidAsync(50);

            var trimmed = ArchivedAuctionDto.From(found);
            Assert.That(trimmed.HighestBidderName, Is.EqualTo("Carol"));
            Assert.That(trimmed.Enchantments, Is.EqualTo(new Dictionary<string, int> { ["sharpness"] = 7, ["transylvanian"] = 5, ["future_ench"] = 2 }));
            Assert.That(trimmed.NbtLookup["modifier"], Is.EqualTo("heroic"));
            Assert.That(trimmed.BidsCount, Is.EqualTo(3));
            Assert.That(trimmed.Bids, Is.Null);
            Assert.That(trimmed.ItemBytes, Is.Null);
            Assert.That(trimmed.Coop, Is.EqualTo(new[] { ArchiveAuctionBuilder.Hex(5000), "SOME-Odd-Id" }));
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(trimmed);
            Assert.That(json, Does.Not.Contain("itemBytes").And.Not.Contain("\"bids\""));
            Assert.That(System.Text.Json.JsonSerializer.Serialize(trimmed), Does.Not.Contain("itemBytes").And.Contain("\"highestBidderName\":\"Carol\""));

            var fullDto = ArchivedAuctionDto.From(found, includeItemBytes: true, includeBids: true);
            Assert.That(fullDto.Bids.Select(b => b.Amount), Is.EqualTo(new long[] { 700, 900, 800 }));
            Assert.That(Convert.FromBase64String(fullDto.ItemBytes), Is.EqualTo(new byte[] { 10, 0, 1, 2, 3, 4, 5, 250, 0 }));
        }

        // ------------------------------------------------------------ block helpers

        [Test]
        public void PerRowHelpersEqualTheBulkMaterialisation()
        {
            var block = ArchivedAuctionBlock.FromAuctions(Tag, Sep, 0, ArchivedAuctionBlock.SourceScylla, new[]
            {
                (new ArchiveAuctionBuilder(50, SepStart.AddDays(2)).Full().Build(), Extras("heroic", "Carol")),
                (Row(3, 1), Extras("fabled", null)),
            });
            var bulk = block.ToAuctions().ToList();
            var extras = block.ToExtras();
            for (var i = 0; i < block.RowCount; i++)
            {
                Assert.That(Describe(block.ToAuction(i)), Is.EqualTo(Describe(bulk[i])));
                Assert.That(block.ExtrasAt(i).NbtLookupStrings, Is.EqualTo(extras[i].NbtLookupStrings));
                Assert.That(block.EndTicksAt(i), Is.EqualTo(bulk[i].End.Ticks));
            }
            var mariaDb = ArchivedAuctionBlock.FromAuctions(Tag, Sep, 0, ArchivedAuctionBlock.SourceMariaDb, new[] { Row(1, 1) });
            Assert.That(mariaDb.HasExtras, Is.False);
            Assert.That(mariaDb.ExtrasAt(0), Is.Null);
            Assert.Throws<ArgumentOutOfRangeException>(() => mariaDb.ToAuction(1));
        }

        private static JObject ParseJson(string line)
        {
            using var json = new Newtonsoft.Json.JsonTextReader(new StringReader(line)) { DateParseHandling = Newtonsoft.Json.DateParseHandling.None };
            return JObject.Load(json);
        }

        private static string Describe(SaveAuction a) => string.Join("|", a.Uuid, a.UId, a.Tag, a.End.Ticks, a.Start.Ticks, a.Bin, a.Claimed, a.AuctioneerId,
            a.ProfileId, a.HighestBidAmount, a.StartingBid, a.Count, a.Tier, a.Category, a.Reforge, a.AnvilUses, a.ItemName, a.ItemCreatedAt.Ticks,
            Convert.ToBase64String(a.NbtData?.data ?? Array.Empty<byte>()), string.Join(",", a.Bids.Select(b => b.Bidder + b.Amount + b.Timestamp.Ticks)),
            string.Join(",", a.CoopMembers.Select(c => c.value)), string.Join(",", a.ClaimedBids.Select(c => c.value)),
            string.Join(",", a.NBTLookup.Select(l => l.KeyId + "=" + l.Value)), string.Join(",", a.Enchantments.Select(e => e.Type + ":" + e.Level)));

        /// <summary>Minimal RFC 4180 parser (quoted fields, doubled quotes, CRLF records).</summary>
        private static List<List<string>> ParseCsv(string text)
        {
            var records = new List<List<string>>();
            var record = new List<string>();
            var field = new System.Text.StringBuilder();
            var quoted = false;
            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                if (quoted)
                {
                    if (c == '"' && i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                    else if (c == '"') quoted = false;
                    else field.Append(c);
                    continue;
                }
                if (c == '"') quoted = true;
                else if (c == ',') { record.Add(field.ToString()); field.Clear(); }
                else if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                {
                    record.Add(field.ToString()); field.Clear();
                    records.Add(record); record = new List<string>();
                    i++;
                }
                else field.Append(c);
            }
            return records;
        }
    }
}
