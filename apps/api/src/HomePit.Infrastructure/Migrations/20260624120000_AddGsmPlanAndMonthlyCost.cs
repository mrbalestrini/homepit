using System;
using HomePit.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomePit.Infrastructure.Migrations;

[DbContext(typeof(HomePitDbContext))]
[Migration("20260624120000_AddGsmPlanAndMonthlyCost")]
public partial class AddGsmPlanAndMonthlyCost : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Plan",
            schema: "homepit",
            table: "gsm_numbers",
            type: "character varying(40)",
            maxLength: 40,
            nullable: false,
            defaultValue: "PrePago");

        migrationBuilder.AddColumn<decimal>(
            name: "MonthlyCost",
            schema: "homepit",
            table: "gsm_numbers",
            type: "numeric(10,2)",
            precision: 10,
            scale: 2,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "MonthlyCost",
            schema: "homepit",
            table: "gsm_numbers");

        migrationBuilder.DropColumn(
            name: "Plan",
            schema: "homepit",
            table: "gsm_numbers");
    }
}
