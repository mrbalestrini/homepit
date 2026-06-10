using System;
using HomePit.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomePit.Infrastructure.Migrations;

[DbContext(typeof(HomePitDbContext))]
[Migration("20260610120000_IncreasePromptTextLength")]
public partial class IncreasePromptTextLength : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "PromptText",
            schema: "homepit",
            table: "prompts",
            type: "character varying(20000)",
            maxLength: 20000,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(16000)",
            oldMaxLength: 16000);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "PromptText",
            schema: "homepit",
            table: "prompts",
            type: "character varying(16000)",
            maxLength: 16000,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(20000)",
            oldMaxLength: 20000);
    }
}
