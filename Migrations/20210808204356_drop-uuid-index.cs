using Microsoft.EntityFrameworkCore.Migrations;

namespace Coflnet.Sky.Core.Migrations
{
    public partial class dropuuidindex : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Auctions_UId",
                table: "Auctions");

            migrationBuilder.DropIndex(
                name: "IX_Auctions_Uuid",
                table: "Auctions");

            migrationBuilder.AlterColumn<bool>(
                name: "Claimed",
                table: "Auctions",
                nullable: false,
                oldClrType: typeof(ulong),
                oldType: "bit");

            migrationBuilder.AlterColumn<bool>(
                name: "Bin",
                table: "Auctions",
                nullable: false,
                oldClrType: typeof(ulong),
                oldType: "bit");

            migrationBuilder.CreateIndex(
                name: "IX_Auctions_UId",
                table: "Auctions",
                column: "UId",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Auctions_UId",
                table: "Auctions");

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

            migrationBuilder.CreateIndex(
                name: "IX_Auctions_UId",
                table: "Auctions",
                column: "UId");

            migrationBuilder.CreateIndex(
                name: "IX_Auctions_Uuid",
                table: "Auctions",
                column: "Uuid",
                unique: true);
        }
    }
}
