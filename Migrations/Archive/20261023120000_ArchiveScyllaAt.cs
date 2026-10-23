using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coflnet.Sky.Core.Migrations.Archive
{
    /// <inheritdoc />
    public partial class ArchiveScyllaAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "scylla_at",
                table: "archived_months",
                type: "datetime",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "scylla_at",
                table: "archived_months");
        }
    }
}
