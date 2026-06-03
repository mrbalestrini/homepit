using System;
using HomePit.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomePit.Infrastructure.Migrations;

[DbContext(typeof(HomePitDbContext))]
[Migration("20260601161000_AddUserProfilePhoto")]
public partial class AddUserProfilePhoto : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "ProfilePhotoObjectKey",
            schema: "homepit",
            table: "users",
            type: "character varying(512)",
            maxLength: 512,
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "ProfilePhotoUpdatedAt",
            schema: "homepit",
            table: "users",
            type: "timestamp with time zone",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "ProfilePhotoObjectKey",
            schema: "homepit",
            table: "users");

        migrationBuilder.DropColumn(
            name: "ProfilePhotoUpdatedAt",
            schema: "homepit",
            table: "users");
    }
}
