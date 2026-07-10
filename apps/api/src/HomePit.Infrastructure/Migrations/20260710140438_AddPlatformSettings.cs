using System;
using HomePit.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomePit.Infrastructure.Migrations;

[DbContext(typeof(HomePitDbContext))]
[Migration("20260710140438_AddPlatformSettings")]
public partial class AddPlatformSettings : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "platform_settings",
            schema: "homepit",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Key = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                AdminName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                ContactEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                ContactPhone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                ManagementPhone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                Instagram = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                AddressLine1 = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                AddressLine2 = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                City = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                State = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                PostalCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_platform_settings", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_platform_settings_Key",
            schema: "homepit",
            table: "platform_settings",
            column: "Key",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "platform_settings",
            schema: "homepit");
    }
}
