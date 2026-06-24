using System;
using HomePit.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomePit.Infrastructure.Migrations;

[DbContext(typeof(HomePitDbContext))]
[Migration("20260624150000_AddPromptArchiving")]
public partial class AddPromptArchiving : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsArchived",
            schema: "homepit",
            table: "prompts",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.DropIndex(
            name: "IX_prompts_HouseholdId_UpdatedAt",
            schema: "homepit",
            table: "prompts");

        migrationBuilder.DropIndex(
            name: "IX_prompts_HouseholdId_UniverseId_UpdatedAt",
            schema: "homepit",
            table: "prompts");

        migrationBuilder.CreateIndex(
            name: "IX_prompts_HouseholdId_IsArchived_UpdatedAt",
            schema: "homepit",
            table: "prompts",
            columns: new[] { "HouseholdId", "IsArchived", "UpdatedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_prompts_HouseholdId_IsArchived_UniverseId_UpdatedAt",
            schema: "homepit",
            table: "prompts",
            columns: new[] { "HouseholdId", "IsArchived", "UniverseId", "UpdatedAt" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_prompts_HouseholdId_IsArchived_UpdatedAt",
            schema: "homepit",
            table: "prompts");

        migrationBuilder.DropIndex(
            name: "IX_prompts_HouseholdId_IsArchived_UniverseId_UpdatedAt",
            schema: "homepit",
            table: "prompts");

        migrationBuilder.CreateIndex(
            name: "IX_prompts_HouseholdId_UpdatedAt",
            schema: "homepit",
            table: "prompts",
            columns: new[] { "HouseholdId", "UpdatedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_prompts_HouseholdId_UniverseId_UpdatedAt",
            schema: "homepit",
            table: "prompts",
            columns: new[] { "HouseholdId", "UniverseId", "UpdatedAt" });

        migrationBuilder.DropColumn(
            name: "IsArchived",
            schema: "homepit",
            table: "prompts");
    }
}
