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
            Assert.That(context.Database.GetMigrations().ToList(), Has.Count.EqualTo(2));
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
            Assert.That(tables, Is.EqualTo(new[] { "archived_months", "auction_bidders", "auction_locations", "auction_tags" }));
            var sql = context.Database.GenerateCreateScript();
            Assert.That(sql, Does.Not.Contain("ALTER DATABASE"));
            Assert.That(sql, Does.Not.Contain("`Auctions`"));
            Assert.That(ArchiveContext.MigrationsHistoryTable, Is.EqualTo("__ArchiveMigrationsHistory"));
        }
    }
}
