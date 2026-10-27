using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microting.EntityFrameworkCore.MySql.Infrastructure;

namespace Coflnet.Sky.Core.Archive
{
    /// <summary><c>auction_tags</c>: small id for every archived tag.</summary>
    public class AuctionTag
    {
        /// <summary>SMALLINT UNSIGNED auto increment; 0 is reserved for month sentinels in <c>archived_months</c>.</summary>
        public ushort Id { get; set; }
        /// <summary>Tag as stored on the auctions (max 40 chars).</summary>
        public string Tag { get; set; }
    }

    /// <summary><c>auction_locations</c>: auction uid to its archive blob (tag, month, part), kept permanently.</summary>
    public class AuctionLocation
    {
        /// <summary><see cref="SaveAuction.UId"/>.</summary>
        public long Uid { get; set; }
        /// <summary><see cref="AuctionTag.Id"/>.</summary>
        public ushort TagId { get; set; }
        /// <summary>Months since 2019-01 (<see cref="ArchiveMonths.ToIndex"/>).</summary>
        public ushort Month { get; set; }
        /// <summary>Part of the tag-month.</summary>
        public byte Part { get; set; }
    }

    /// <summary><c>auction_bidders</c>: a player's bids (one row per bidder and auction; the bid amounts come from the archived auction blob), kept permanently.</summary>
    public class AuctionBidder
    {
        /// <summary><c>Players.Id</c> (<see cref="SaveBids.BidderId"/>).</summary>
        public uint BidderId { get; set; }
        /// <summary>Auction end, unix seconds.</summary>
        public uint End { get; set; }
        /// <summary><see cref="SaveAuction.UId"/>.</summary>
        public long Uid { get; set; }
    }

    /// <summary>
    /// <c>auction_sellers</c>: a player's own auctions (one row per seller and auction), kept permanently. Same shape and ordering as
    /// <c>auction_bidders</c>; the auction itself comes from the archived blob via <c>auction_locations</c>.
    /// </summary>
    public class AuctionSeller
    {
        /// <summary><c>Players.Id</c> of the auctioneer (<see cref="SaveAuction.SellerId"/>), the same id space as <see cref="AuctionBidder.BidderId"/>.</summary>
        public uint SellerId { get; set; }
        /// <summary>Auction end, unix seconds.</summary>
        public uint End { get; set; }
        /// <summary><see cref="SaveAuction.UId"/>.</summary>
        public long Uid { get; set; }
    }

    /// <summary>
    /// <c>auction_seller_backfill</c>: one row per month whose archived R2 parts have been scanned for <c>auction_sellers</c> rows
    /// (written only by SkyIndexer's retention pass after it read every part of the month once; resumable, runs once per month).
    /// </summary>
    public class AuctionSellerBackfillMonth
    {
        /// <summary>Months since 2019-01 (<see cref="ArchiveMonths.ToIndex"/>).</summary>
        public ushort Month { get; set; }
        /// <summary>UTC time the month was finished.</summary>
        public DateTime DoneAt { get; set; }
    }

    /// <summary>
    /// <c>archived_months</c>: one row per archived tag-month. The row with <c>tag_id = 0</c> is the month sentinel:
    /// <c>archived_at</c> = every tag of the month is archived and verified, <c>deleted_at</c> = every MariaDB row of the month is deleted,
    /// <c>scylla_at</c> = every tag of the month has its Scylla rows merged (phase 2).
    /// </summary>
    public class ArchivedMonth
    {
        /// <summary><see cref="AuctionTag.Id"/> or 0 for the sentinel.</summary>
        public ushort TagId { get; set; }
        /// <summary>Months since 2019-01.</summary>
        public ushort Month { get; set; }
        /// <summary>Number of blob parts.</summary>
        public byte Parts { get; set; }
        /// <summary>Archived rows.</summary>
        public uint Rows { get; set; }
        /// <summary>Archived sold rows.</summary>
        public uint SoldRows { get; set; }
        /// <summary>mariadb|scylla|merged.</summary>
        public string Source { get; set; }
        /// <summary>Hash of part 000, or of the concatenated part hashes (<see cref="ArchivedAuctionBlock.CombinedSha256"/>).</summary>
        public string Sha256 { get; set; }
        /// <summary>UTC time the blobs were written.</summary>
        public DateTime ArchivedAt { get; set; }
        /// <summary>UTC time the read-back verification and the index rows were completed.</summary>
        public DateTime? VerifiedAt { get; set; }
        /// <summary>Rows written to <c>auction_locations</c>.</summary>
        public uint? IndexRows { get; set; }
        /// <summary>Rows written to <c>auction_bidders</c>.</summary>
        public uint? IndexBidderRows { get; set; }
        /// <summary>MariaDB auction rows deleted by the retention worker.</summary>
        public uint? DeletedRows { get; set; }
        /// <summary>UTC time the retention worker finished the month.</summary>
        public DateTime? DeletedAt { get; set; }
        /// <summary>Free text for operators.</summary>
        public string Note { get; set; }
        /// <summary>
        /// UTC time phase 2 (SkyAuctions' Scylla sweep) finished: on a tag row, the Scylla rows of the tag-month are merged into
        /// the archive; on the sentinel row, every tag of the month is. Scylla partitions may only be deleted once every month they
        /// overlap has it on its sentinel.
        /// </summary>
        public DateTime? ScyllaAt { get; set; }
    }

