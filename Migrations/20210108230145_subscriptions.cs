using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Coflnet.Sky.Core.Migrations
{
    public partial class subscriptions : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Initiator",
                table: "SubscribeItem");

            migrationBuilder.DropColumn(
                name: "ItemTag",
                table: "SubscribeItem");

            migrationBuilder.DropColumn(
                name: "PlayerUuid",
                table: "SubscribeItem");

            migrationBuilder.DropColumn(
                name: "Token",
                table: "SubscribeItem");

            migrationBuilder.AddColumn<DateTime>(
                name: "NotTriggerAgainBefore",
                table: "SubscribeItem",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "TopicId",
                table: "SubscribeItem",
                maxLength: 45,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "SubscribeItem",
                nullable: false,
                defaultValue: 0);


            migrationBuilder.CreateTable(
                name: "Device",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    UserId = table.Column<int>(nullable: false),
                    Name = table.Column<string>(nullable: true),
                    Token = table.Column<string>(nullable: true),
                    GoogleUserId = table.Column<int>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Device", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Device_Users_GoogleUserId",
                        column: x => x.GoogleUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Device_GoogleUserId",
                table: "Device",
                column: "GoogleUserId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Device");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Players",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "NotTriggerAgainBefore",
                table: "SubscribeItem");

            migrationBuilder.DropColumn(
                name: "TopicId",
                table: "SubscribeItem");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "SubscribeItem");

            migrationBuilder.AddColumn<string>(
                name: "Initiator",
                table: "SubscribeItem",
                type: "varchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ItemTag",
                table: "SubscribeItem",
                type: "varchar(45)",
                maxLength: 45,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PlayerUuid",
                table: "SubscribeItem",
                type: "char(32)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Token",
                table: "SubscribeItem",
                type: "text",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "Id",
                table: "Players",
                type: "int",
                nullable: false,
                oldClrType: typeof(int))
                .Annotation("MySQL:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn);

            migrationBuilder.AlterColumn<string>(
                name: "UuId",
                table: "Players",
                type: "char(32)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "char(32)");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Players",
                table: "Players",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_Players_UuId",
                table: "Players",
                column: "UuId");
        }
    }
}
