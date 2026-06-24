using System;
using HomePit.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomePit.Infrastructure.Migrations;

[DbContext(typeof(HomePitDbContext))]
[Migration("20260624130000_AddGsmRechargeHistoryAndDaysWithoutRecharge")]
public partial class AddGsmRechargeHistoryAndDaysWithoutRecharge : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "DaysWithoutRecharge",
            schema: "homepit",
            table: "gsm_numbers",
            type: "integer",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "gsm_recharges",
            schema: "homepit",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                GsmNumberId = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedByMemberId = table.Column<Guid>(type: "uuid", nullable: true),
                RechargedOn = table.Column<DateOnly>(type: "date", nullable: false),
                Amount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                Note = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_gsm_recharges", item => item.Id);
                table.ForeignKey(
                    name: "FK_gsm_recharges_gsm_numbers_GsmNumberId",
                    column: item => item.GsmNumberId,
                    principalSchema: "homepit",
                    principalTable: "gsm_numbers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_gsm_recharges_household_members_CreatedByMemberId",
                    column: item => item.CreatedByMemberId,
                    principalSchema: "homepit",
                    principalTable: "household_members",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "FK_gsm_recharges_households_HouseholdId",
                    column: item => item.HouseholdId,
                    principalSchema: "homepit",
                    principalTable: "households",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_gsm_recharges_CreatedByMemberId",
            schema: "homepit",
            table: "gsm_recharges",
            column: "CreatedByMemberId");

        migrationBuilder.CreateIndex(
            name: "IX_gsm_recharges_GsmNumberId",
            schema: "homepit",
            table: "gsm_recharges",
            column: "GsmNumberId");

        migrationBuilder.CreateIndex(
            name: "IX_gsm_recharges_HouseholdId_GsmNumberId_RechargedOn",
            schema: "homepit",
            table: "gsm_recharges",
            columns: new[] { "HouseholdId", "GsmNumberId", "RechargedOn" });

        migrationBuilder.Sql("""
            INSERT INTO homepit.gsm_recharges (
                "Id",
                "HouseholdId",
                "GsmNumberId",
                "CreatedByMemberId",
                "RechargedOn",
                "Amount",
                "Note",
                "CreatedAt",
                "UpdatedAt")
            SELECT
                (
                    substr(md5(gsm."Id"::text || '-gsm-recharge'), 1, 8) || '-' ||
                    substr(md5(gsm."Id"::text || '-gsm-recharge'), 9, 4) || '-' ||
                    substr(md5(gsm."Id"::text || '-gsm-recharge'), 13, 4) || '-' ||
                    substr(md5(gsm."Id"::text || '-gsm-recharge'), 17, 4) || '-' ||
                    substr(md5(gsm."Id"::text || '-gsm-recharge'), 21, 12)
                )::uuid,
                gsm."HouseholdId",
                gsm."Id",
                gsm."CreatedByMemberId",
                gsm."LastRechargeOn",
                NULL,
                NULL,
                NOW(),
                NOW()
            FROM homepit.gsm_numbers AS gsm
            WHERE gsm."LastRechargeOn" IS NOT NULL;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DELETE FROM homepit.gsm_recharges;
            """);

        migrationBuilder.DropTable(
            name: "gsm_recharges",
            schema: "homepit");

        migrationBuilder.DropColumn(
            name: "DaysWithoutRecharge",
            schema: "homepit",
            table: "gsm_numbers");
    }
}
