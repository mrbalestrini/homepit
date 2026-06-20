using HomePit.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomePit.Infrastructure.Migrations;

[DbContext(typeof(HomePitDbContext))]
[Migration("20260620120000_AddActivityDueDate")]
public partial class AddActivityDueDate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateOnly>(
            name: "DueDate",
            schema: "homepit",
            table: "activities",
            type: "date",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "DueDate",
            schema: "homepit",
            table: "activities");
    }
}
