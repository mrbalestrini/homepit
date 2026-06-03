using HomePit.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomePit.Infrastructure.Migrations;

[DbContext(typeof(HomePitDbContext))]
[Migration("20260603152000_AddPromptBankModule")]
public partial class AddPromptBankModule : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "prompt_categories",
            schema: "homepit",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedByMemberId = table.Column<Guid>(type: "uuid", nullable: true),
                Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_prompt_categories", item => item.Id);
                table.ForeignKey(
                    name: "FK_prompt_categories_household_members_CreatedByMemberId",
                    column: item => item.CreatedByMemberId,
                    principalSchema: "homepit",
                    principalTable: "household_members",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "FK_prompt_categories_households_HouseholdId",
                    column: item => item.HouseholdId,
                    principalSchema: "homepit",
                    principalTable: "households",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "prompts",
            schema: "homepit",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedByMemberId = table.Column<Guid>(type: "uuid", nullable: true),
                UniverseId = table.Column<Guid>(type: "uuid", nullable: true),
                Title = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                PromptText = table.Column<string>(type: "character varying(16000)", maxLength: 16000, nullable: false),
                LinkUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                LinkTitle = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                ImageObjectKey = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                ImageContentType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                ImageUpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_prompts", item => item.Id);
                table.CheckConstraint(
                    name: "CK_prompts_link_url_title_pair",
                    sql: """
                        ("LinkUrl" IS NULL AND "LinkTitle" IS NULL)
                        OR
                        ("LinkUrl" IS NOT NULL AND "LinkTitle" IS NOT NULL)
                        """);
                table.ForeignKey(
                    name: "FK_prompts_household_members_CreatedByMemberId",
                    column: item => item.CreatedByMemberId,
                    principalSchema: "homepit",
                    principalTable: "household_members",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "FK_prompts_households_HouseholdId",
                    column: item => item.HouseholdId,
                    principalSchema: "homepit",
                    principalTable: "households",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_prompts_universes_UniverseId",
                    column: item => item.UniverseId,
                    principalSchema: "homepit",
                    principalTable: "universes",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateTable(
            name: "prompt_category_assignments",
            schema: "homepit",
            columns: table => new
            {
                PromptId = table.Column<Guid>(type: "uuid", nullable: false),
                CategoryId = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_prompt_category_assignments", item => new { item.PromptId, item.CategoryId });
                table.ForeignKey(
                    name: "FK_prompt_category_assignments_prompt_categories_CategoryId",
                    column: item => item.CategoryId,
                    principalSchema: "homepit",
                    principalTable: "prompt_categories",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_prompt_category_assignments_prompts_PromptId",
                    column: item => item.PromptId,
                    principalSchema: "homepit",
                    principalTable: "prompts",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_prompt_categories_CreatedByMemberId",
            schema: "homepit",
            table: "prompt_categories",
            column: "CreatedByMemberId");

        migrationBuilder.CreateIndex(
            name: "IX_prompt_categories_HouseholdId_Name",
            schema: "homepit",
            table: "prompt_categories",
            columns: new[] { "HouseholdId", "Name" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_prompt_category_assignments_CategoryId",
            schema: "homepit",
            table: "prompt_category_assignments",
            column: "CategoryId");

        migrationBuilder.CreateIndex(
            name: "IX_prompt_category_assignments_PromptId_CategoryId",
            schema: "homepit",
            table: "prompt_category_assignments",
            columns: new[] { "PromptId", "CategoryId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_prompts_CreatedByMemberId",
            schema: "homepit",
            table: "prompts",
            column: "CreatedByMemberId");

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

        migrationBuilder.CreateIndex(
            name: "IX_prompts_UniverseId",
            schema: "homepit",
            table: "prompts",
            column: "UniverseId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "prompt_category_assignments",
            schema: "homepit");

        migrationBuilder.DropTable(
            name: "prompt_categories",
            schema: "homepit");

        migrationBuilder.DropTable(
            name: "prompts",
            schema: "homepit");
    }
}
