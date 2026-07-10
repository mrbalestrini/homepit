using System;
using HomePit.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomePit.Infrastructure.Migrations;

[DbContext(typeof(HomePitDbContext))]
[Migration("20260710193000_RefactorPlanTotals")]
public partial class RefactorPlanTotals : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.RenameColumn(
            name: "MaxUniversesPerHousehold",
            schema: "homepit",
            table: "plan_definitions",
            newName: "MaxUniverses");

        migrationBuilder.RenameColumn(
            name: "MaxProjectsPerUniverse",
            schema: "homepit",
            table: "plan_definitions",
            newName: "MaxProjects");

        migrationBuilder.AddColumn<int>(
            name: "MaxInvitedMembers",
            schema: "homepit",
            table: "plan_definitions",
            type: "integer",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "MaxInvitedMembers",
            schema: "homepit",
            table: "plan_definitions");

        migrationBuilder.RenameColumn(
            name: "MaxUniverses",
            schema: "homepit",
            table: "plan_definitions",
            newName: "MaxUniversesPerHousehold");

        migrationBuilder.RenameColumn(
            name: "MaxProjects",
            schema: "homepit",
            table: "plan_definitions",
            newName: "MaxProjectsPerUniverse");
    }
}
