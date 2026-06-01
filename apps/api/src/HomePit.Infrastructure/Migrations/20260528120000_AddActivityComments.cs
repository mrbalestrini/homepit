using System;
using HomePit.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomePit.Infrastructure.Migrations;

[DbContext(typeof(HomePitDbContext))]
[Migration("20260528120000_AddActivityComments")]
public partial class AddActivityComments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "activity_comments",
            schema: "homepit",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                ActivityId = table.Column<Guid>(type: "uuid", nullable: false),
                AuthorMemberId = table.Column<Guid>(type: "uuid", nullable: false),
                Body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_activity_comments", item => item.Id);
                table.ForeignKey(
                    name: "FK_activity_comments_activities_ActivityId",
                    column: item => item.ActivityId,
                    principalSchema: "homepit",
                    principalTable: "activities",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_activity_comments_household_members_AuthorMemberId",
                    column: item => item.AuthorMemberId,
                    principalSchema: "homepit",
                    principalTable: "household_members",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_activity_comments_AuthorMemberId",
            schema: "homepit",
            table: "activity_comments",
            column: "AuthorMemberId");

        migrationBuilder.CreateIndex(
            name: "IX_activity_comments_ActivityId",
            schema: "homepit",
            table: "activity_comments",
            column: "ActivityId");

        migrationBuilder.CreateIndex(
            name: "IX_activity_comments_HouseholdId_ActivityId_CreatedAt",
            schema: "homepit",
            table: "activity_comments",
            columns: new[] { "HouseholdId", "ActivityId", "CreatedAt" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "activity_comments",
            schema: "homepit");
    }
}
