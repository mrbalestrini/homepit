using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomePit.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHouseholdCommercialOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                schema: "homepit",
                table: "households",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE homepit.households AS household
                SET "CreatedByUserId" = COALESCE(
                    (
                        SELECT member."UserId"
                        FROM homepit.household_members AS member
                        WHERE member."HouseholdId" = household."Id"
                            AND member."IsActive"
                            AND member."Role" = 'Owner'
                        ORDER BY member."CreatedAt", member."Id"
                        LIMIT 1
                    ),
                    (
                        SELECT member."UserId"
                        FROM homepit.household_members AS member
                        WHERE member."HouseholdId" = household."Id"
                            AND member."IsActive"
                        ORDER BY member."CreatedAt", member."Id"
                        LIMIT 1
                    ),
                    (
                        SELECT app_user."Id"
                        FROM homepit.users AS app_user
                        WHERE app_user."IsActive"
                            AND app_user."SystemRole" <> 'SuperAdmin'
                        ORDER BY app_user."CreatedAt", app_user."Id"
                        LIMIT 1
                    ),
                    (
                        SELECT app_user."Id"
                        FROM homepit.users AS app_user
                        ORDER BY app_user."CreatedAt", app_user."Id"
                        LIMIT 1
                    )
                )
                WHERE household."CreatedByUserId" IS NULL;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "CreatedByUserId",
                schema: "homepit",
                table: "households",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_households_CreatedByUserId",
                schema: "homepit",
                table: "households",
                column: "CreatedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_households_users_CreatedByUserId",
                schema: "homepit",
                table: "households",
                column: "CreatedByUserId",
                principalSchema: "homepit",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_households_users_CreatedByUserId",
                schema: "homepit",
                table: "households");

            migrationBuilder.DropIndex(
                name: "IX_households_CreatedByUserId",
                schema: "homepit",
                table: "households");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                schema: "homepit",
                table: "households");
        }
    }
}