    /// <summary>
    /// Archive bookkeeping tables. Separate from <see cref="HypixelContext"/> (same database, own migrations history table
    /// <c>__ArchiveMigrationsHistory</c>) so its migrations never touch the HypixelContext model or the 700 M row Auctions table.
    /// </summary>
    public class ArchiveContext : DbContext
    {
        /// <summary>Migrations history table of this context.</summary>
        public const string MigrationsHistoryTable = "__ArchiveMigrationsHistory";

        /// <summary><c>auction_tags</c></summary>
        public DbSet<AuctionTag> AuctionTags { get; set; }
        /// <summary><c>auction_locations</c></summary>
        public DbSet<AuctionLocation> AuctionLocations { get; set; }
        /// <summary><c>auction_bidders</c></summary>
        public DbSet<AuctionBidder> AuctionBidders { get; set; }
        /// <summary><c>auction_sellers</c></summary>
        public DbSet<AuctionSeller> AuctionSellers { get; set; }
        /// <summary><c>auction_seller_backfill</c></summary>
        public DbSet<AuctionSellerBackfillMonth> AuctionSellerBackfillMonths { get; set; }
        /// <summary><c>archived_months</c></summary>
        public DbSet<ArchivedMonth> ArchivedMonths { get; set; }

        private static IConfiguration _config;

        /// <summary>Call once at startup (same configuration as <see cref="HypixelContext.SetConfiguration"/>).</summary>
        public static void SetConfiguration(IConfiguration config) => _config = config;

        /// <summary>Uses <c>DBConnection</c>/<c>DBVersion</c> like <see cref="HypixelContext"/>.</summary>
        public ArchiveContext() { }

        /// <summary>Explicit options (design time, tests).</summary>
        public ArchiveContext(DbContextOptions<ArchiveContext> options) : base(options) { }

        /// <summary>Apply the provider options shared by every construction path.</summary>
        public static void Configure(DbContextOptionsBuilder optionsBuilder, string connectionString, string version)
        {
            optionsBuilder.UseMySql(connectionString, new MariaDbServerVersion(version),
                opts => opts.CommandTimeout(60).MaxBatchSize(100).MigrationsHistoryTable(MigrationsHistoryTable));
        }

