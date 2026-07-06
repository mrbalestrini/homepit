using System;
using HomePit.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomePit.Infrastructure.Migrations;

[DbContext(typeof(HomePitDbContext))]
[Migration("20260706120000_AddFinanceModule")]
public partial class AddFinanceModule : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "assets",
            schema: "homepit",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedByMemberId = table.Column<Guid>(type: "uuid", nullable: true),
                Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                CurrentValue = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                RemainingDebt = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                IsPaidOff = table.Column<bool>(type: "boolean", nullable: false),
                Notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_assets", item => item.Id);
                table.ForeignKey(
                    name: "FK_assets_household_members_CreatedByMemberId",
                    column: item => item.CreatedByMemberId,
                    principalSchema: "homepit",
                    principalTable: "household_members",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "FK_assets_households_HouseholdId",
                    column: item => item.HouseholdId,
                    principalSchema: "homepit",
                    principalTable: "households",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "credit_card_accounts",
            schema: "homepit",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedByMemberId = table.Column<Guid>(type: "uuid", nullable: true),
                Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                Brand = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                LastFourDigits = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: true),
                ClosingDay = table.Column<int>(type: "integer", nullable: false),
                DueDay = table.Column<int>(type: "integer", nullable: false),
                Notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_credit_card_accounts", item => item.Id);
                table.ForeignKey(
                    name: "FK_credit_card_accounts_household_members_CreatedByMemberId",
                    column: item => item.CreatedByMemberId,
                    principalSchema: "homepit",
                    principalTable: "household_members",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "FK_credit_card_accounts_households_HouseholdId",
                    column: item => item.HouseholdId,
                    principalSchema: "homepit",
                    principalTable: "households",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "finance_periods",
            schema: "homepit",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                Year = table.Column<int>(type: "integer", nullable: false),
                Month = table.Column<int>(type: "integer", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_finance_periods", item => item.Id);
                table.ForeignKey(
                    name: "FK_finance_periods_households_HouseholdId",
                    column: item => item.HouseholdId,
                    principalSchema: "homepit",
                    principalTable: "households",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "asset_property_details",
            schema: "homepit",
            columns: table => new
            {
                AssetId = table.Column<Guid>(type: "uuid", nullable: false),
                RegistryNumber = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                PropertyInscription = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                PrivateAreaSquareMeters = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                DebtCheckOn = table.Column<DateOnly>(type: "date", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_asset_property_details", item => item.AssetId);
                table.ForeignKey(
                    name: "FK_asset_property_details_assets_AssetId",
                    column: item => item.AssetId,
                    principalSchema: "homepit",
                    principalTable: "assets",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "asset_valuations",
            schema: "homepit",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                AssetId = table.Column<Guid>(type: "uuid", nullable: false),
                ReferenceYear = table.Column<int>(type: "integer", nullable: false),
                Label = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                Amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                Notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_asset_valuations", item => item.Id);
                table.ForeignKey(
                    name: "FK_asset_valuations_assets_AssetId",
                    column: item => item.AssetId,
                    principalSchema: "homepit",
                    principalTable: "assets",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "asset_vehicle_details",
            schema: "homepit",
            columns: table => new
            {
                AssetId = table.Column<Guid>(type: "uuid", nullable: false),
                Brand = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                Model = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                YearModel = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                Renavam = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_asset_vehicle_details", item => item.AssetId);
                table.ForeignKey(
                    name: "FK_asset_vehicle_details_assets_AssetId",
                    column: item => item.AssetId,
                    principalSchema: "homepit",
                    principalTable: "assets",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "credit_card_statements",
            schema: "homepit",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                CreditCardAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedByMemberId = table.Column<Guid>(type: "uuid", nullable: true),
                ClosingDate = table.Column<DateOnly>(type: "date", nullable: false),
                DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                TotalAmount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                Notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                ExternalSource = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                ExternalReference = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                ImportedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_credit_card_statements", item => item.Id);
                table.ForeignKey(
                    name: "FK_credit_card_statements_credit_card_accounts_CreditCardAccountId",
                    column: item => item.CreditCardAccountId,
                    principalSchema: "homepit",
                    principalTable: "credit_card_accounts",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_credit_card_statements_household_members_CreatedByMemberId",
                    column: item => item.CreatedByMemberId,
                    principalSchema: "homepit",
                    principalTable: "household_members",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "FK_credit_card_statements_households_HouseholdId",
                    column: item => item.HouseholdId,
                    principalSchema: "homepit",
                    principalTable: "households",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "finance_recurring_templates",
            schema: "homepit",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedByMemberId = table.Column<Guid>(type: "uuid", nullable: true),
                UniverseId = table.Column<Guid>(type: "uuid", nullable: true),
                ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                Type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                DefaultAmount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                Recurrence = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                DayOfMonth = table.Column<int>(type: "integer", nullable: true),
                MonthOfYear = table.Column<int>(type: "integer", nullable: true),
                IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_finance_recurring_templates", item => item.Id);
                table.ForeignKey(
                    name: "FK_finance_recurring_templates_household_members_CreatedByMemberId",
                    column: item => item.CreatedByMemberId,
                    principalSchema: "homepit",
                    principalTable: "household_members",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "FK_finance_recurring_templates_households_HouseholdId",
                    column: item => item.HouseholdId,
                    principalSchema: "homepit",
                    principalTable: "households",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_finance_recurring_templates_projects_ProjectId",
                    column: item => item.ProjectId,
                    principalSchema: "homepit",
                    principalTable: "projects",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "FK_finance_recurring_templates_universes_UniverseId",
                    column: item => item.UniverseId,
                    principalSchema: "homepit",
                    principalTable: "universes",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateTable(
            name: "credit_card_transactions",
            schema: "homepit",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                CreditCardAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                CreditCardStatementId = table.Column<Guid>(type: "uuid", nullable: true),
                CreatedByMemberId = table.Column<Guid>(type: "uuid", nullable: true),
                UniverseId = table.Column<Guid>(type: "uuid", nullable: true),
                ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Merchant = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                Amount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                PurchasedOn = table.Column<DateOnly>(type: "date", nullable: false),
                Notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                ExternalSource = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                ExternalReference = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                ImportedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_credit_card_transactions", item => item.Id);
                table.ForeignKey(
                    name: "FK_credit_card_transactions_credit_card_accounts_CreditCardAccountId",
                    column: item => item.CreditCardAccountId,
                    principalSchema: "homepit",
                    principalTable: "credit_card_accounts",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_credit_card_transactions_credit_card_statements_CreditCardStatementId",
                    column: item => item.CreditCardStatementId,
                    principalSchema: "homepit",
                    principalTable: "credit_card_statements",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "FK_credit_card_transactions_household_members_CreatedByMemberId",
                    column: item => item.CreatedByMemberId,
                    principalSchema: "homepit",
                    principalTable: "household_members",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "FK_credit_card_transactions_households_HouseholdId",
                    column: item => item.HouseholdId,
                    principalSchema: "homepit",
                    principalTable: "households",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_credit_card_transactions_projects_ProjectId",
                    column: item => item.ProjectId,
                    principalSchema: "homepit",
                    principalTable: "projects",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "FK_credit_card_transactions_universes_UniverseId",
                    column: item => item.UniverseId,
                    principalSchema: "homepit",
                    principalTable: "universes",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateTable(
            name: "finance_entries",
            schema: "homepit",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                FinancePeriodId = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedByMemberId = table.Column<Guid>(type: "uuid", nullable: true),
                RecurringTemplateId = table.Column<Guid>(type: "uuid", nullable: true),
                CreditCardStatementId = table.Column<Guid>(type: "uuid", nullable: true),
                UniverseId = table.Column<Guid>(type: "uuid", nullable: true),
                ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                Title = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                Notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                Amount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                Type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                Verified = table.Column<bool>(type: "boolean", nullable: false),
                ReferenceDate = table.Column<DateOnly>(type: "date", nullable: false),
                Origin = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_finance_entries", item => item.Id);
                table.ForeignKey(
                    name: "FK_finance_entries_credit_card_statements_CreditCardStatementId",
                    column: item => item.CreditCardStatementId,
                    principalSchema: "homepit",
                    principalTable: "credit_card_statements",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "FK_finance_entries_finance_periods_FinancePeriodId",
                    column: item => item.FinancePeriodId,
                    principalSchema: "homepit",
                    principalTable: "finance_periods",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_finance_entries_finance_recurring_templates_RecurringTemplateId",
                    column: item => item.RecurringTemplateId,
                    principalSchema: "homepit",
                    principalTable: "finance_recurring_templates",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "FK_finance_entries_household_members_CreatedByMemberId",
                    column: item => item.CreatedByMemberId,
                    principalSchema: "homepit",
                    principalTable: "household_members",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "FK_finance_entries_households_HouseholdId",
                    column: item => item.HouseholdId,
                    principalSchema: "homepit",
                    principalTable: "households",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_finance_entries_projects_ProjectId",
                    column: item => item.ProjectId,
                    principalSchema: "homepit",
                    principalTable: "projects",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "FK_finance_entries_universes_UniverseId",
                    column: item => item.UniverseId,
                    principalSchema: "homepit",
                    principalTable: "universes",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateIndex(
            name: "IX_assets_CreatedByMemberId",
            schema: "homepit",
            table: "assets",
            column: "CreatedByMemberId");

        migrationBuilder.CreateIndex(
            name: "IX_assets_HouseholdId_Type_Title",
            schema: "homepit",
            table: "assets",
            columns: new[] { "HouseholdId", "Type", "Title" });

        migrationBuilder.CreateIndex(
            name: "IX_asset_valuations_AssetId_ReferenceYear_Label",
            schema: "homepit",
            table: "asset_valuations",
            columns: new[] { "AssetId", "ReferenceYear", "Label" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_credit_card_accounts_CreatedByMemberId",
            schema: "homepit",
            table: "credit_card_accounts",
            column: "CreatedByMemberId");

        migrationBuilder.CreateIndex(
            name: "IX_credit_card_accounts_HouseholdId_Name",
            schema: "homepit",
            table: "credit_card_accounts",
            columns: new[] { "HouseholdId", "Name" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_credit_card_statements_CreatedByMemberId",
            schema: "homepit",
            table: "credit_card_statements",
            column: "CreatedByMemberId");

        migrationBuilder.CreateIndex(
            name: "IX_credit_card_statements_CreditCardAccountId_DueDate",
            schema: "homepit",
            table: "credit_card_statements",
            columns: new[] { "CreditCardAccountId", "DueDate" });

        migrationBuilder.CreateIndex(
            name: "IX_credit_card_statements_HouseholdId",
            schema: "homepit",
            table: "credit_card_statements",
            column: "HouseholdId");

        migrationBuilder.CreateIndex(
            name: "IX_credit_card_transactions_CreatedByMemberId",
            schema: "homepit",
            table: "credit_card_transactions",
            column: "CreatedByMemberId");

        migrationBuilder.CreateIndex(
            name: "IX_credit_card_transactions_CreditCardAccountId_PurchasedOn",
            schema: "homepit",
            table: "credit_card_transactions",
            columns: new[] { "CreditCardAccountId", "PurchasedOn" });

        migrationBuilder.CreateIndex(
            name: "IX_credit_card_transactions_CreditCardStatementId",
            schema: "homepit",
            table: "credit_card_transactions",
            column: "CreditCardStatementId");

        migrationBuilder.CreateIndex(
            name: "IX_credit_card_transactions_HouseholdId",
            schema: "homepit",
            table: "credit_card_transactions",
            column: "HouseholdId");

        migrationBuilder.CreateIndex(
            name: "IX_credit_card_transactions_ProjectId",
            schema: "homepit",
            table: "credit_card_transactions",
            column: "ProjectId");

        migrationBuilder.CreateIndex(
            name: "IX_credit_card_transactions_UniverseId",
            schema: "homepit",
            table: "credit_card_transactions",
            column: "UniverseId");

        migrationBuilder.CreateIndex(
            name: "IX_finance_entries_CreatedByMemberId",
            schema: "homepit",
            table: "finance_entries",
            column: "CreatedByMemberId");

        migrationBuilder.CreateIndex(
            name: "IX_finance_entries_CreditCardStatementId",
            schema: "homepit",
            table: "finance_entries",
            column: "CreditCardStatementId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_finance_entries_FinancePeriodId",
            schema: "homepit",
            table: "finance_entries",
            column: "FinancePeriodId");

        migrationBuilder.CreateIndex(
            name: "IX_finance_entries_HouseholdId_FinancePeriodId_ReferenceDate",
            schema: "homepit",
            table: "finance_entries",
            columns: new[] { "HouseholdId", "FinancePeriodId", "ReferenceDate" });

        migrationBuilder.CreateIndex(
            name: "IX_finance_entries_ProjectId",
            schema: "homepit",
            table: "finance_entries",
            column: "ProjectId");

        migrationBuilder.CreateIndex(
            name: "IX_finance_entries_RecurringTemplateId",
            schema: "homepit",
            table: "finance_entries",
            column: "RecurringTemplateId");

        migrationBuilder.CreateIndex(
            name: "IX_finance_entries_UniverseId",
            schema: "homepit",
            table: "finance_entries",
            column: "UniverseId");

        migrationBuilder.CreateIndex(
            name: "IX_finance_periods_HouseholdId_Year_Month",
            schema: "homepit",
            table: "finance_periods",
            columns: new[] { "HouseholdId", "Year", "Month" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_finance_recurring_templates_CreatedByMemberId",
            schema: "homepit",
            table: "finance_recurring_templates",
            column: "CreatedByMemberId");

        migrationBuilder.CreateIndex(
            name: "IX_finance_recurring_templates_HouseholdId_IsActive_Recurrence",
            schema: "homepit",
            table: "finance_recurring_templates",
            columns: new[] { "HouseholdId", "IsActive", "Recurrence" });

        migrationBuilder.CreateIndex(
            name: "IX_finance_recurring_templates_ProjectId",
            schema: "homepit",
            table: "finance_recurring_templates",
            column: "ProjectId");

        migrationBuilder.CreateIndex(
            name: "IX_finance_recurring_templates_UniverseId",
            schema: "homepit",
            table: "finance_recurring_templates",
            column: "UniverseId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "asset_property_details",
            schema: "homepit");

        migrationBuilder.DropTable(
            name: "asset_valuations",
            schema: "homepit");

        migrationBuilder.DropTable(
            name: "asset_vehicle_details",
            schema: "homepit");

        migrationBuilder.DropTable(
            name: "credit_card_transactions",
            schema: "homepit");

        migrationBuilder.DropTable(
            name: "finance_entries",
            schema: "homepit");

        migrationBuilder.DropTable(
            name: "assets",
            schema: "homepit");

        migrationBuilder.DropTable(
            name: "credit_card_statements",
            schema: "homepit");

        migrationBuilder.DropTable(
            name: "finance_periods",
            schema: "homepit");

        migrationBuilder.DropTable(
            name: "finance_recurring_templates",
            schema: "homepit");

        migrationBuilder.DropTable(
            name: "credit_card_accounts",
            schema: "homepit");
    }
}
