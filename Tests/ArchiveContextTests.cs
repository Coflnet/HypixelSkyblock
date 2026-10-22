using System.Linq;
using Coflnet.Sky.Core.Archive;
using Microsoft.EntityFrameworkCore;
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
            Assert.That(context.Database.GetMigrations().ToList(), Has.Count.EqualTo(1));
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
