using System;
using HomePit.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomePit.Infrastructure.Migrations;

[DbContext(typeof(HomePitDbContext))]
[Migration("20260714130000_AddShowInCatalogFlag")]
public partial class AddShowInCatalogFlag : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "ShowInCatalog",
            schema: "homepit",
            table: "plan_definitions",
            type: "boolean",
            nullable: false,
            defaultValue: true);

        migrationBuilder.Sql("""
            UPDATE homepit.plan_definitions
            SET "ShowInCatalog" = TRUE;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "ShowInCatalog",
            schema: "homepit",
            table: "plan_definitions");
    }
}
