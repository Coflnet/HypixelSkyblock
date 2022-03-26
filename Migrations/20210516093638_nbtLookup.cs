using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Coflnet.Sky.Core.Migrations
{
    public partial class nbtLookup : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<bool>(
                name: "ChangedFlag",
                table: "Players",
                nullable: false,
                oldClrType: typeof(ulong),
                oldType: "bit");

            migrationBuilder.AlterColumn<bool>(
                name: "Reforgeable",
                table: "Items",
                nullable: false,
                oldClrType: typeof(ulong),
                oldType: "bit");

            migrationBuilder.AlterColumn<bool>(
                name: "IsBazaar",
                table: "Items",
                nullable: false,
                oldClrType: typeof(ulong),
                oldType: "bit");

            migrationBuilder.AlterColumn<bool>(
                name: "Enchantable",
                table: "Items",
                nullable: false,
                oldClrType: typeof(ulong),
                oldType: "bit");

            migrationBuilder.AlterColumn<string>(
                name: "GoogleId",
                table: "Users",
                type: "varchar(32)",
                maxLength: 32,
                nullable: true,
                oldClrType: typeof(string),
                oldNullable: true);


            migrationBuilder.CreateTable(
                name: "NBTKeys",
                columns: table => new
                {
                    Id = table.Column<short>(nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Slug = table.Column<string>(maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NBTKeys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NBTLookups",
                columns: table => new
                {
                    AuctionId = table.Column<int>(nullable: false),
                    KeyId = table.Column<short>(nullable: false),
                    Value = table.Column<long>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NBTLookups", x => new { x.AuctionId, x.KeyId });
                    table.ForeignKey(
                        name: "FK_NBTLookups_Auctions_AuctionId",
                        column: x => x.AuctionId,
                        principalTable: "Auctions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NBTValues",
                columns: table => new
                {
                    Id = table.Column<short>(nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    KeyId = table.Column<short>(nullable: false),
                    Value = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NBTValues", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_GoogleId",
                table: "Users",
                column: "GoogleId");

            migrationBuilder.CreateIndex(
                name: "IX_Device_ConnectionId",
                table: "Device",
                column: "ConnectionId");

            migrationBuilder.CreateIndex(
                name: "IX_NBTKeys_Slug",
                table: "NBTKeys",
                column: "Slug");

            migrationBuilder.CreateIndex(
                name: "IX_NBTLookups_KeyId_Value",
                table: "NBTLookups",
                columns: new[] { "KeyId", "Value" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NBTKeys");

            migrationBuilder.DropTable(
                name: "NBTLookups");

            migrationBuilder.DropIndex(
                name: "IX_Users_GoogleId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Device_ConnectionId",
                table: "Device");

            migrationBuilder.AlterColumn<ulong>(
                name: "ChangedFlag",
                table: "Players",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool));

            migrationBuilder.AlterColumn<ulong>(
                name: "Reforgeable",
                table: "Items",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool));

            migrationBuilder.AlterColumn<ulong>(
                name: "IsBazaar",
                table: "Items",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool));

            migrationBuilder.AlterColumn<ulong>(
                name: "Enchantable",
                table: "Items",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool));

            migrationBuilder.AlterColumn<ulong>(
                name: "Claimed",
                table: "Auctions",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool));

            migrationBuilder.AlterColumn<ulong>(
                name: "Bin",
                table: "Auctions",
                type: "bit",
                nullable: false,
                oldClrType: typeof(bool));
        }
    }
}
