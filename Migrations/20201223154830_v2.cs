using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Coflnet.Sky.Core.Migrations
{
    public partial class v2 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Auctions_Players_AuctioneerId",
                table: "Auctions");

            migrationBuilder.DropForeignKey(
                name: "FK_Bids_Players_Bidder",
                table: "Bids");

            migrationBuilder.DropForeignKey(
                name: "FK_Enchantment_Auctions_SaveAuctionId",
                table: "Enchantment");

            migrationBuilder.AddColumn<int>(
                name: "Id",
                table: "Players",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<byte>(
                name: "Type",
                table: "Enchantment",
                type: "TINYINT(3)",
                nullable: false,
                oldClrType: typeof(byte),
                oldType: "TINYINT(2)");

            migrationBuilder.AlterColumn<int>(
                name: "SaveAuctionId",
                table: "Enchantment",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<byte>(
                name: "Level",
                table: "Enchantment",
                type: "TINYINT(3)",
                nullable: false,
                oldClrType: typeof(short),
                oldType: "smallint");

            migrationBuilder.AddColumn<int>(
                name: "ItemType",
                table: "Enchantment",
                type: "MEDIUMINT(9)",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "BidderId",
                table: "Bids",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "Bin",
                table: "Auctions",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ItemId",
                table: "Auctions",
                type: "MEDIUMINT(9)",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SellerId",
                table: "Auctions",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Items",
                columns: table => new
                {
                    Id = table.Column<int>(type: "MEDIUMINT(9)", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Tag = table.Column<string>(maxLength: 44, nullable: true),
                    Name = table.Column<string>(nullable: true),
                    Description = table.Column<string>(nullable: true),
                    Category = table.Column<int>(nullable: false),
                    Tier = table.Column<int>(nullable: false),
                    IconUrl = table.Column<string>(nullable: true),
                    Extra = table.Column<string>(nullable: true),
                    MinecraftType = table.Column<string>(maxLength: 44, nullable: true),
                    color = table.Column<string>(maxLength: 12, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Items", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Prices",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Min = table.Column<float>(nullable: false),
                    Max = table.Column<float>(nullable: false),
                    Avg = table.Column<float>(nullable: false),
                    Volume = table.Column<int>(nullable: false),
                    ItemId = table.Column<int>(nullable: false),
                    Date = table.Column<DateTime>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Prices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AltItemNames",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Name = table.Column<string>(nullable: true),
                    DBItemId = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AltItemNames", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AltItemNames_Items_DBItemId",
                        column: x => x.DBItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Players_Id",
                table: "Players",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_Enchantment_ItemType_Type_Level",
                table: "Enchantment",
                columns: new[] { "ItemType", "Type", "Level" });

            migrationBuilder.CreateIndex(
                name: "IX_Bids_BidderId",
                table: "Bids",
                column: "BidderId");

            migrationBuilder.CreateIndex(
                name: "IX_Auctions_SellerId",
                table: "Auctions",
                column: "SellerId");

            migrationBuilder.CreateIndex(
                name: "IX_Auctions_ItemId_End",
                table: "Auctions",
                columns: new[] { "ItemId", "End" });

            migrationBuilder.CreateIndex(
                name: "IX_AltItemNames_DBItemId",
                table: "AltItemNames",
                column: "DBItemId");

            migrationBuilder.CreateIndex(
                name: "IX_AltItemNames_Name",
                table: "AltItemNames",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Items_Tag",
                table: "Items",
                column: "Tag",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Prices_ItemId_Date",
                table: "Prices",
                columns: new[] { "ItemId", "Date" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Enchantment_Auctions_SaveAuctionId",
                table: "Enchantment",
                column: "SaveAuctionId",
                principalTable: "Auctions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Enchantment_Auctions_SaveAuctionId",
                table: "Enchantment");

            migrationBuilder.DropTable(
                name: "AltItemNames");

            migrationBuilder.DropTable(
                name: "Prices");

            migrationBuilder.DropTable(
                name: "Items");

            migrationBuilder.DropIndex(
                name: "IX_Players_Id",
                table: "Players");

            migrationBuilder.DropIndex(
                name: "IX_Enchantment_ItemType_Type_Level",
                table: "Enchantment");

            migrationBuilder.DropIndex(
                name: "IX_Bids_BidderId",
                table: "Bids");

            migrationBuilder.DropIndex(
                name: "IX_Auctions_SellerId",
                table: "Auctions");

            migrationBuilder.DropIndex(
                name: "IX_Auctions_ItemId_End",
                table: "Auctions");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "ItemType",
                table: "Enchantment");

            migrationBuilder.DropColumn(
                name: "BidderId",
                table: "Bids");

            migrationBuilder.DropColumn(
                name: "Bin",
                table: "Auctions");

            migrationBuilder.DropColumn(
                name: "ItemId",
                table: "Auctions");

            migrationBuilder.DropColumn(
                name: "SellerId",
                table: "Auctions");

            migrationBuilder.AlterColumn<byte>(
                name: "Type",
                table: "Enchantment",
                type: "TINYINT(2)",
                nullable: false,
                oldClrType: typeof(byte),
                oldType: "TINYINT(3)");

            migrationBuilder.AlterColumn<int>(
                name: "SaveAuctionId",
                table: "Enchantment",
                type: "int",
                nullable: true,
                oldClrType: typeof(int));

            migrationBuilder.AlterColumn<short>(
                name: "Level",
                table: "Enchantment",
                type: "smallint",
                nullable: false,
                oldClrType: typeof(byte),
                oldType: "TINYINT(3)");

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

            migrationBuilder.AddForeignKey(
                name: "FK_Enchantment_Auctions_SaveAuctionId",
                table: "Enchantment",
                column: "SaveAuctionId",
                principalTable: "Auctions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
