using System;
using HomePit.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomePit.Infrastructure.Migrations
{
    [DbContext(typeof(HomePitDbContext))]
    [Migration("20260713170000_AddHouseholdInvitations")]
    public partial class AddHouseholdInvitations : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "household_invitations",
                schema: "homepit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    InviterUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    InviteeEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    Role = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    InvitedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RespondedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_household_invitations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_household_invitations_households_HouseholdId",
                        column: x => x.HouseholdId,
                        principalSchema: "homepit",
                        principalTable: "households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_household_invitations_users_InviterUserId",
                        column: x => x.InviterUserId,
                        principalSchema: "homepit",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_household_invitations_HouseholdId_InviteeEmail",
                schema: "homepit",
                table: "household_invitations",
                columns: new[] { "HouseholdId", "InviteeEmail" },
                unique: true);
            migrationBuilder.CreateIndex(
                name: "IX_household_invitations_InviteeEmail_Status",
                schema: "homepit",
                table: "household_invitations",
                columns: new[] { "InviteeEmail", "Status" });
            migrationBuilder.CreateIndex(
                name: "IX_household_invitations_InviterUserId",
                schema: "homepit",
                table: "household_invitations",
                column: "InviterUserId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "household_invitations", schema: "homepit");
        }
    }
}
