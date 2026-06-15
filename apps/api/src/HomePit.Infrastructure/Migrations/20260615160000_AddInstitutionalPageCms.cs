using HomePit.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomePit.Infrastructure.Migrations;

[DbContext(typeof(HomePitDbContext))]
[Migration("20260615160000_AddInstitutionalPageCms")]
public partial class AddInstitutionalPageCms : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "institutional_pages",
            schema: "homepit",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Slug = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                SeoTitle = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                SeoDescription = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                BrandName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                BrandTagline = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                HeroEyebrow = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                HeroTitle = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                HeroDescription = table.Column<string>(type: "character varying(1200)", maxLength: 1200, nullable: false),
                PrimaryCtaLabel = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                PrimaryCtaUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                BenefitsTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                BenefitsDescription = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false),
                StepsTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                StepsDescription = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false),
                HighlightEyebrow = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                HighlightTitle = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                HighlightDescription = table.Column<string>(type: "character varying(1200)", maxLength: 1200, nullable: false),
                FinalCtaTitle = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                FinalCtaDescription = table.Column<string>(type: "character varying(1200)", maxLength: 1200, nullable: false),
                FooterText = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false),
                HeroImageAlt = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                HeroImageObjectKey = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                HeroImageContentType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                HeroImageUpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                HighlightImageAlt = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                HighlightImageObjectKey = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                HighlightImageContentType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                HighlightImageUpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_institutional_pages", item => item.Id));

        migrationBuilder.CreateTable(
            name: "institutional_benefits",
            schema: "homepit",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                InstitutionalPageId = table.Column<Guid>(type: "uuid", nullable: false),
                Position = table.Column<int>(type: "integer", nullable: false),
                Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                Description = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_institutional_benefits", item => item.Id);
                table.ForeignKey(
                    name: "FK_institutional_benefits_institutional_pages_InstitutionalPageId",
                    column: item => item.InstitutionalPageId,
                    principalSchema: "homepit",
                    principalTable: "institutional_pages",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "institutional_steps",
            schema: "homepit",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                InstitutionalPageId = table.Column<Guid>(type: "uuid", nullable: false),
                Position = table.Column<int>(type: "integer", nullable: false),
                Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                Description = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_institutional_steps", item => item.Id);
                table.ForeignKey(
                    name: "FK_institutional_steps_institutional_pages_InstitutionalPageId",
                    column: item => item.InstitutionalPageId,
                    principalSchema: "homepit",
                    principalTable: "institutional_pages",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_institutional_pages_Slug",
            schema: "homepit",
            table: "institutional_pages",
            column: "Slug",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_institutional_benefits_InstitutionalPageId_Position",
            schema: "homepit",
            table: "institutional_benefits",
            columns: new[] { "InstitutionalPageId", "Position" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_institutional_steps_InstitutionalPageId_Position",
            schema: "homepit",
            table: "institutional_steps",
            columns: new[] { "InstitutionalPageId", "Position" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "institutional_benefits", schema: "homepit");
        migrationBuilder.DropTable(name: "institutional_steps", schema: "homepit");
        migrationBuilder.DropTable(name: "institutional_pages", schema: "homepit");
    }
}
