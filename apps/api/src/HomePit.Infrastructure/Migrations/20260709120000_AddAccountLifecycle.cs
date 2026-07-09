using System;
using HomePit.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomePit.Infrastructure.Migrations;

[DbContext(typeof(HomePitDbContext))]
[Migration("20260709120000_AddAccountLifecycle")]
public partial class AddAccountLifecycle : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "AccountState",
            schema: "homepit",
            table: "users",
            type: "character varying(40)",
            maxLength: 40,
            nullable: false,
            defaultValue: "Active");

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "DeactivatedAt",
            schema: "homepit",
            table: "users",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "DeactivatedByUserId",
            schema: "homepit",
            table: "users",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "ScheduledDeletionAt",
            schema: "homepit",
            table: "users",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_users_DeactivatedByUserId",
            schema: "homepit",
            table: "users",
            column: "DeactivatedByUserId");

        migrationBuilder.AddForeignKey(
            name: "FK_users_users_DeactivatedByUserId",
            schema: "homepit",
            table: "users",
            column: "DeactivatedByUserId",
            principalSchema: "homepit",
            principalTable: "users",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_users_users_DeactivatedByUserId",
            schema: "homepit",
            table: "users");

        migrationBuilder.DropIndex(
            name: "IX_users_DeactivatedByUserId",
            schema: "homepit",
            table: "users");

        migrationBuilder.DropColumn(
            name: "AccountState",
            schema: "homepit",
            table: "users");

        migrationBuilder.DropColumn(
            name: "DeactivatedAt",
            schema: "homepit",
            table: "users");

        migrationBuilder.DropColumn(
            name: "DeactivatedByUserId",
            schema: "homepit",
            table: "users");

        migrationBuilder.DropColumn(
            name: "ScheduledDeletionAt",
            schema: "homepit",
            table: "users");
    }
}
