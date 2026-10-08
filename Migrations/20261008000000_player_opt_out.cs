using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Coflnet.Sky.Core.Migrations;

[DbContext(typeof(HypixelContext))]
[Migration("20261008000000_player_opt_out")]
public partial class playeroptout : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // idempotent: the table may already exist from before it moved into the core library
        migrationBuilder.Sql(PlayerOptOut.CreateTableSql);
        migrationBuilder.Sql(PlayerOptOut.SeedLegacySql);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "PlayerOptOutRequests");
    }
}
