using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Coflnet.Sky.Core.Archive
{
    /// <summary>
    /// Read-only MariaDB lookups of the auction archive (<see cref="ArchiveContext"/> tables) that <see cref="ArchivedAuctionReader"/> needs.
    /// A seam so the reader can be tested without MariaDB; <see cref="EfArchiveLocationLookup"/> is the production implementation.
    /// </summary>
    public interface IArchiveLocationLookup
    {
        /// <summary><c>auction_locations</c> row of an auction uid, null when it is not archived.</summary>
        Task<AuctionLocation> GetLocationAsync(long uid, CancellationToken cancellationToken = default);

        /// <summary><c>auction_locations</c> rows of the given uids (missing uids are absent from the result).</summary>
        Task<IReadOnlyDictionary<long, AuctionLocation>> GetLocationsAsync(IReadOnlyCollection<long> uids, CancellationToken cancellationToken = default);

        /// <summary>Tag of an <c>auction_tags</c> id, null when unknown.</summary>
        Task<string> GetTagNameAsync(ushort tagId, CancellationToken cancellationToken = default);

        /// <summary>
        /// <c>auction_bidders</c> rows of a bidder (<c>Players.Id</c>) whose auction ended in <c>[fromUnixSeconds, toUnixSeconds)</c>,
        /// newest End first, at most <paramref name="limit"/>, only rows after <paramref name="before"/> when given.
        /// </summary>
        Task<IReadOnlyList<AuctionBidder>> GetBidderRowsAsync(uint bidderId, uint fromUnixSeconds, uint toUnixSeconds, int limit, AuctionIndexCursor before = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// <c>auction_sellers</c> rows of a seller (<c>Players.Id</c>) whose auction ended in <c>[fromUnixSeconds, toUnixSeconds)</c>,
        /// newest End first, at most <paramref name="limit"/>, only rows after <paramref name="before"/> when given.
        /// </summary>
        Task<IReadOnlyList<AuctionSeller>> GetSellerRowsAsync(uint sellerId, uint fromUnixSeconds, uint toUnixSeconds, int limit, AuctionIndexCursor before = null, CancellationToken cancellationToken = default);

        /// <summary><c>archived_months</c> row of a tag-month, null when the tag or the row does not exist.</summary>
        Task<ArchivedMonth> GetArchivedMonthAsync(string tag, int month, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// <see cref="IArchiveLocationLookup"/> over <see cref="ArchiveContext"/>. Only reads (AsNoTracking), so a read-only database user
    /// is enough. Tag ids and names are cached for the lifetime of the instance (rows of <c>auction_tags</c> never change).
    /// </summary>
    public class EfArchiveLocationLookup : IArchiveLocationLookup
    {
        /// <summary>Uids per <c>IN (...)</c> query of <see cref="GetLocationsAsync"/>.</summary>
        public const int LocationChunkSize = 500;

        private readonly Func<ArchiveContext> dbFactory;
        private readonly ConcurrentDictionary<ushort, string> tagNames = new();
        private readonly ConcurrentDictionary<string, ushort> tagIds = new(StringComparer.Ordinal);

        /// <summary>Create with a factory for (short lived) contexts, e.g. <c>() =&gt; new ArchiveContext()</c>.</summary>
        public EfArchiveLocationLookup(Func<ArchiveContext> dbFactory)
        {
            this.dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
        }

        /// <inheritdoc/>
        public async Task<AuctionLocation> GetLocationAsync(long uid, CancellationToken cancellationToken = default)
        {
            using var db = dbFactory();
            return await db.AuctionLocations.AsNoTracking().Where(l => l.Uid == uid).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyDictionary<long, AuctionLocation>> GetLocationsAsync(IReadOnlyCollection<long> uids, CancellationToken cancellationToken = default)
        {
            var result = new Dictionary<long, AuctionLocation>();
            if (uids == null || uids.Count == 0)
                return result;
            using var db = dbFactory();
            foreach (var chunk in uids.Distinct().Chunk(LocationChunkSize))
            {
                var list = chunk.ToList();
                var rows = await db.AuctionLocations.AsNoTracking().Where(l => list.Contains(l.Uid)).ToListAsync(cancellationToken).ConfigureAwait(false);
                foreach (var row in rows)
                    result[row.Uid] = row;
            }
            return result;
        }

        /// <inheritdoc/>
        public async Task<string> GetTagNameAsync(ushort tagId, CancellationToken cancellationToken = default)
        {
            if (tagNames.TryGetValue(tagId, out var cached))
                return cached;
            using var db = dbFactory();
            var name = await db.AuctionTags.AsNoTracking().Where(t => t.Id == tagId).Select(t => t.Tag).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
            if (name != null)
            {
                tagNames[tagId] = name;
                tagIds[name] = tagId;
            }
            return name;
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<AuctionBidder>> GetBidderRowsAsync(uint bidderId, uint fromUnixSeconds, uint toUnixSeconds, int limit, AuctionIndexCursor before = null, CancellationToken cancellationToken = default)
        {
            using var db = dbFactory();
            var query = db.AuctionBidders.AsNoTracking().Where(b => b.BidderId == bidderId && b.End >= fromUnixSeconds && b.End < toUnixSeconds);
            if (before != null)
            {
                var (beforeEnd, beforeUid) = (before.End, before.Uid);
                query = query.Where(b => b.End < beforeEnd || (b.End == beforeEnd && b.Uid < beforeUid));
            }
            return await query.OrderByDescending(b => b.End).ThenByDescending(b => b.Uid)
                .Take(Math.Max(0, limit))
                .ToListAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<AuctionSeller>> GetSellerRowsAsync(uint sellerId, uint fromUnixSeconds, uint toUnixSeconds, int limit, AuctionIndexCursor before = null, CancellationToken cancellationToken = default)
        {
            using var db = dbFactory();
            var query = db.AuctionSellers.AsNoTracking().Where(b => b.SellerId == sellerId && b.End >= fromUnixSeconds && b.End < toUnixSeconds);
            if (before != null)
            {
                var (beforeEnd, beforeUid) = (before.End, before.Uid);
                query = query.Where(b => b.End < beforeEnd || (b.End == beforeEnd && b.Uid < beforeUid));
            }
            return await query.OrderByDescending(b => b.End).ThenByDescending(b => b.Uid)
                .Take(Math.Max(0, limit))
                .ToListAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task<ArchivedMonth> GetArchivedMonthAsync(string tag, int month, CancellationToken cancellationToken = default)
        {
            var tagId = await GetTagIdAsync(tag, cancellationToken).ConfigureAwait(false);
            if (tagId == null)
                return null;
            var index = ArchiveMonths.ToIndex(month);
            using var db = dbFactory();
            return await db.ArchivedMonths.AsNoTracking().Where(m => m.TagId == tagId && m.Month == index)
                .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        }

        private async Task<ushort?> GetTagIdAsync(string tag, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(tag))
                return null;
            if (tagIds.TryGetValue(tag, out var cached))
                return cached;
            using var db = dbFactory();
            var id = await db.AuctionTags.AsNoTracking().Where(t => t.Tag == tag).Select(t => (ushort?)t.Id).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
            if (id != null)
            {
                tagIds[tag] = id.Value;
                tagNames[id.Value] = tag;
            }
            return id;
        }
    }
}
