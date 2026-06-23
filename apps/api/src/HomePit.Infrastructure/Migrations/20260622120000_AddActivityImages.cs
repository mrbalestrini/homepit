using System;
using HomePit.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomePit.Infrastructure.Migrations;

[DbContext(typeof(HomePitDbContext))]
[Migration("20260622120000_AddActivityImages")]
public partial class AddActivityImages : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "ImageContentType",
            schema: "homepit",
            table: "activities",
            type: "character varying(120)",
            maxLength: 120,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ImageObjectKey",
            schema: "homepit",
            table: "activities",
            type: "character varying(512)",
            maxLength: 512,
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "ImageUpdatedAt",
            schema: "homepit",
            table: "activities",
            type: "timestamp with time zone",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "ImageContentType",
            schema: "homepit",
            table: "activities");

        migrationBuilder.DropColumn(
            name: "ImageObjectKey",
            schema: "homepit",
            table: "activities");

        migrationBuilder.DropColumn(
            name: "ImageUpdatedAt",
            schema: "homepit",
            table: "activities");
    }
}
