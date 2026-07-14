using System;
using HomePit.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomePit.Infrastructure.Migrations;

[DbContext(typeof(HomePitDbContext))]
[Migration("20260714120000_AddPopularPlanFlag")]
public partial class AddPopularPlanFlag : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsPopular",
            schema: "homepit",
            table: "plan_definitions",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.Sql("""
            UPDATE homepit.plan_definitions
            SET "IsPopular" = TRUE
            WHERE "Slug" = 'gold';
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "IsPopular",
            schema: "homepit",
            table: "plan_definitions");
    }
}
