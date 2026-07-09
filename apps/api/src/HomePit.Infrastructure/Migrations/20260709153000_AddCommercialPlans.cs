using System;
using HomePit.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomePit.Infrastructure.Migrations;

[DbContext(typeof(HomePitDbContext))]
[Migration("20260709153000_AddCommercialPlans")]
public partial class AddCommercialPlans : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "plan_definitions",
            schema: "homepit",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Slug = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                MonthlyPrice = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                AnnualPrice = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                MaxOwnedHouseholds = table.Column<int>(type: "integer", nullable: false),
                MaxUniversesPerHousehold = table.Column<int>(type: "integer", nullable: false),
                MaxProjectsPerUniverse = table.Column<int>(type: "integer", nullable: false),
                MaxOriginalImages = table.Column<int>(type: "integer", nullable: false),
                SortOrder = table.Column<int>(type: "integer", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_plan_definitions", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "user_plan_image_assets",
            schema: "homepit",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                Module = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                ObjectKey = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                ContentType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                UploadedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                IsDegraded = table.Column<bool>(type: "boolean", nullable: false),
                DegradedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_user_plan_image_assets", x => x.Id);
                table.ForeignKey(
                    name: "FK_user_plan_image_assets_users_UserId",
                    column: x => x.UserId,
                    principalSchema: "homepit",
                    principalTable: "users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "user_subscriptions",
            schema: "homepit",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                PlanDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                BillingCycle = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                StartsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                EndsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                AmountPaid = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                AdminNote = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_user_subscriptions", x => x.Id);
                table.ForeignKey(
                    name: "FK_user_subscriptions_plan_definitions_PlanDefinitionId",
                    column: x => x.PlanDefinitionId,
                    principalSchema: "homepit",
                    principalTable: "plan_definitions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_user_subscriptions_users_UserId",
                    column: x => x.UserId,
                    principalSchema: "homepit",
                    principalTable: "users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_plan_definitions_Slug",
            schema: "homepit",
            table: "plan_definitions",
            column: "Slug",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_user_plan_image_assets_Module_EntityId",
            schema: "homepit",
            table: "user_plan_image_assets",
            columns: new[] { "Module", "EntityId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_user_plan_image_assets_UserId_UploadedAt",
            schema: "homepit",
            table: "user_plan_image_assets",
            columns: new[] { "UserId", "UploadedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_user_subscriptions_PlanDefinitionId",
            schema: "homepit",
            table: "user_subscriptions",
            column: "PlanDefinitionId");

        migrationBuilder.CreateIndex(
            name: "IX_user_subscriptions_UserId_EndsAt",
            schema: "homepit",
            table: "user_subscriptions",
            columns: new[] { "UserId", "EndsAt" });

        migrationBuilder.CreateIndex(
            name: "IX_user_subscriptions_UserId_StartsAt",
            schema: "homepit",
            table: "user_subscriptions",
            columns: new[] { "UserId", "StartsAt" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "user_plan_image_assets",
            schema: "homepit");

        migrationBuilder.DropTable(
            name: "user_subscriptions",
            schema: "homepit");

        migrationBuilder.DropTable(
            name: "plan_definitions",
            schema: "homepit");
    }
}
