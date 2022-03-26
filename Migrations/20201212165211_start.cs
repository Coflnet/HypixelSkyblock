using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;


namespace Coflnet.Sky.Core.Migrations
{
    public partial class start : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BazaarPull",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Timestamp = table.Column<DateTime>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BazaarPull", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NbtData",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    data = table.Column<byte[]>(maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NbtData", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Players",
                columns: table => new
                {
                    UuId = table.Column<string>(type: "char(32)", nullable: false),
                    Name = table.Column<string>(maxLength: 16, nullable: true),
                    UpdatedAt = table.Column<DateTime>(nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySqlValueGenerationStrategy.ComputedColumn),
                    ChangedFlag = table.Column<bool>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Players", x => x.UuId);
                });

            migrationBuilder.CreateTable(
                name: "SubscribeItem",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ItemTag = table.Column<string>(maxLength: 45, nullable: true),
                    PlayerUuid = table.Column<string>(type: "char(32)", nullable: true),
                    Price = table.Column<long>(nullable: false),
                    GeneratedAt = table.Column<DateTime>(nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySqlValueGenerationStrategy.ComputedColumn),
                    Type = table.Column<int>(nullable: false),
                    Token = table.Column<string>(nullable: true),
                    Initiator = table.Column<string>(maxLength: 32, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscribeItem", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BazaarPrices",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    PullInstanceId = table.Column<int>(nullable: true),
                    ProductId = table.Column<string>(maxLength: 40, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BazaarPrices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BazaarPrices_BazaarPull_PullInstanceId",
                        column: x => x.PullInstanceId,
                        principalTable: "BazaarPull",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Auctions",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Uuid = table.Column<string>(type: "char(32)", nullable: true),
                    Claimed = table.Column<bool>(nullable: false),
                    Count = table.Column<int>(nullable: false),
                    StartingBid = table.Column<long>(nullable: false),
                    Tag = table.Column<string>(maxLength: 40, nullable: true),
                    ItemName = table.Column<string>(maxLength: 45, nullable: true),
                    Start = table.Column<DateTime>(nullable: false),
                    End = table.Column<DateTime>(nullable: false),
                    AuctioneerId = table.Column<string>(type: "char(32)", nullable: true),
                    ProfileId = table.Column<string>(type: "char(32)", nullable: true),
                    HighestBidAmount = table.Column<long>(nullable: false),
                    AnvilUses = table.Column<short>(nullable: false),
                    NbtDataId = table.Column<int>(nullable: true),
                    ItemCreatedAt = table.Column<DateTime>(nullable: false),
                    Reforge = table.Column<byte>(type: "TINYINT(2)", nullable: false),
                    Category = table.Column<byte>(type: "TINYINT(2)", nullable: false),
                    Tier = table.Column<byte>(type: "TINYINT(2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Auctions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Auctions_Players_AuctioneerId",
                        column: x => x.AuctioneerId,
                        principalTable: "Players",
                        principalColumn: "UuId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Auctions_NbtData_NbtDataId",
                        column: x => x.NbtDataId,
                        principalTable: "NbtData",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BuyOrder",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Amount = table.Column<int>(nullable: false),
                    PricePerUnit = table.Column<double>(nullable: false),
                    Orders = table.Column<short>(nullable: false),
                    ProductInfoId = table.Column<int>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BuyOrder", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BuyOrder_BazaarPrices_ProductInfoId",
                        column: x => x.ProductInfoId,
                        principalTable: "BazaarPrices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QuickStatus",
                columns: table => new
                {
                    ID = table.Column<int>(nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    QuickStatusID = table.Column<int>(nullable: true),
                    BuyPrice = table.Column<double>(nullable: false),
                    BuyVolume = table.Column<long>(nullable: false),
                    BuyMovingWeek = table.Column<long>(nullable: false),
                    BuyOrders = table.Column<int>(nullable: false),
                    SellPrice = table.Column<double>(nullable: false),
                    SellVolume = table.Column<long>(nullable: false),
                    SellMovingWeek = table.Column<long>(nullable: false),
                    SellOrders = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuickStatus", x => x.ID);
                    table.ForeignKey(
                        name: "FK_QuickStatus_BazaarPrices_QuickStatusID",
                        column: x => x.QuickStatusID,
                        principalTable: "BazaarPrices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SellOrder",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Amount = table.Column<int>(nullable: false),
                    PricePerUnit = table.Column<double>(nullable: false),
                    Orders = table.Column<short>(nullable: false),
                    ProductInfoId = table.Column<int>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SellOrder", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SellOrder_BazaarPrices_ProductInfoId",
                        column: x => x.ProductInfoId,
                        principalTable: "BazaarPrices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Bids",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Uuid = table.Column<int>(nullable: true),
                    Bidder = table.Column<string>(type: "char(32)", nullable: true),
                    ProfileId = table.Column<string>(type: "char(32)", nullable: true),
                    Amount = table.Column<long>(nullable: false),
                    Timestamp = table.Column<DateTime>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bids", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Bids_Players_Bidder",
                        column: x => x.Bidder,
                        principalTable: "Players",
                        principalColumn: "UuId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Bids_Auctions_Uuid",
                        column: x => x.Uuid,
                        principalTable: "Auctions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Enchantment",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Type = table.Column<byte>(type: "TINYINT(2)", nullable: false),
                    Level = table.Column<short>(nullable: false),
                    SaveAuctionId = table.Column<int>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Enchantment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Enchantment_Auctions_SaveAuctionId",
                        column: x => x.SaveAuctionId,
                        principalTable: "Auctions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UuId",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    value = table.Column<string>(type: "char(32)", nullable: true),
                    SaveAuctionId = table.Column<int>(nullable: true),
                    SaveAuctionId1 = table.Column<int>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UuId", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UuId_Auctions_SaveAuctionId",
                        column: x => x.SaveAuctionId,
                        principalTable: "Auctions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UuId_Auctions_SaveAuctionId1",
                        column: x => x.SaveAuctionId1,
                        principalTable: "Auctions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Auctions_AuctioneerId",
                table: "Auctions",
                column: "AuctioneerId");

            migrationBuilder.CreateIndex(
                name: "IX_Auctions_End",
                table: "Auctions",
                column: "End");

            migrationBuilder.CreateIndex(
                name: "IX_Auctions_ItemName",
                table: "Auctions",
                column: "ItemName");

            migrationBuilder.CreateIndex(
                name: "IX_Auctions_NbtDataId",
                table: "Auctions",
                column: "NbtDataId");

            migrationBuilder.CreateIndex(
                name: "IX_Auctions_Uuid",
                table: "Auctions",
                column: "Uuid",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BazaarPrices_ProductId",
                table: "BazaarPrices",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_BazaarPrices_PullInstanceId",
                table: "BazaarPrices",
                column: "PullInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_BazaarPull_Timestamp",
                table: "BazaarPull",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_Bids_Bidder",
                table: "Bids",
                column: "Bidder");

            migrationBuilder.CreateIndex(
                name: "IX_Bids_Uuid",
                table: "Bids",
                column: "Uuid");

            migrationBuilder.CreateIndex(
                name: "IX_BuyOrder_ProductInfoId",
                table: "BuyOrder",
                column: "ProductInfoId");

            migrationBuilder.CreateIndex(
                name: "IX_Enchantment_SaveAuctionId",
                table: "Enchantment",
                column: "SaveAuctionId");

            migrationBuilder.CreateIndex(
                name: "IX_Players_Name",
                table: "Players",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_QuickStatus_QuickStatusID",
                table: "QuickStatus",
                column: "QuickStatusID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SellOrder_ProductInfoId",
                table: "SellOrder",
                column: "ProductInfoId");

            migrationBuilder.CreateIndex(
                name: "IX_UuId_SaveAuctionId",
                table: "UuId",
                column: "SaveAuctionId");

            migrationBuilder.CreateIndex(
                name: "IX_UuId_SaveAuctionId1",
                table: "UuId",
                column: "SaveAuctionId1");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Bids");

            migrationBuilder.DropTable(
                name: "BuyOrder");

            migrationBuilder.DropTable(
                name: "Enchantment");

            migrationBuilder.DropTable(
                name: "QuickStatus");

            migrationBuilder.DropTable(
                name: "SellOrder");

            migrationBuilder.DropTable(
                name: "SubscribeItem");

            migrationBuilder.DropTable(
                name: "UuId");

            migrationBuilder.DropTable(
                name: "BazaarPrices");

            migrationBuilder.DropTable(
                name: "Auctions");

            migrationBuilder.DropTable(
                name: "BazaarPull");

            migrationBuilder.DropTable(
                name: "Players");

            migrationBuilder.DropTable(
                name: "NbtData");
        }
    }
}
