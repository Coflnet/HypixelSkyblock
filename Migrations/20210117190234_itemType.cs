using Microsoft.EntityFrameworkCore.Migrations;

namespace Coflnet.Sky.Core.Migrations
{
    public partial class itemType : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaymentSessionId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "SessionId",
                table: "Users");

            migrationBuilder.AlterColumn<string>(
                name: "GoogleId",
                table: "Users",
                maxLength: 32,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Enchantable",
                table: "Items",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsBazaar",
                table: "Items",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "Reforgeable",
                table: "Items",
                nullable: false,
                defaultValue: false); 

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Device",
                maxLength: 40,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConnectionId",
                table: "Device",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OccuredTimes",
                table: "AltItemNames",
                nullable: false,
                defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_GoogleId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Device_ConnectionId",
                table: "Device");

            migrationBuilder.DropColumn(
                name: "Enchantable",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "IsBazaar",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "Reforgeable",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "ConnectionId",
                table: "Device");

            migrationBuilder.DropColumn(
                name: "OccuredTimes",
                table: "AltItemNames");

            migrationBuilder.AlterColumn<string>(
                name: "GoogleId",
                table: "Users",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldMaxLength: 32,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentSessionId",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SessionId",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Device",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldMaxLength: 40,
                oldNullable: true);
        }
    }
}
