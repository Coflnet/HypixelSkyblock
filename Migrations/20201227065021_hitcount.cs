using Microsoft.EntityFrameworkCore.Migrations;

namespace Coflnet.Sky.Core.Migrations
{
    public partial class hitcount : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "HitCount",
                table: "Players",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "HitCount",
                table: "Items",
                nullable: false,
                defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HitCount",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "HitCount",
                table: "Items");
        }
    }
}
