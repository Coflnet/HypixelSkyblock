using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coflnet.Sky.Core.Migrations.Archive
{
    /// <inheritdoc />
    public partial class ArchiveInit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "archived_months",
                columns: table => new
                {
                    tag_id = table.Column<ushort>(type: "smallint unsigned", nullable: false),
                    month = table.Column<ushort>(type: "smallint unsigned", nullable: false),
                    parts = table.Column<byte>(type: "tinyint unsigned", nullable: false),
                    rows = table.Column<uint>(type: "int unsigned", nullable: false),
                    sold_rows = table.Column<uint>(type: "int unsigned", nullable: false),
                    source = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false),
                    sha256 = table.Column<string>(type: "char(64)", maxLength: 64, nullable: false),
                    archived_at = table.Column<DateTime>(type: "datetime", nullable: false),
                    verified_at = table.Column<DateTime>(type: "datetime", nullable: true),
                    index_rows = table.Column<uint>(type: "int unsigned", nullable: true),
                    index_bidder_rows = table.Column<uint>(type: "int unsigned", nullable: true),
                    deleted_rows = table.Column<uint>(type: "int unsigned", nullable: true),
                    deleted_at = table.Column<DateTime>(type: "datetime", nullable: true),
                    note = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_archived_months", x => new { x.tag_id, x.month });
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "auction_bidders",
                columns: table => new
                {
                    bidder_id = table.Column<uint>(type: "int unsigned", nullable: false),
                    end = table.Column<uint>(type: "int unsigned", nullable: false),
                    uid = table.Column<long>(type: "bigint", nullable: false),
                    amount = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_auction_bidders", x => new { x.bidder_id, x.end, x.uid });
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "auction_locations",
                columns: table => new
                {
                    uid = table.Column<long>(type: "bigint", nullable: false),
                    tag_id = table.Column<ushort>(type: "smallint unsigned", nullable: false),
                    month = table.Column<ushort>(type: "smallint unsigned", nullable: false),
                    part = table.Column<byte>(type: "tinyint unsigned", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_auction_locations", x => x.uid);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "auction_tags",
                columns: table => new
                {
                    id = table.Column<ushort>(type: "smallint unsigned", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    tag = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_auction_tags", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_auction_tags_tag",
                table: "auction_tags",
                column: "tag",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "archived_months");

            migrationBuilder.DropTable(
                name: "auction_bidders");

            migrationBuilder.DropTable(
                name: "auction_locations");

            migrationBuilder.DropTable(
                name: "auction_tags");
        }
    }
}
