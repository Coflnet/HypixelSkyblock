using Microsoft.EntityFrameworkCore;
using System;

namespace Coflnet.Sky.Core
{
    /// <summary>
    /// Database context that contains a parallel copy of <see cref="SaveAuction"/> entries
    /// for currently active auctions only.
    ///
    /// This context exists so generic filter queries (e.g. <c>SkyFilter</c> filters) can be
    /// evaluated against the comparatively small "currently on the auction house" data set
    /// without having to scan the historic auctions table.
    ///
    /// Differences to <see cref="HypixelContext"/>:
    /// <list type="bullet">
    ///  <item>Uses its own connection string (<c>ActiveAuctionsDBConnection</c>).
    ///        The intention is that this points to a separate, small database / schema.</item>
    ///  <item>Adds additional indexes that are useful for cross-item filtering
    ///        (price, end-time, bin status, category/tier) rather than the
    ///        item-id-first lookup pattern used by the historic context.</item>
    ///  <item>Auctions are inserted by the indexer when they appear and removed
    ///        again as soon as they end / are sold.</item>
    /// </list>
    /// </summary>
    public class ActiveAuctionsContext : HypixelContext
    {
        /// <summary>
        /// Connection string for the active-only auctions database.
        /// This must be configured explicitly so the active projection can never write to
        /// the historic auction database by accident.
        /// </summary>
        public static string ActiveDbContextId => _config?["ActiveAuctionsDBConnection"]
            ?? Environment.GetEnvironmentVariable("ActiveAuctionsDBConnection");

        public static bool IsConfigured => !string.IsNullOrWhiteSpace(ActiveDbContextId);

        private static string DbVer => _config?["DBVersion"]
            ?? Environment.GetEnvironmentVariable("DBVersion")
            ?? "10.3";

        /// <inheritdoc />
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!IsConfigured)
                throw new System.InvalidOperationException("ActiveAuctionsDBConnection is not configured");

            optionsBuilder.UseMySql(
                ActiveDbContextId,
                new MariaDbServerVersion(DbVer),
                opts => opts.CommandTimeout(60).MaxBatchSize(100))
                .EnableSensitiveDataLogging();
        }

        /// <inheritdoc />
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // reuse all base mappings (entities, relationships, FKs, base indexes)
            base.OnModelCreating(modelBuilder);

            // additional indexes optimised for "all currently active auctions" queries
            // where the primary access pattern is NOT by item id
            modelBuilder.Entity<SaveAuction>(entity =>
            {
                // ordering / cursor based queries
                entity.HasIndex(e => e.End);
                entity.HasIndex(e => e.StartingBid);
                entity.HasIndex(e => e.HighestBidAmount);
                // common filter combinations
                entity.HasIndex(e => new { e.Bin, e.End });
                entity.HasIndex(e => new { e.Category, e.Tier });
                entity.HasIndex(e => new { e.Tier, e.End });
            });
        }
    }
}
