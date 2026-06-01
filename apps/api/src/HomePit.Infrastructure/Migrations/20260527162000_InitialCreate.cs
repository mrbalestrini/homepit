using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomePit.Infrastructure.Migrations;

public partial class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(name: "homepit");

        migrationBuilder.CreateTable(
            name: "households",
            schema: "homepit",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_households", item => item.Id));

        migrationBuilder.CreateTable(
            name: "users",
            schema: "homepit",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                PasswordHash = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                DisplayName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                PhoneNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_users", item => item.Id));

        migrationBuilder.CreateTable(
            name: "household_members",
            schema: "homepit",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                Role = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_household_members", item => item.Id);
                table.ForeignKey("FK_household_members_households_HouseholdId", item => item.HouseholdId, principalSchema: "homepit", principalTable: "households", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
                table.ForeignKey("FK_household_members_users_UserId", item => item.UserId, principalSchema: "homepit", principalTable: "users", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "refresh_tokens",
            schema: "homepit",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                TokenHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_refresh_tokens", item => item.Id);
                table.ForeignKey("FK_refresh_tokens_users_UserId", item => item.UserId, principalSchema: "homepit", principalTable: "users", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "universes",
            schema: "homepit",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_universes", item => item.Id);
                table.ForeignKey("FK_universes_households_HouseholdId", item => item.HouseholdId, principalSchema: "homepit", principalTable: "households", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "notification_preferences",
            schema: "homepit",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                HouseholdMemberId = table.Column<Guid>(type: "uuid", nullable: false),
                DailyDigestEnabled = table.Column<bool>(type: "boolean", nullable: false),
                WhatsAppPhoneNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                DailyDigestTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                TimeZoneId = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_notification_preferences", item => item.Id);
                table.ForeignKey("FK_notification_preferences_household_members_HouseholdMemberId", item => item.HouseholdMemberId, principalSchema: "homepit", principalTable: "household_members", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
                table.ForeignKey("FK_notification_preferences_households_HouseholdId", item => item.HouseholdId, principalSchema: "homepit", principalTable: "households", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "notification_runs",
            schema: "homepit",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                HouseholdMemberId = table.Column<Guid>(type: "uuid", nullable: false),
                Kind = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                LocalDate = table.Column<DateOnly>(type: "date", nullable: false),
                SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ProviderMessageId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_notification_runs", item => item.Id);
                table.ForeignKey("FK_notification_runs_households_HouseholdId", item => item.HouseholdId, principalSchema: "homepit", principalTable: "households", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "projects",
            schema: "homepit",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                UniverseId = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_projects", item => item.Id);
                table.ForeignKey("FK_projects_universes_UniverseId", item => item.UniverseId, principalSchema: "homepit", principalTable: "universes", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "activities",
            schema: "homepit",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                ResponsibleMemberId = table.Column<Guid>(type: "uuid", nullable: true),
                Title = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                Priority = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                Size = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_activities", item => item.Id);
                table.ForeignKey("FK_activities_household_members_ResponsibleMemberId", item => item.ResponsibleMemberId, principalSchema: "homepit", principalTable: "household_members", principalColumn: "Id", onDelete: ReferentialAction.SetNull);
                table.ForeignKey("FK_activities_projects_ProjectId", item => item.ProjectId, principalSchema: "homepit", principalTable: "projects", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "pending_items",
            schema: "homepit",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                ActivityId = table.Column<Guid>(type: "uuid", nullable: false),
                Title = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                Priority = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                SnoozeDays = table.Column<int>(type: "integer", nullable: true),
                CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_pending_items", item => item.Id);
                table.ForeignKey("FK_pending_items_activities_ActivityId", item => item.ActivityId, principalSchema: "homepit", principalTable: "activities", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex("IX_activities_ProjectId", "activities", "ProjectId", schema: "homepit");
        migrationBuilder.CreateIndex("IX_activities_ResponsibleMemberId", "activities", "ResponsibleMemberId", schema: "homepit");
        migrationBuilder.CreateIndex("IX_household_members_HouseholdId_UserId", "household_members", new[] { "HouseholdId", "UserId" }, schema: "homepit", unique: true);
        migrationBuilder.CreateIndex("IX_household_members_UserId", "household_members", "UserId", schema: "homepit");
        migrationBuilder.CreateIndex("IX_notification_preferences_HouseholdId", "notification_preferences", "HouseholdId", schema: "homepit");
        migrationBuilder.CreateIndex("IX_notification_preferences_HouseholdMemberId", "notification_preferences", "HouseholdMemberId", schema: "homepit", unique: true);
        migrationBuilder.CreateIndex("IX_notification_runs_HouseholdId_HouseholdMemberId_Kind_LocalDate", "notification_runs", new[] { "HouseholdId", "HouseholdMemberId", "Kind", "LocalDate" }, schema: "homepit", unique: true);
        migrationBuilder.CreateIndex("IX_pending_items_ActivityId", "pending_items", "ActivityId", schema: "homepit");
        migrationBuilder.CreateIndex("IX_projects_HouseholdId_UniverseId_Name", "projects", new[] { "HouseholdId", "UniverseId", "Name" }, schema: "homepit", unique: true);
        migrationBuilder.CreateIndex("IX_projects_UniverseId", "projects", "UniverseId", schema: "homepit");
        migrationBuilder.CreateIndex("IX_refresh_tokens_TokenHash", "refresh_tokens", "TokenHash", schema: "homepit", unique: true);
        migrationBuilder.CreateIndex("IX_refresh_tokens_UserId", "refresh_tokens", "UserId", schema: "homepit");
        migrationBuilder.CreateIndex("IX_universes_HouseholdId_Name", "universes", new[] { "HouseholdId", "Name" }, schema: "homepit", unique: true);
        migrationBuilder.CreateIndex("IX_users_Email", "users", "Email", schema: "homepit", unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "notification_preferences", schema: "homepit");
        migrationBuilder.DropTable(name: "notification_runs", schema: "homepit");
        migrationBuilder.DropTable(name: "pending_items", schema: "homepit");
        migrationBuilder.DropTable(name: "refresh_tokens", schema: "homepit");
        migrationBuilder.DropTable(name: "activities", schema: "homepit");
        migrationBuilder.DropTable(name: "projects", schema: "homepit");
        migrationBuilder.DropTable(name: "household_members", schema: "homepit");
        migrationBuilder.DropTable(name: "universes", schema: "homepit");
        migrationBuilder.DropTable(name: "users", schema: "homepit");
        migrationBuilder.DropTable(name: "households", schema: "homepit");
    }
}
