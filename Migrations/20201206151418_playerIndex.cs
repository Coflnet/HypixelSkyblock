using Microsoft.EntityFrameworkCore.Migrations;
using MySql.Data.EntityFrameworkCore.Metadata;

namespace hypixel.Migrations
{
    public partial class playerIndex : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Auctions_Players_AuctioneerId",
                table: "Auctions");

            migrationBuilder.DropForeignKey(
                name: "FK_Bids_Players_Bidder",
                table: "Bids");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Players",
                table: "Players");

            migrationBuilder.DropIndex(
                name: "IX_Auctions_AuctioneerId",
                table: "Auctions");

            migrationBuilder.AlterColumn<string>(
                name: "UuId",
                table: "Players",
                type: "char(32)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "char(32)");

            migrationBuilder.AddColumn<int>(
                name: "Id",
                table: "Players",
                nullable: false,
                defaultValue: 0)
                .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn);

            migrationBuilder.AddColumn<int>(
                name: "BidderId",
                table: "Bids",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "AuctioneerIntId",
                table: "Auctions",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Players",
                table: "Players",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_Players_UuId",
                table: "Players",
                column: "UuId");

            migrationBuilder.CreateIndex(
                name: "IX_Bids_BidderId",
                table: "Bids",
                column: "BidderId");

            migrationBuilder.CreateIndex(
                name: "IX_Auctions_AuctioneerIntId",
                table: "Auctions",
                column: "AuctioneerIntId");

            migrationBuilder.AddForeignKey(
                name: "FK_Auctions_Players_AuctioneerIntId",
                table: "Auctions",
                column: "AuctioneerIntId",
                principalTable: "Players",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Bids_Players_BidderId",
                table: "Bids",
                column: "BidderId",
                principalTable: "Players",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Auctions_Players_AuctioneerIntId",
                table: "Auctions");

            migrationBuilder.DropForeignKey(
                name: "FK_Bids_Players_BidderId",
                table: "Bids");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Players",
                table: "Players");

            migrationBuilder.DropIndex(
                name: "IX_Players_UuId",
                table: "Players");

            migrationBuilder.DropIndex(
                name: "IX_Bids_BidderId",
                table: "Bids");

            migrationBuilder.DropIndex(
                name: "IX_Auctions_AuctioneerIntId",
                table: "Auctions");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "BidderId",
                table: "Bids");

            migrationBuilder.DropColumn(
                name: "AuctioneerIntId",
                table: "Auctions");

            migrationBuilder.AlterColumn<string>(
                name: "UuId",
                table: "Players",
                type: "char(32)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "char(32)",
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Players",
                table: "Players",
                column: "UuId");

            migrationBuilder.CreateIndex(
                name: "IX_Auctions_AuctioneerId",
                table: "Auctions",
                column: "AuctioneerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Auctions_Players_AuctioneerId",
                table: "Auctions",
                column: "AuctioneerId",
                principalTable: "Players",
                principalColumn: "UuId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Bids_Players_Bidder",
                table: "Bids",
                column: "Bidder",
                principalTable: "Players",
                principalColumn: "UuId",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
