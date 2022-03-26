using Microsoft.EntityFrameworkCore.Migrations;

namespace Coflnet.Sky.Core.Migrations
{
    public partial class dropOldIndexes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Bids_Bidder",
                table: "Bids");

            migrationBuilder.DropIndex(
                name: "IX_Auctions_AuctioneerId",
                table: "Auctions");

            migrationBuilder.DropIndex(
                name: "IX_Auctions_ItemName",
                table: "Auctions");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Bids_Bidder",
                table: "Bids",
                column: "Bidder");

            migrationBuilder.CreateIndex(
                name: "IX_Auctions_AuctioneerId",
                table: "Auctions",
                column: "AuctioneerId");

            migrationBuilder.CreateIndex(
                name: "IX_Auctions_ItemName",
                table: "Auctions",
                column: "ItemName");
        }
    }
}
