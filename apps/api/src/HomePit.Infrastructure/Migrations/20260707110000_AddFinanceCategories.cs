using System;
using HomePit.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomePit.Infrastructure.Migrations;

[DbContext(typeof(HomePitDbContext))]
[Migration("20260707110000_AddFinanceCategories")]
public partial class AddFinanceCategories : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "finance_categories",
            schema: "homepit",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedByMemberId = table.Column<Guid>(type: "uuid", nullable: true),
                Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                IsDefault = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                SortOrder = table.Column<int>(type: "integer", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_finance_categories", item => item.Id);
                table.ForeignKey(
                    name: "FK_finance_categories_household_members_CreatedByMemberId",
                    column: item => item.CreatedByMemberId,
                    principalSchema: "homepit",
                    principalTable: "household_members",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "FK_finance_categories_households_HouseholdId",
                    column: item => item.HouseholdId,
                    principalSchema: "homepit",
                    principalTable: "households",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.AddColumn<Guid>(
            name: "CategoryId",
            schema: "homepit",
            table: "finance_recurring_templates",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "CategoryId",
            schema: "homepit",
            table: "finance_entries",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "CategoryId",
            schema: "homepit",
            table: "credit_card_transactions",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_credit_card_transactions_CategoryId",
            schema: "homepit",
            table: "credit_card_transactions",
            column: "CategoryId");

        migrationBuilder.CreateIndex(
            name: "IX_finance_categories_CreatedByMemberId",
            schema: "homepit",
            table: "finance_categories",
            column: "CreatedByMemberId");

        migrationBuilder.CreateIndex(
            name: "IX_finance_categories_HouseholdId_Name",
            schema: "homepit",
            table: "finance_categories",
            columns: new[] { "HouseholdId", "Name" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_finance_categories_HouseholdId_SortOrder",
            schema: "homepit",
            table: "finance_categories",
            columns: new[] { "HouseholdId", "SortOrder" });

        migrationBuilder.CreateIndex(
            name: "IX_finance_entries_CategoryId",
            schema: "homepit",
            table: "finance_entries",
            column: "CategoryId");

        migrationBuilder.CreateIndex(
            name: "IX_finance_recurring_templates_CategoryId",
            schema: "homepit",
            table: "finance_recurring_templates",
            column: "CategoryId");

        migrationBuilder.AddForeignKey(
            name: "FK_credit_card_transactions_finance_categories_CategoryId",
            schema: "homepit",
            table: "credit_card_transactions",
            column: "CategoryId",
            principalSchema: "homepit",
            principalTable: "finance_categories",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);

        migrationBuilder.AddForeignKey(
            name: "FK_finance_entries_finance_categories_CategoryId",
            schema: "homepit",
            table: "finance_entries",
            column: "CategoryId",
            principalSchema: "homepit",
            principalTable: "finance_categories",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);

        migrationBuilder.AddForeignKey(
            name: "FK_finance_recurring_templates_finance_categories_CategoryId",
            schema: "homepit",
            table: "finance_recurring_templates",
            column: "CategoryId",
            principalSchema: "homepit",
            principalTable: "finance_categories",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);

        migrationBuilder.Sql("""
            INSERT INTO homepit.finance_categories (
                "Id",
                "HouseholdId",
                "CreatedByMemberId",
                "Name",
                "IsDefault",
                "SortOrder",
                "CreatedAt",
                "UpdatedAt")
            SELECT
                (
                    substr(md5(h."Id"::text || '-finance-category-' || defaults.sort_order::text), 1, 8) || '-' ||
                    substr(md5(h."Id"::text || '-finance-category-' || defaults.sort_order::text), 9, 4) || '-' ||
                    substr(md5(h."Id"::text || '-finance-category-' || defaults.sort_order::text), 13, 4) || '-' ||
                    substr(md5(h."Id"::text || '-finance-category-' || defaults.sort_order::text), 17, 4) || '-' ||
                    substr(md5(h."Id"::text || '-finance-category-' || defaults.sort_order::text), 21, 12)
                )::uuid,
                h."Id",
                NULL,
                defaults.name,
                TRUE,
                defaults.sort_order,
                NOW(),
                NOW()
            FROM homepit.households AS h
            CROSS JOIN (
                VALUES
                    (0, 'Salário'),
                    (1, 'Casa'),
                    (2, 'Mercado'),
                    (3, 'Refeição'),
                    (4, 'Saúde'),
                    (5, 'Filhos'),
                    (6, 'Carro'),
                    (7, 'Locomoção'),
                    (8, 'Investimentos'),
                    (9, 'Igreja'),
                    (10, 'Lazer'),
                    (11, 'Compras não essenciais')
            ) AS defaults(sort_order, name);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_credit_card_transactions_finance_categories_CategoryId",
            schema: "homepit",
            table: "credit_card_transactions");

        migrationBuilder.DropForeignKey(
            name: "FK_finance_entries_finance_categories_CategoryId",
            schema: "homepit",
            table: "finance_entries");

        migrationBuilder.DropForeignKey(
            name: "FK_finance_recurring_templates_finance_categories_CategoryId",
            schema: "homepit",
            table: "finance_recurring_templates");

        migrationBuilder.DropTable(
            name: "finance_categories",
            schema: "homepit");

        migrationBuilder.DropIndex(
            name: "IX_credit_card_transactions_CategoryId",
            schema: "homepit",
            table: "credit_card_transactions");

        migrationBuilder.DropIndex(
            name: "IX_finance_entries_CategoryId",
            schema: "homepit",
            table: "finance_entries");

        migrationBuilder.DropIndex(
            name: "IX_finance_recurring_templates_CategoryId",
            schema: "homepit",
            table: "finance_recurring_templates");

        migrationBuilder.DropColumn(
            name: "CategoryId",
            schema: "homepit",
            table: "credit_card_transactions");

        migrationBuilder.DropColumn(
            name: "CategoryId",
            schema: "homepit",
            table: "finance_entries");

        migrationBuilder.DropColumn(
            name: "CategoryId",
            schema: "homepit",
            table: "finance_recurring_templates");
    }
}
