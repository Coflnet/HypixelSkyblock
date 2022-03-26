using Microsoft.EntityFrameworkCore.Migrations;

namespace Coflnet.Sky.Core.Migrations
{
    public partial class auctionsuid : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "UId",
                table: "Players",
                nullable: false,
                defaultValue: 0);


            migrationBuilder.AddColumn<long>(
                name: "UId",
                table: "Auctions",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateIndex(
                name: "IX_Players_UId",
                table: "Players",
                column: "UId");

            migrationBuilder.CreateIndex(
                name: "IX_Auctions_UId",
                table: "Auctions",
                column: "UId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Players_UId",
                table: "Players");

            migrationBuilder.DropIndex(
                name: "IX_Auctions_UId",
                table: "Auctions");

            migrationBuilder.DropColumn(
                name: "UId",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "UId",
                table: "Auctions");
        }
    }
}
