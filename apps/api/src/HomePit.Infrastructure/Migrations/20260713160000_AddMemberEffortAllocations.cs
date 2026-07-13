using System;
using HomePit.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomePit.Infrastructure.Migrations
{
    [DbContext(typeof(HomePitDbContext))]
    [Migration("20260713160000_AddMemberEffortAllocations")]
    public partial class AddMemberEffortAllocations : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "member_effort_allocations",
                schema: "homepit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdMemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    UniverseId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    ScopeType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Weekday = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Points = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_member_effort_allocations", x => x.Id);
                    table.CheckConstraint("CK_member_effort_allocations_points_non_negative", "\"Points\" >= 0");
                    table.CheckConstraint(
                        "CK_member_effort_allocations_scope",
                        "(\"ScopeType\" = 'Household' AND \"UniverseId\" IS NULL AND \"ProjectId\" IS NULL) OR (\"ScopeType\" = 'Universe' AND \"UniverseId\" IS NOT NULL AND \"ProjectId\" IS NULL) OR (\"ScopeType\" = 'Project' AND \"UniverseId\" IS NULL AND \"ProjectId\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_member_effort_allocations_household_members_HouseholdMemberId",
                        column: x => x.HouseholdMemberId,
                        principalSchema: "homepit",
                        principalTable: "household_members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_member_effort_allocations_households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalSchema: "homepit",
                        principalTable: "households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_member_effort_allocations_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "homepit",
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_member_effort_allocations_universes_UniverseId",
                        column: x => x.UniverseId,
                        principalSchema: "homepit",
                        principalTable: "universes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_member_effort_allocations_HouseholdMemberId_Weekday",
                schema: "homepit",
                table: "member_effort_allocations",
                columns: new[] { "HouseholdMemberId", "Weekday" },
                unique: true,
                filter: "\"ScopeType\" = 'Household'");
            migrationBuilder.CreateIndex(
                name: "IX_member_effort_allocations_HouseholdMemberId_UniverseId_Weekday",
                schema: "homepit",
                table: "member_effort_allocations",
                columns: new[] { "HouseholdMemberId", "UniverseId", "Weekday" },
                unique: true,
                filter: "\"ScopeType\" = 'Universe'");
            migrationBuilder.CreateIndex(
                name: "IX_member_effort_allocations_HouseholdMemberId_ProjectId_Weekday",
                schema: "homepit",
                table: "member_effort_allocations",
                columns: new[] { "HouseholdMemberId", "ProjectId", "Weekday" },
                unique: true,
                filter: "\"ScopeType\" = 'Project'");
            migrationBuilder.CreateIndex(
                name: "IX_member_effort_allocations_HouseholdId",
                schema: "homepit",
                table: "member_effort_allocations",
                column: "HouseholdId");
            migrationBuilder.CreateIndex(
                name: "IX_member_effort_allocations_ProjectId",
                schema: "homepit",
                table: "member_effort_allocations",
                column: "ProjectId");
            migrationBuilder.CreateIndex(
                name: "IX_member_effort_allocations_UniverseId",
                schema: "homepit",
                table: "member_effort_allocations",
                column: "UniverseId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "member_effort_allocations", schema: "homepit");
        }
    }
}
