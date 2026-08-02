using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Coflnet.Sky.Core.Migrations;

[DbContext(typeof(HypixelContext))]
[Migration("20260728000000_terms_acceptance")]
public partial class termsacceptance : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "TermsAcceptedAtUtc",
            table: "Users",
            type: "datetime(6)",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "TermsAcceptedHash",
            table: "Users",
            type: "char(64)",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "TermsAcceptedVersion",
            table: "Users",
            type: "varchar(64)",
            maxLength: 64,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "TermsAcceptanceSource",
            table: "Users",
            type: "varchar(32)",
            maxLength: 32,
            nullable: true);

        migrationBuilder.CreateTable(
            name: "AgreementAcceptances",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                UserId = table.Column<int>(type: "int", nullable: false),
                Agreement = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                Version = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
                Hash = table.Column<string>(type: "char(64)", nullable: false),
                AcceptedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                Source = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_AgreementAcceptances", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_AgreementAcceptances_UserId_Agreement_Version_Hash",
            table: "AgreementAcceptances",
            columns: new[] { "UserId", "Agreement", "Version", "Hash" },
            unique: true);

        migrationBuilder.Sql(
            """
            INSERT INTO `AgreementAcceptances` (`UserId`, `Agreement`, `Version`, `Hash`, `AcceptedAtUtc`, `Source`)
            SELECT `Id`, 'terms', `TermsAcceptedVersion`, LOWER(`TermsAcceptedHash`), `TermsAcceptedAtUtc`, `TermsAcceptanceSource`
            FROM `Users`
            WHERE `TermsAcceptedVersion` IS NOT NULL
              AND `TermsAcceptedHash` IS NOT NULL
              AND `TermsAcceptedAtUtc` IS NOT NULL
              AND `TermsAcceptanceSource` IS NOT NULL
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "AgreementAcceptances");
        migrationBuilder.DropColumn(name: "TermsAcceptedAtUtc", table: "Users");
        migrationBuilder.DropColumn(name: "TermsAcceptedHash", table: "Users");
        migrationBuilder.DropColumn(name: "TermsAcceptedVersion", table: "Users");
        migrationBuilder.DropColumn(name: "TermsAcceptanceSource", table: "Users");
    }
}
