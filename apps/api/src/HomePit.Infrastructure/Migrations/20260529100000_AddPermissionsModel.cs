using System;
using HomePit.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomePit.Infrastructure.Migrations;

[DbContext(typeof(HomePitDbContext))]
[Migration("20260529100000_AddPermissionsModel")]
public partial class AddPermissionsModel : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "SystemRole",
            schema: "homepit",
            table: "users",
            type: "character varying(40)",
            maxLength: 40,
            nullable: false,
            defaultValue: "User");

        migrationBuilder.AddColumn<Guid>(
            name: "CreatedByMemberId",
            schema: "homepit",
            table: "universes",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "CreatedByMemberId",
            schema: "homepit",
            table: "projects",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "CreatedByMemberId",
            schema: "homepit",
            table: "activities",
            type: "uuid",
            nullable: true);

        migrationBuilder.Sql("""
            UPDATE homepit.users
            SET "SystemRole" = 'Admin'
            WHERE "Id" = (
                SELECT "Id"
                FROM homepit.users
                ORDER BY "CreatedAt", "Id"
                LIMIT 1
            );
            """);

        migrationBuilder.Sql("""
            UPDATE homepit.universes AS universe
            SET "CreatedByMemberId" = (
                SELECT member."Id"
                FROM homepit.household_members AS member
                WHERE member."HouseholdId" = universe."HouseholdId"
                    AND member."IsActive"
                ORDER BY
                    CASE member."Role"
                        WHEN 'Owner' THEN 0
                        WHEN 'Admin' THEN 1
                        ELSE 2
                    END,
                    member."CreatedAt",
                    member."Id"
                LIMIT 1
            )
            WHERE universe."CreatedByMemberId" IS NULL;
            """);

        migrationBuilder.Sql("""
            UPDATE homepit.projects AS project
            SET "CreatedByMemberId" = (
                SELECT member."Id"
                FROM homepit.household_members AS member
                WHERE member."HouseholdId" = project."HouseholdId"
                    AND member."IsActive"
                ORDER BY
                    CASE member."Role"
                        WHEN 'Owner' THEN 0
                        WHEN 'Admin' THEN 1
                        ELSE 2
                    END,
                    member."CreatedAt",
                    member."Id"
                LIMIT 1
            )
            WHERE project."CreatedByMemberId" IS NULL;
            """);

        migrationBuilder.Sql("""
            UPDATE homepit.activities AS activity
            SET "CreatedByMemberId" = (
                SELECT member."Id"
                FROM homepit.household_members AS member
                WHERE member."HouseholdId" = activity."HouseholdId"
                    AND member."IsActive"
                ORDER BY
                    CASE member."Role"
                        WHEN 'Owner' THEN 0
                        WHEN 'Admin' THEN 1
                        ELSE 2
                    END,
                    member."CreatedAt",
                    member."Id"
                LIMIT 1
            )
            WHERE activity."CreatedByMemberId" IS NULL;
            """);

        migrationBuilder.CreateIndex(
            name: "IX_activities_CreatedByMemberId",
            schema: "homepit",
            table: "activities",
            column: "CreatedByMemberId");

        migrationBuilder.CreateIndex(
            name: "IX_projects_CreatedByMemberId",
            schema: "homepit",
            table: "projects",
            column: "CreatedByMemberId");

        migrationBuilder.CreateIndex(
            name: "IX_universes_CreatedByMemberId",
            schema: "homepit",
            table: "universes",
            column: "CreatedByMemberId");

        migrationBuilder.AddForeignKey(
            name: "FK_activities_household_members_CreatedByMemberId",
            schema: "homepit",
            table: "activities",
            column: "CreatedByMemberId",
            principalSchema: "homepit",
            principalTable: "household_members",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);

        migrationBuilder.AddForeignKey(
            name: "FK_projects_household_members_CreatedByMemberId",
            schema: "homepit",
            table: "projects",
            column: "CreatedByMemberId",
            principalSchema: "homepit",
            principalTable: "household_members",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);

        migrationBuilder.AddForeignKey(
            name: "FK_universes_household_members_CreatedByMemberId",
            schema: "homepit",
            table: "universes",
            column: "CreatedByMemberId",
            principalSchema: "homepit",
            principalTable: "household_members",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_activities_household_members_CreatedByMemberId",
            schema: "homepit",
            table: "activities");

        migrationBuilder.DropForeignKey(
            name: "FK_projects_household_members_CreatedByMemberId",
            schema: "homepit",
            table: "projects");

        migrationBuilder.DropForeignKey(
            name: "FK_universes_household_members_CreatedByMemberId",
            schema: "homepit",
            table: "universes");

        migrationBuilder.DropIndex(
            name: "IX_activities_CreatedByMemberId",
            schema: "homepit",
            table: "activities");

        migrationBuilder.DropIndex(
            name: "IX_projects_CreatedByMemberId",
            schema: "homepit",
            table: "projects");

        migrationBuilder.DropIndex(
            name: "IX_universes_CreatedByMemberId",
            schema: "homepit",
            table: "universes");

        migrationBuilder.DropColumn(
            name: "CreatedByMemberId",
            schema: "homepit",
            table: "activities");

        migrationBuilder.DropColumn(
            name: "CreatedByMemberId",
            schema: "homepit",
            table: "projects");

        migrationBuilder.DropColumn(
            name: "CreatedByMemberId",
            schema: "homepit",
            table: "universes");

        migrationBuilder.DropColumn(
            name: "SystemRole",
            schema: "homepit",
            table: "users");
    }
}