        /// <inheritdoc/>
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                var connStr = _config?["DBConnection"]
                    ?? Environment.GetEnvironmentVariable("DBConnection")
                    ?? "server=mariadb;port=3306";
                var version = _config?["DBVersion"]
                    ?? Environment.GetEnvironmentVariable("DBVersion")
                    ?? "10.3";
                Configure(optionsBuilder, connStr, version);
            }
        }

        /// <inheritdoc/>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            // Charset on tables only (columns inherit it): delegating to the database would make the migration emit
            // ALTER DATABASE ... CHARACTER SET, which must never run against the shared production schema.
            modelBuilder.HasCharSet(CharSet.Utf8Mb4, DelegationModes.ApplyToTables);

            // auction_locations / auction_bidders are PAGE_COMPRESSED=1, applied by raw SQL in migration 20261027180000_ArchiveIndexCompressionAndSellers (no EF table option for it).
            modelBuilder.Entity<AuctionTag>(entity =>
            {
                entity.ToTable("auction_tags");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id").HasColumnType("smallint unsigned").ValueGeneratedOnAdd();
                entity.Property(e => e.Tag).HasColumnName("tag").HasColumnType("varchar(40)").HasMaxLength(40).IsRequired();
                entity.HasIndex(e => e.Tag).IsUnique();
            });

            modelBuilder.Entity<AuctionLocation>(entity =>
            {
                entity.ToTable("auction_locations");
                entity.HasKey(e => e.Uid);
                entity.Property(e => e.Uid).HasColumnName("uid").HasColumnType("bigint").ValueGeneratedNever();
                entity.Property(e => e.TagId).HasColumnName("tag_id").HasColumnType("smallint unsigned");
                entity.Property(e => e.Month).HasColumnName("month").HasColumnType("smallint unsigned");
                entity.Property(e => e.Part).HasColumnName("part").HasColumnType("tinyint unsigned");
            });

            modelBuilder.Entity<AuctionBidder>(entity =>
            {
                entity.ToTable("auction_bidders");
                entity.HasKey(e => new { e.BidderId, e.End, e.Uid });
                entity.Property(e => e.BidderId).HasColumnName("bidder_id").HasColumnType("int unsigned").ValueGeneratedNever();
                entity.Property(e => e.End).HasColumnName("end").HasColumnType("int unsigned").ValueGeneratedNever();
                entity.Property(e => e.Uid).HasColumnName("uid").HasColumnType("bigint").ValueGeneratedNever();
            });

            // auction_sellers is PAGE_COMPRESSED=1 like auction_bidders (raw SQL in migration 20261027180000_ArchiveIndexCompressionAndSellers).
            modelBuilder.Entity<AuctionSeller>(entity =>
            {
                entity.ToTable("auction_sellers");
                entity.HasKey(e => new { e.SellerId, e.End, e.Uid });
                entity.Property(e => e.SellerId).HasColumnName("seller_id").HasColumnType("int unsigned").ValueGeneratedNever();
                entity.Property(e => e.End).HasColumnName("end").HasColumnType("int unsigned").ValueGeneratedNever();
                entity.Property(e => e.Uid).HasColumnName("uid").HasColumnType("bigint").ValueGeneratedNever();
            });

            modelBuilder.Entity<AuctionSellerBackfillMonth>(entity =>
            {
                entity.ToTable("auction_seller_backfill");
                entity.HasKey(e => e.Month);
                entity.Property(e => e.Month).HasColumnName("month").HasColumnType("smallint unsigned").ValueGeneratedNever();
                entity.Property(e => e.DoneAt).HasColumnName("done_at").HasColumnType("datetime");
            });

            modelBuilder.Entity<ArchivedMonth>(entity =>
            {
                entity.ToTable("archived_months");
                entity.HasKey(e => new { e.TagId, e.Month });
                entity.Property(e => e.TagId).HasColumnName("tag_id").HasColumnType("smallint unsigned").ValueGeneratedNever();
                entity.Property(e => e.Month).HasColumnName("month").HasColumnType("smallint unsigned").ValueGeneratedNever();
                entity.Property(e => e.Parts).HasColumnName("parts").HasColumnType("tinyint unsigned");
                entity.Property(e => e.Rows).HasColumnName("rows").HasColumnType("int unsigned");
                entity.Property(e => e.SoldRows).HasColumnName("sold_rows").HasColumnType("int unsigned");
                entity.Property(e => e.Source).HasColumnName("source").HasColumnType("varchar(16)").HasMaxLength(16).IsRequired();
                entity.Property(e => e.Sha256).HasColumnName("sha256").HasColumnType("char(64)").HasMaxLength(64).IsRequired();
                entity.Property(e => e.ArchivedAt).HasColumnName("archived_at").HasColumnType("datetime");
                entity.Property(e => e.VerifiedAt).HasColumnName("verified_at").HasColumnType("datetime");
                entity.Property(e => e.IndexRows).HasColumnName("index_rows").HasColumnType("int unsigned");
                entity.Property(e => e.IndexBidderRows).HasColumnName("index_bidder_rows").HasColumnType("int unsigned");
                entity.Property(e => e.DeletedRows).HasColumnName("deleted_rows").HasColumnType("int unsigned");
                entity.Property(e => e.DeletedAt).HasColumnName("deleted_at").HasColumnType("datetime");
                entity.Property(e => e.Note).HasColumnName("note").HasColumnType("varchar(255)").HasMaxLength(255);
                entity.Property(e => e.ScyllaAt).HasColumnName("scylla_at").HasColumnType("datetime");
            });
        }
    }

    /// <summary>
    /// Lets <c>dotnet ef migrations add --context ArchiveContext</c> build the model without starting the application host.
    /// The connection string is a placeholder; generating migrations never connects.
    /// </summary>
    public class ArchiveContextDesignTimeFactory : IDesignTimeDbContextFactory<ArchiveContext>
    {
        /// <inheritdoc/>
        public ArchiveContext CreateDbContext(string[] args)
        {
            var builder = new DbContextOptionsBuilder<ArchiveContext>();
            ArchiveContext.Configure(builder, "server=localhost;database=design_time", "10.5.5");
            return new ArchiveContext(builder.Options);
        }
    }
}
