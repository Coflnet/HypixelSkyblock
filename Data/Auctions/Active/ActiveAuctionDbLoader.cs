using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Coflnet.Sky.Core
{
    /// <summary>
    /// Loads currently active auctions from the database without needing an index on Start.
    /// </summary>
    public class ActiveAuctionDbLoader
    {
        private readonly Func<HypixelContext> contextFactory;
        private readonly ILogger logger;

        public ActiveAuctionDbLoader(Func<HypixelContext> contextFactory = null, ILogger logger = null)
        {
            this.contextFactory = contextFactory ?? (() => new HypixelContext());
            this.logger = logger;
        }

        /// <summary>
        /// Binary search over the primary key for roughly the first auction id with Start &gt; notBefore.
        /// </summary>
        public async Task<int> FindLowerBoundIdAsync(DateTime notBefore, CancellationToken ct)
        {
            var maxId = await MaxIdAsync(ct);
            try
            {
                var lo = Math.Max(1, maxId - 8_000_000);
                var hi = maxId;
                while (lo < hi)
                {
                    var mid = lo + (hi - lo) / 2;
                    var probe = await ProbeAsync(mid, ct);
                    if (probe == null || probe.Value.Start > notBefore)
                        hi = mid;
                    else
                        lo = mid + 1;
                }
                return Math.Max(1, lo - 50_000);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                logger?.LogError(e, "finding lower bound id failed, using fallback");
                return Math.Max(1, maxId - 4_500_000);
            }
        }

        public async IAsyncEnumerable<SaveAuction> LoadActiveAsync(int lowerBoundId, DateTime now,
            [EnumeratorCancellation] CancellationToken ct, int chunkSize = 250_000)
        {
            var maxId = await MaxIdAsync(ct);
            for (var from = Math.Max(1, lowerBoundId); from <= maxId; from += chunkSize)
            {
                var to = from + chunkSize;
                await foreach (var auction in QueryChunkAsync(from, to, now, ct))
                    yield return auction;
            }
        }

        public async Task<List<SaveAuction>> LoadByUidsAsync(IReadOnlyCollection<long> uids, DateTime now, CancellationToken ct)
        {
            var result = new List<SaveAuction>();
            foreach (var batch in uids.Chunk(500))
            {
                result.AddRange(await QueryByUidsAsync(batch, now, ct));
            }
            return result;
        }

        protected virtual async Task<int> MaxIdAsync(CancellationToken ct)
        {
            using var ctx = contextFactory();
            return await ctx.Auctions.MaxAsync(a => a.Id, ct);
        }

        /// <summary>Primary key seek for the first auction with Id &gt;= <paramref name="fromId"/></summary>
        protected virtual async Task<(int Id, DateTime Start)?> ProbeAsync(int fromId, CancellationToken ct)
        {
            using var ctx = contextFactory();
            var probe = await ctx.Auctions.AsNoTracking().Where(a => a.Id >= fromId).OrderBy(a => a.Id)
                .Select(a => new { a.Id, a.Start }).FirstOrDefaultAsync(ct);
            return probe == null ? null : (probe.Id, probe.Start);
        }

        protected virtual async IAsyncEnumerable<SaveAuction> QueryChunkAsync(int from, int to, DateTime now,
            [EnumeratorCancellation] CancellationToken ct)
        {
            using var ctx = contextFactory();
            var list = await ctx.Auctions.AsNoTracking()
                .Where(a => a.Id >= from && a.Id < to && a.End > now)
                .Include(a => a.Enchantments).Include(a => a.NbtData).Include(a => a.Bids)
                .AsSplitQuery().ToListAsync(ct);
            foreach (var auction in list)
                yield return auction;
        }

        protected virtual async Task<List<SaveAuction>> QueryByUidsAsync(long[] uids, DateTime now, CancellationToken ct)
        {
            using var ctx = contextFactory();
            return await ctx.Auctions.AsNoTracking()
                .Where(a => uids.Contains(a.UId) && a.End > now)
                .Include(a => a.Enchantments).Include(a => a.NbtData).Include(a => a.Bids)
                .AsSplitQuery().ToListAsync(ct);
        }
    }
}
