using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Coflnet.Sky.Core.Archive
{
    /// <summary>
    /// Read side of the auction archive v2: month listing, filtered month reads, lookups by auction uid/uuid and bidder, and an
    /// uncached hash verification. Needs an <see cref="IAuctionBlobStore"/> and read-only MariaDB access (<see cref="IArchiveLocationLookup"/>);
    /// no ASP.NET dependency. Thread-safe as long as the store and lookup are.
    /// </summary>
    public class ArchivedAuctionReader
    {
        private readonly IAuctionBlobStore store;
        private readonly IArchiveLocationLookup lookup;
        private readonly ILogger logger;

        /// <summary>
        /// Months with at most this many rows (tag index <see cref="ArchivedMonthEntry.Rows"/>) are merged with every part loaded at once
        /// (each part read once). Larger months first read each part to learn its End range and then merge lazily, so only the parts
        /// whose ranges overlap are held at the same time (usually 1-2), at the cost of reading each part twice (the second read is
        /// normally served by the store's block cache).
        /// </summary>
        public int SinglePassMergeMaxRows { get; set; } = 250_000;

        /// <summary>Production constructor: lookups through <see cref="EfArchiveLocationLookup"/> over <paramref name="dbFactory"/>.</summary>
        public ArchivedAuctionReader(IAuctionBlobStore store, Func<ArchiveContext> dbFactory, ILogger logger)
            : this(store, new EfArchiveLocationLookup(dbFactory), logger)
        {
        }

        /// <summary>Constructor with an explicit lookup (tests, other databases).</summary>
        public ArchivedAuctionReader(IAuctionBlobStore store, IArchiveLocationLookup lookup, ILogger logger)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.lookup = lookup ?? throw new ArgumentNullException(nameof(lookup));
            this.logger = logger ?? NullLogger.Instance;
        }

        /// <summary>True if the blob store is configured.</summary>
        public bool IsEnabled => store.IsEnabled;

        /// <summary>The tag's month index, null when the tag has no archive.</summary>
        public Task<ArchivedTagIndex> GetTagIndexAsync(string tag, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(tag))
                return Task.FromResult<ArchivedTagIndex>(null);
            return store.ReadIndexAsync(tag, cancellationToken);
        }

        /// <summary>The archived (verified) months of a tag, ascending; empty when the tag has no archive.</summary>
        public async Task<IReadOnlyList<ArchivedMonthEntry>> GetMonthsAsync(string tag, CancellationToken cancellationToken = default)
        {
            var index = await GetTagIndexAsync(tag, cancellationToken).ConfigureAwait(false);
            return (IReadOnlyList<ArchivedMonthEntry>)index?.Months ?? Array.Empty<ArchivedMonthEntry>();
        }

        /// <summary>
        /// The rows of a tag-month that match <paramref name="query"/>, sorted by (End, uid) ascending or, with
        /// <see cref="ArchivedAuctionQuery.Descending"/>, descending, at most <see cref="ArchivedAuctionQuery.Limit"/> rows.
        /// The parts come from the tag index entry (nothing when the month is not in the index); blocks are read through the store's
        /// cache. Phase-2 parts (Scylla-only rows) overlap the End range of the phase-1 parts, so parts are merged, not concatenated.
        /// A part the index lists but the store does not have throws <see cref="InvalidDataException"/>.
        /// </summary>
        public async IAsyncEnumerable<ArchivedAuction> ReadMonthAsync(string tag, int month, ArchivedAuctionQuery query = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            query ??= new ArchivedAuctionQuery();
            if (query.Limit is <= 0)
                yield break;
            var (monthStart, monthEnd) = ArchiveMonths.MonthRange(month);
            if (query.ExcludesRange(monthStart, monthEnd))
                yield break;
            var index = await GetTagIndexAsync(tag, cancellationToken).ConfigureAwait(false);
            var entry = index?.Find(month);
            if (entry == null || entry.Parts <= 0)
                yield break;

            var descending = query.Descending;
            var sources = new List<PartSource>(entry.Parts);
            if (entry.Parts == 1 || entry.Rows <= SinglePassMergeMaxRows)
            {
                for (var part = 0; part < entry.Parts; part++)
                {
                    var block = await ReadPartAsync(tag, month, part, false, cancellationToken).ConfigureAwait(false);
                    if (block.RowCount > 0)
                        sources.Add(PartSource.Loaded(block, descending));
                }
            }
            else
            {
                for (var part = 0; part < entry.Parts; part++)
                {
                    var block = await ReadPartAsync(tag, month, part, false, cancellationToken).ConfigureAwait(false);
                    if (block.RowCount > 0)
                        sources.Add(PartSource.Deferred(block, descending));
                }
            }

            var endFromTicks = query.EndFrom == null ? (long?)null : ArchivedAuctionQuery.AsUtc(query.EndFrom.Value).Ticks;
            var endToTicks = query.EndTo == null ? (long?)null : ArchivedAuctionQuery.AsUtc(query.EndTo.Value).Ticks;
            var returned = 0;
            await foreach (var (block, row) in MergeAsync(sources, tag, month, descending, cancellationToken).ConfigureAwait(false))
            {
                var ticks = block.EndTicksAt(row);
                // the merge is sorted, so once past the End window nothing later can match
                if (!descending && endToTicks != null && ticks >= endToTicks)
                    yield break;
                if (descending && endFromTicks != null && ticks < endFromTicks)
                    yield break;
                if (endFromTicks != null && ticks < endFromTicks || endToTicks != null && ticks >= endToTicks)
                    continue;
                var auction = Materialise(block, row);
                if (!query.Matches(auction))
                    continue;
                yield return auction;
                if (query.Limit != null && ++returned >= query.Limit.Value)
                    yield break;
            }
        }

        /// <summary>
        /// The archived auction with this <see cref="SaveAuction.UId"/>, null when it is not archived: one <c>auction_locations</c> primary key
        /// read, the tag name (cached), and one (cached) block read; no bucket listing.
        /// </summary>
        public async Task<ArchivedAuction> FindByUidAsync(long uid, CancellationToken cancellationToken = default)
        {
            var location = await lookup.GetLocationAsync(uid, cancellationToken).ConfigureAwait(false);
            if (location == null)
                return null;
            var tag = await lookup.GetTagNameAsync(location.TagId, cancellationToken).ConfigureAwait(false);
            if (tag == null)
            {
                logger.LogWarning("Archive: auction {Uid} is located in tag id {TagId}, which has no auction_tags row", uid, location.TagId);
                return null;
            }
            var month = ArchiveMonths.FromIndex(location.Month);
            var block = await store.ReadBlockAsync(tag, month, location.Part, false, cancellationToken).ConfigureAwait(false);
            if (block == null)
            {
                logger.LogWarning("Archive: auction {Uid} is located in {Tag} {Month} part {Part}, which does not exist", uid, tag, month, location.Part);
                return null;
            }
            var row = Array.IndexOf(block.AuctionUid ?? Array.Empty<long>(), uid);
            if (row < 0)
            {
                logger.LogWarning("Archive: auction {Uid} is located in {Tag} {Month} part {Part}, which does not contain it", uid, tag, month, location.Part);
                return null;
            }
            return Materialise(block, row);
        }

        /// <summary>
        /// The archived auction with this uuid (hex, dashes ignored), null when it is not archived. The uuid is mapped to its uid with
        /// <see cref="AuctionService.GetId"/> (as SkyAuctions/SkyIndexer do) and the row's uuid is compared, so a uid collision returns null.
        /// </summary>
        public async Task<ArchivedAuction> FindByUuidAsync(string uuid, CancellationToken cancellationToken = default)
        {
            var normalized = ArchivedAuctionQuery.NormalizeUuid(uuid);
            if (normalized == null || normalized.Length < 17 || !IsHex(normalized))
                return null;
            var uid = AuctionService.Instance.GetId(normalized);
            var found = await FindByUidAsync(uid, cancellationToken).ConfigureAwait(false);
            if (found == null)
                return null;
            if (!string.Equals(ArchivedAuctionQuery.NormalizeUuid(found.Row.Uuid), normalized, StringComparison.Ordinal))
            {
                logger.LogInformation("Archive: uid {Uid} of {Uuid} belongs to auction {Other}", uid, normalized, found.Row.Uuid);
                return null;
            }
            return found;
        }

        /// <summary>
        /// Archived auctions a player (<c>Players.Id</c>) bid on, newest End first, at most <paramref name="limit"/>, from
        /// <c>auction_bidders</c> (+ <c>auction_locations</c>), optionally limited to the months <paramref name="fromMonth"/>..<paramref name="toMonth"/>
        /// (<c>yyyyMM</c>, inclusive). Every block is read once, however many of the auctions it holds.
        /// </summary>
        public async Task<IReadOnlyList<ArchivedAuction>> FindByBidderAsync(uint bidderId, int? fromMonth, int? toMonth, int limit, CancellationToken cancellationToken = default)
        {
            if (limit <= 0)
                return Array.Empty<ArchivedAuction>();
            var from = fromMonth == null ? 0u : ToUnixSeconds(ArchiveMonths.MonthRange(fromMonth.Value).start);
            var to = toMonth == null ? uint.MaxValue : ToUnixSeconds(ArchiveMonths.MonthRange(toMonth.Value).end);
            var bids = await lookup.GetBidderRowsAsync(bidderId, from, to, limit, cancellationToken).ConfigureAwait(false);
            if (bids.Count == 0)
                return Array.Empty<ArchivedAuction>();
            var uids = bids.Select(b => b.Uid).Distinct().ToList();
            var locations = await lookup.GetLocationsAsync(uids, cancellationToken).ConfigureAwait(false);
            var found = new Dictionary<long, ArchivedAuction>();
            foreach (var group in uids.Where(locations.ContainsKey).GroupBy(u => (locations[u].TagId, locations[u].Month, locations[u].Part)))
            {
                var (tagId, monthIndex, part) = group.Key;
                var tag = await lookup.GetTagNameAsync(tagId, cancellationToken).ConfigureAwait(false);
                if (tag == null)
                {
                    logger.LogWarning("Archive: {Count} bids of {BidderId} are located in tag id {TagId}, which has no auction_tags row", group.Count(), bidderId, tagId);
                    continue;
                }
                var month = ArchiveMonths.FromIndex(monthIndex);
                var block = await store.ReadBlockAsync(tag, month, part, false, cancellationToken).ConfigureAwait(false);
                if (block == null)
                {
                    logger.LogWarning("Archive: {Count} bids of {BidderId} are located in {Tag} {Month} part {Part}, which does not exist", group.Count(), bidderId, tag, month, part);
                    continue;
                }
                var rowOf = new Dictionary<long, int>(block.RowCount);
                for (var i = 0; i < block.RowCount; i++)
                    rowOf.TryAdd(block.AuctionUid[i], i);
                foreach (var uid in group)
                {
                    if (rowOf.TryGetValue(uid, out var row))
                        found[uid] = Materialise(block, row);
                    else
                        logger.LogWarning("Archive: auction {Uid} is located in {Tag} {Month} part {Part}, which does not contain it", uid, tag, month, part);
                }
            }
            var missing = uids.Count - found.Count;
            if (missing > 0)
                logger.LogDebug("Archive: {Missing} of {Count} bid auctions of {BidderId} have no readable location", missing, uids.Count, bidderId);
            return found.Values.OrderByDescending(a => a.Row.End).ThenByDescending(a => a.Row.UId).ToList();
        }

        /// <summary>
        /// Read every part of a tag-month uncached, recompute <see cref="ArchivedAuctionBlock.Sha256"/> and compare it with the tag index entry
        /// and with <c>archived_months.sha256</c> (over the parts the row covers). Read only. When the month is not in the index the
        /// parts of the <c>archived_months</c> row are read.
        /// </summary>
        public async Task<ArchivedMonthVerification> VerifyMonthAsync(string tag, int month, CancellationToken cancellationToken = default)
        {
            var watch = Stopwatch.StartNew();
            var result = new ArchivedMonthVerification { Tag = tag, Month = month };
            var index = await GetTagIndexAsync(tag, cancellationToken).ConfigureAwait(false);
            var entry = index?.Find(month);
            ArchivedMonth dbRow = null;
            try
            {
                dbRow = await lookup.GetArchivedMonthAsync(tag, month, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Archive verify: archived_months of {Tag} {Month} could not be read", tag, month);
                result.DbError = ex.GetType().Name + ": " + ex.Message;
            }
            result.InIndex = entry != null;
            result.IndexParts = entry?.Parts;
            result.IndexRows = entry?.Rows;
            result.IndexSoldRows = entry?.SoldRows;
            result.Source = entry?.Source ?? dbRow?.Source;
            result.DbParts = dbRow?.Parts;
            result.DbRows = dbRow?.Rows;
            result.DbSha256 = dbRow?.Sha256;

            void Difference(string text) => result.FirstDifference ??= text;
            if (entry == null)
                Difference(index == null ? "tag index missing" : "month not in tag index");

            var parts = entry?.Parts ?? dbRow?.Parts ?? 0;
            var hashes = new List<string>(parts);
            var indexMatches = entry != null && (entry.PartSha256?.Count ?? 0) == parts;
            if (entry != null && !indexMatches)
                Difference($"tag index lists {entry.PartSha256?.Count ?? 0} part hashes for {parts} parts");
            for (var part = 0; part < parts; part++)
            {
                var block = await store.ReadBlockAsync(tag, month, part, true, cancellationToken).ConfigureAwait(false);
                if (block == null)
                {
                    hashes.Add(null);
                    indexMatches = false;
                    Difference($"part {part} missing");
                    continue;
                }
                if (block.Tag != tag || block.Month != month || block.Part != part)
                    Difference($"part {part} is labelled {block.Tag} {block.Month} part {block.Part}");
                var sha = block.Sha256();
                hashes.Add(sha);
                result.Rows += block.RowCount;
                result.SoldRows += block.IsSold?.Count(s => s) ?? 0;
                var expected = entry?.PartSha256 != null && part < entry.PartSha256.Count ? entry.PartSha256[part] : null;
                if (entry != null && !string.Equals(expected, sha, StringComparison.OrdinalIgnoreCase))
                {
                    indexMatches = false;
                    Difference($"part {part}: tag index sha256 {expected ?? "(none)"} != read {sha}");
                }
            }
            result.Parts = parts;
            result.PartSha256 = hashes;
            result.HashesMatchIndex = indexMatches;
            if (entry != null && hashes.All(h => h != null))
            {
                if (result.Rows != entry.Rows)
                    Difference($"rows: tag index {entry.Rows} != read {result.Rows}");
                if (result.SoldRows != entry.SoldRows)
                    Difference($"sold rows: tag index {entry.SoldRows} != read {result.SoldRows}");
            }

            if (dbRow != null)
            {
                // until SkyIndexer imports phase-2 parts, the row (and its hash) only covers the phase-1 parts
                if (dbRow.Parts > parts || hashes.Take(dbRow.Parts).Any(h => h == null))
                {
                    result.HashesMatchDb = false;
                    Difference($"archived_months has {dbRow.Parts} parts, {hashes.Count(h => h != null)} readable");
                }
                else
                {
                    var combined = ArchivedAuctionBlock.CombinedSha256(hashes.Take(dbRow.Parts).ToList());
                    result.HashesMatchDb = string.Equals(combined, dbRow.Sha256, StringComparison.OrdinalIgnoreCase);
                    if (!result.HashesMatchDb.Value)
                        Difference($"archived_months sha256 {dbRow.Sha256} != read {combined} over {dbRow.Parts} parts");
                }
            }
            else if (result.DbError == null)
            {
                Difference("no archived_months row");
            }
            result.CombinedSha256 = hashes.All(h => h != null) ? ArchivedAuctionBlock.CombinedSha256(hashes) : null;
            result.MsElapsed = watch.ElapsedMilliseconds;
            return result;
        }

        private async Task<ArchivedAuctionBlock> ReadPartAsync(string tag, int month, int part, bool uncached, CancellationToken cancellationToken)
        {
            var block = await store.ReadBlockAsync(tag, month, part, uncached, cancellationToken).ConfigureAwait(false);
            if (block == null)
                throw new InvalidDataException($"Part {part} of {tag} {ArchiveMonths.Format(month)} is listed in the tag index but does not exist");
            if (block.Tag != tag || block.Month != month || block.Part != part)
                throw new InvalidDataException($"Part {part} of {tag} {ArchiveMonths.Format(month)} is labelled {block.Tag} {block.Month} part {block.Part}");
            return block;
        }

        private static ArchivedAuction Materialise(ArchivedAuctionBlock block, int row) => new()
        {
            Row = block.ToAuction(row),
            Extras = block.ExtrasAt(row),
            Month = block.Month,
            Part = block.Part,
            Source = block.Source,
        };

        /// <summary>
        /// K-way merge of the parts by (End, uid). A deferred part is only loaded once its first key (in merge direction) is not after
        /// the smallest head of the loaded parts, and released when exhausted.
        /// </summary>
        private async IAsyncEnumerable<(ArchivedAuctionBlock Block, int Row)> MergeAsync(List<PartSource> sources, string tag, int month, bool descending,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var comparer = descending ? KeyComparer.Descending : KeyComparer.Ascending;
            var pending = new Queue<PartSource>(sources.OrderBy(s => s.StartKey, comparer).ThenBy(s => s.Part));
            var heap = new PriorityQueue<Cursor, (long, long)>(comparer);
            var counter = 0;
            while (true)
            {
                while (pending.Count > 0 && (heap.Count == 0 || comparer.Compare(pending.Peek().StartKey, heap.Peek().Key) <= 0))
                {
                    var source = pending.Dequeue();
                    var block = source.Block ?? await ReadPartAsync(tag, month, source.Part, false, cancellationToken).ConfigureAwait(false);
                    var cursor = new Cursor(block, descending);
                    if (!cursor.Done)
                        heap.Enqueue(cursor, cursor.Key);
                }
                if (heap.Count == 0)
                    yield break;
                if ((++counter & 1023) == 0)
                    cancellationToken.ThrowIfCancellationRequested();
                var next = heap.Dequeue();
                yield return (next.Block, next.Position);
                next.Advance();
                if (!next.Done)
                    heap.Enqueue(next, next.Key);
            }
        }

        private static uint ToUnixSeconds(DateTime utc)
        {
            var seconds = new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToUnixTimeSeconds();
            return seconds <= 0 ? 0 : seconds >= uint.MaxValue ? uint.MaxValue : (uint)seconds;
        }

        private static bool IsHex(string value)
        {
            foreach (var c in value)
                if (!(c >= '0' && c <= '9' || c >= 'a' && c <= 'f'))
                    return false;
            return true;
        }

        private sealed class PartSource
        {
            public int Part { get; private init; }
            /// <summary>Loaded block, null when it is re-read on demand.</summary>
            public ArchivedAuctionBlock Block { get; private init; }
            public (long Ticks, long Uid) StartKey { get; private init; }

            public static PartSource Loaded(ArchivedAuctionBlock block, bool descending) => new()
            {
                Part = block.Part,
                Block = block,
                StartKey = FirstKey(block, descending),
            };

            /// <summary>Remember only the range; the block is read again when the merge reaches it.</summary>
            public static PartSource Deferred(ArchivedAuctionBlock block, bool descending) => new()
            {
                Part = block.Part,
                StartKey = FirstKey(block, descending),
            };

            private static (long, long) FirstKey(ArchivedAuctionBlock block, bool descending)
            {
                var i = descending ? block.RowCount - 1 : 0;
                return (block.EndTicksAt(i), block.AuctionUid[i]);
            }
        }

        private sealed class Cursor
        {
            private readonly int step;
            public ArchivedAuctionBlock Block { get; }
            public int Position { get; private set; }
            public bool Done => Position < 0 || Position >= Block.RowCount;
            public (long, long) Key => (Block.EndTicksAt(Position), Block.AuctionUid[Position]);

            public Cursor(ArchivedAuctionBlock block, bool descending)
            {
                Block = block;
                step = descending ? -1 : 1;
                Position = descending ? block.RowCount - 1 : 0;
            }

            public void Advance() => Position += step;
        }

        private sealed class KeyComparer : IComparer<(long, long)>
        {
            public static readonly KeyComparer Ascending = new(false);
            public static readonly KeyComparer Descending = new(true);
            private readonly bool descending;

            private KeyComparer(bool descending) => this.descending = descending;

            public int Compare((long, long) x, (long, long) y)
            {
                var c = x.Item1.CompareTo(y.Item1);
                if (c == 0)
                    c = x.Item2.CompareTo(y.Item2);
                return descending ? -c : c;
            }
        }
    }

    /// <summary>Result of <see cref="ArchivedAuctionReader.VerifyMonthAsync"/>.</summary>
    public sealed class ArchivedMonthVerification
    {
        /// <summary>Item tag.</summary>
        public string Tag { get; set; }
        /// <summary>Month, <c>yyyyMM</c>.</summary>
        public int Month { get; set; }
        /// <summary>Parts read (the tag index's count, else the <c>archived_months</c> row's).</summary>
        public int Parts { get; set; }
        /// <summary>Rows over every readable part.</summary>
        public int Rows { get; set; }
        /// <summary>Sold rows over every readable part.</summary>
        public int SoldRows { get; set; }
        /// <summary>True if the month is in the tag index, every part exists and every recomputed part hash equals the index's.</summary>
        public bool HashesMatchIndex { get; set; }
        /// <summary>
        /// True if <c>archived_months.sha256</c> equals <see cref="ArchivedAuctionBlock.CombinedSha256"/> of the recomputed hashes of the
        /// parts the row counts (phase-2 parts only count once SkyIndexer imported them); null without a row or when MariaDB failed.
        /// </summary>
        public bool? HashesMatchDb { get; set; }
        /// <summary>First problem found (missing index entry or part, hash, row count, label), null when everything matches.</summary>
        public string FirstDifference { get; set; }
        /// <summary>Wall time of the verification.</summary>
        public long MsElapsed { get; set; }
        /// <summary>True if the tag index has the month.</summary>
        public bool InIndex { get; set; }
        /// <summary>Source from the tag index (else the database row).</summary>
        public string Source { get; set; }
        /// <summary>Recomputed hash of each part (null for a missing part).</summary>
        public List<string> PartSha256 { get; set; } = new();
        /// <summary>Combined hash over every part, null when a part is missing.</summary>
        public string CombinedSha256 { get; set; }
        /// <summary>Tag index entry: parts.</summary>
        public int? IndexParts { get; set; }
        /// <summary>Tag index entry: rows.</summary>
        public int? IndexRows { get; set; }
        /// <summary>Tag index entry: sold rows.</summary>
        public int? IndexSoldRows { get; set; }
        /// <summary><c>archived_months.parts</c>.</summary>
        public int? DbParts { get; set; }
        /// <summary><c>archived_months.rows</c>.</summary>
        public long? DbRows { get; set; }
        /// <summary><c>archived_months.sha256</c>.</summary>
        public string DbSha256 { get; set; }
        /// <summary>Error reading <c>archived_months</c>, null when it worked.</summary>
        public string DbError { get; set; }
    }

    /// <summary>DI registration of <see cref="ArchivedAuctionReader"/>.</summary>
    public static class ArchivedAuctionReaderServiceCollectionExtensions
    {
        /// <summary>
        /// Register <see cref="IArchiveLocationLookup"/> (<see cref="EfArchiveLocationLookup"/> over <c>new ArchiveContext()</c>, i.e.
        /// <see cref="ArchiveContext.SetConfiguration"/>'s <c>DBConnection</c>, unless one is registered already) and a singleton
        /// <see cref="ArchivedAuctionReader"/>. An <see cref="IAuctionBlobStore"/> must be registered by the host.
        /// </summary>
        public static IServiceCollection AddArchivedAuctionReader(this IServiceCollection services)
        {
            services.TryAddSingleton<IArchiveLocationLookup>(_ => new EfArchiveLocationLookup(() => new ArchiveContext()));
            services.TryAddSingleton(p => new ArchivedAuctionReader(
                p.GetRequiredService<IAuctionBlobStore>(),
                p.GetRequiredService<IArchiveLocationLookup>(),
                p.GetService<ILoggerFactory>()?.CreateLogger<ArchivedAuctionReader>()));
            return services;
        }
    }
}
