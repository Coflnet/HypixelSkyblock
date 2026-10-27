using System.Linq;
using Coflnet.Sky.Core.Archive;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NUnit.Framework;

namespace Tests
{
    public class ArchiveContextTests
    {
        private static ArchiveContext Create() => new ArchiveContextDesignTimeFactory().CreateDbContext(new string[0]);

        /// <summary>EF refuses to Migrate() when the model and the snapshot differ; this catches a missing migration offline.</summary>
        [Test]
        public void ModelMatchesMigrationSnapshot()
        {
            using var context = Create();
            Assert.That(context.Database.HasPendingModelChanges(), Is.False);
            Assert.That(context.Database.GetMigrations().ToList(), Has.Count.EqualTo(3));
        }

        /// <summary>One idempotent migration: sellers + marker table created first (compressed inside CREATE), then bidders lose amount and both old index tables are compressed.</summary>
        [Test]
        public void IndexMigrationIsOneIdempotentMigration()
        {
            using var context = Create();
            var migrations = context.Database.GetMigrations().ToList();
            Assert.That(migrations.Last(), Is.EqualTo("20261027180000_ArchiveIndexCompressionAndSellers"));
            var sql = context.GetService<IMigrator>().GenerateScript("20261023120000_ArchiveScyllaAt", "20261027180000_ArchiveIndexCompressionAndSellers");
            Assert.That(sql, Does.Contain("CREATE TABLE IF NOT EXISTS `auction_sellers`"));
            Assert.That(sql, Does.Contain("CONSTRAINT `PK_auction_sellers` PRIMARY KEY (`seller_id`, `end`, `uid`)"));
            Assert.That(sql, Does.Contain(") CHARACTER SET=utf8mb4 PAGE_COMPRESSED=1;"));
            Assert.That(sql, Does.Contain("CREATE TABLE IF NOT EXISTS `auction_seller_backfill`"));
            Assert.That(sql, Does.Not.Contain("ALTER TABLE auction_sellers"), "compression is a CREATE option");
            Assert.That(sql, Does.Contain("ALTER TABLE auction_bidders DROP COLUMN IF EXISTS amount;"), "instant drop on its own");
            Assert.That(sql, Does.Contain("ALTER TABLE auction_bidders PAGE_COMPRESSED=1;"), "option change as a separate metadata-only statement");
            Assert.That(sql, Does.Not.Contain("amount, PAGE_COMPRESSED"));
            Assert.That(sql, Does.Contain("ALTER TABLE auction_locations PAGE_COMPRESSED=1;"));
            Assert.That(sql.IndexOf("CREATE TABLE IF NOT EXISTS `auction_sellers`"), Is.LessThan(sql.IndexOf("ALTER TABLE auction_bidders")), "creates first");
            Assert.That(sql, Does.Not.Contain("DROP TABLE"));
            Assert.That(context.Model.FindEntityType(typeof(AuctionBidder)).FindProperty("Amount"), Is.Null);
        }

        /// <summary>auction_sellers has exactly the shape of auction_bidders.</summary>
        [Test]
        public void SellersHaveTheShapeOfBidders()
        {
            using var context = Create();
            var sellers = context.Model.FindEntityType(typeof(AuctionSeller));
            var bidders = context.Model.FindEntityType(typeof(AuctionBidder));
            string Shape(Microsoft.EntityFrameworkCore.Metadata.IEntityType e) => string.Join(",", e.GetProperties().Select(p => p.GetColumnType() + "/" + p.IsNullable));
            Assert.That(Shape(sellers), Is.EqualTo(Shape(bidders)));
            Assert.That(sellers.GetProperties().Select(p => p.GetColumnName()), Is.EqualTo(new[] { "seller_id", "end", "uid" }));
            Assert.That(sellers.FindPrimaryKey().Properties.Select(p => p.GetColumnName()), Is.EqualTo(new[] { "seller_id", "end", "uid" }));
        }

        /// <summary>Phase 2 marker: the migration only adds the nullable column, so SkyIndexer's running code and rows are unaffected.</summary>
        [Test]
        public void ScyllaAtMigrationOnlyAddsANullableColumn()
        {
            using var context = Create();
            var migrator = context.GetService<IMigrator>();
            var sql = migrator.GenerateScript("20261021101936_ArchiveInit", "20261023120000_ArchiveScyllaAt");
            TestContext.Out.WriteLine(sql);
            Assert.That(sql, Does.Contain("ALTER TABLE `archived_months` ADD `scylla_at` datetime NULL"));
            Assert.That(sql, Does.Not.Contain("DROP"));
            Assert.That(sql, Does.Not.Contain("CREATE TABLE `archived_months`"));
            var property = context.Model.FindEntityType(typeof(ArchivedMonth)).FindProperty(nameof(ArchivedMonth.ScyllaAt));
            Assert.That(property.GetColumnName(), Is.EqualTo("scylla_at"));
            Assert.That(property.IsNullable, Is.True);
        }

        [Test]
        public void TablesAndHistoryAreSeparateFromHypixelContext()
        {
            using var context = Create();
            var tables = context.Model.GetEntityTypes().Select(e => e.GetTableName()).OrderBy(t => t).ToList();
            Assert.That(tables, Is.EqualTo(new[] { "archived_months", "auction_bidders", "auction_locations", "auction_seller_backfill", "auction_sellers", "auction_tags" }));
            var sql = context.Database.GenerateCreateScript();
            Assert.That(sql, Does.Not.Contain("ALTER DATABASE"));
            Assert.That(sql, Does.Not.Contain("`Auctions`"));
            Assert.That(ArchiveContext.MigrationsHistoryTable, Is.EqualTo("__ArchiveMigrationsHistory"));
        }
    }
}
