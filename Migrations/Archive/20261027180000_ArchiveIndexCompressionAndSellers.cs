using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coflnet.Sky.Core.Migrations.Archive
{
    /// <summary>
    /// Index table changes of the archive in one migration:
    /// 1. new <c>auction_sellers(seller_id, end, uid)</c> (same shape as <c>auction_bidders</c>, PAGE_COMPRESSED=1 as a table option)
    ///    and the tiny <c>auction_seller_backfill(month, done_at)</c> marker table, both empty and instant;
    /// 2. <c>auction_bidders</c> loses <c>amount</c> (bid amounts come from the archived auction blob) and gets PAGE_COMPRESSED=1,
    ///    <c>auction_locations</c> gets PAGE_COMPRESSED=1 (zlib, needs hole punching on ext4/xfs).
    /// MariaDB DDL is not transactional and EF records the migration only at the end, so every statement is idempotent
    /// (IF NOT EXISTS / IF EXISTS; repeating PAGE_COMPRESSED=1 is a no-op). Production already has PAGE_COMPRESSED=1 on both
    /// index tables (applied by hand) and dropping the column was measured instant, so the ALTERs are metadata only there; on a
    /// database without it they rebuild the tables for many minutes (AuctionArchiveJob.EnsureSchemaAsync raises the command timeout).
    /// Raw SQL because EF has no table option for the compression.
    /// </summary>
    /// <inheritdoc />
    public partial class ArchiveIndexCompressionAndSellers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"CREATE TABLE IF NOT EXISTS `auction_sellers` (
    `seller_id` int unsigned NOT NULL,
    `end` int unsigned NOT NULL,
    `uid` bigint NOT NULL,
    CONSTRAINT `PK_auction_sellers` PRIMARY KEY (`seller_id`, `end`, `uid`)
) CHARACTER SET=utf8mb4 PAGE_COMPRESSED=1;");
            migrationBuilder.Sql(@"CREATE TABLE IF NOT EXISTS `auction_seller_backfill` (
    `month` smallint unsigned NOT NULL,
    `done_at` datetime NOT NULL,
    CONSTRAINT `PK_auction_seller_backfill` PRIMARY KEY (`month`)
) CHARACTER SET=utf8mb4;");
            // two statements on purpose: a DROP COLUMN alone can use MariaDB's instant drop, combined with the table option it may rebuild the table
            migrationBuilder.Sql("ALTER TABLE auction_bidders DROP COLUMN IF EXISTS amount;");
            migrationBuilder.Sql("ALTER TABLE auction_bidders PAGE_COMPRESSED=1;");
            migrationBuilder.Sql("ALTER TABLE auction_locations PAGE_COMPRESSED=1;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE auction_locations PAGE_COMPRESSED=0;");
            migrationBuilder.Sql("ALTER TABLE auction_bidders ADD COLUMN IF NOT EXISTS amount bigint unsigned NOT NULL DEFAULT 0, PAGE_COMPRESSED=0;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS `auction_seller_backfill`;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS `auction_sellers`;");
        }
    }
}
