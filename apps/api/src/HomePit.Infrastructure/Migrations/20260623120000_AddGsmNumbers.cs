using System;
using HomePit.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomePit.Infrastructure.Migrations;

[DbContext(typeof(HomePitDbContext))]
[Migration("20260623120000_AddGsmNumbers")]
public partial class AddGsmNumbers : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "gsm_numbers",
            schema: "homepit",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedByMemberId = table.Column<Guid>(type: "uuid", nullable: true),
                Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                NormalizedNumber = table.Column<string>(type: "character varying(13)", maxLength: 13, nullable: false),
                Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                AcquiredOn = table.Column<DateOnly>(type: "date", nullable: false),
                LastRechargeOn = table.Column<DateOnly>(type: "date", nullable: true),
                Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_gsm_numbers", item => item.Id);
                table.ForeignKey(
                    name: "FK_gsm_numbers_household_members_CreatedByMemberId",
                    column: item => item.CreatedByMemberId,
                    principalSchema: "homepit",
                    principalTable: "household_members",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "FK_gsm_numbers_households_HouseholdId",
                    column: item => item.HouseholdId,
                    principalSchema: "homepit",
                    principalTable: "households",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_gsm_numbers_CreatedByMemberId",
            schema: "homepit",
            table: "gsm_numbers",
            column: "CreatedByMemberId");

        migrationBuilder.CreateIndex(
            name: "IX_gsm_numbers_HouseholdId_NormalizedNumber",
            schema: "homepit",
            table: "gsm_numbers",
            columns: new[] { "HouseholdId", "NormalizedNumber" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "gsm_numbers",
            schema: "homepit");
    }
}
