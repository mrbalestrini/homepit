using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomePit.Infrastructure.Migrations;

public partial class AddInstitutionalSeoImage : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "SeoImageContentType",
            schema: "homepit",
            table: "institutional_pages",
            type: "character varying(120)",
            maxLength: 120,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "SeoImageObjectKey",
            schema: "homepit",
            table: "institutional_pages",
            type: "character varying(512)",
            maxLength: 512,
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "SeoImageUpdatedAt",
            schema: "homepit",
            table: "institutional_pages",
            type: "timestamp with time zone",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "SeoImageContentType",
            schema: "homepit",
            table: "institutional_pages");

        migrationBuilder.DropColumn(
            name: "SeoImageObjectKey",
            schema: "homepit",
            table: "institutional_pages");

        migrationBuilder.DropColumn(
            name: "SeoImageUpdatedAt",
            schema: "homepit",
            table: "institutional_pages");
    }
}
