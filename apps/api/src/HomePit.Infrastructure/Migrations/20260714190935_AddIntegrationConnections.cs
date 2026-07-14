using HomePit.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomePit.Infrastructure.Migrations;

[DbContext(typeof(HomePitDbContext))]
[Migration("20260714190935_AddIntegrationConnections")]
public partial class AddIntegrationConnections : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "integration_connections",
            schema: "homepit",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                CredentialKind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                AccessMode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                KeyId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                SecretHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                TokenPrefix = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                OAuthAuthorizationId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                LastUsedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_integration_connections", x => x.Id);
                table.ForeignKey(
                    name: "FK_integration_connections_households_HouseholdId",
                    column: x => x.HouseholdId,
                    principalSchema: "homepit",
                    principalTable: "households",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_integration_connections_users_UserId",
                    column: x => x.UserId,
                    principalSchema: "homepit",
                    principalTable: "users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "integration_audit_events",
            schema: "homepit",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                IntegrationConnectionId = table.Column<Guid>(type: "uuid", nullable: false),
                Surface = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                Operation = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                ResourceType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                ResourceId = table.Column<Guid>(type: "uuid", nullable: true),
                StatusCode = table.Column<int>(type: "integer", nullable: false),
                TraceId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_integration_audit_events", x => x.Id);
                table.ForeignKey(
                    name: "FK_integration_audit_events_integration_connections_IntegrationConnectionId",
                    column: x => x.IntegrationConnectionId,
                    principalSchema: "homepit",
                    principalTable: "integration_connections",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "integration_idempotency_records",
            schema: "homepit",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                IntegrationConnectionId = table.Column<Guid>(type: "uuid", nullable: false),
                Operation = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                RequestHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                ResponseJson = table.Column<string>(type: "text", nullable: false),
                StatusCode = table.Column<int>(type: "integer", nullable: false),
                ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_integration_idempotency_records", x => x.Id);
                table.ForeignKey(
                    name: "FK_integration_idempotency_records_integration_connections_IntegrationConnectionId",
                    column: x => x.IntegrationConnectionId,
                    principalSchema: "homepit",
                    principalTable: "integration_connections",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex("IX_integration_connections_HouseholdId", "integration_connections", "HouseholdId", schema: "homepit");
        migrationBuilder.CreateIndex("IX_integration_connections_KeyId", "integration_connections", "KeyId", schema: "homepit", unique: true);
        migrationBuilder.CreateIndex("IX_integration_connections_UserId_HouseholdId_ExpiresAt", "integration_connections", new[] { "UserId", "HouseholdId", "ExpiresAt" }, schema: "homepit");
        migrationBuilder.CreateIndex("IX_integration_audit_events_CreatedAt", "integration_audit_events", "CreatedAt", schema: "homepit");
        migrationBuilder.CreateIndex("IX_integration_audit_events_IntegrationConnectionId_CreatedAt", "integration_audit_events", new[] { "IntegrationConnectionId", "CreatedAt" }, schema: "homepit");
        migrationBuilder.CreateIndex("IX_integration_idempotency_records_ExpiresAt", "integration_idempotency_records", "ExpiresAt", schema: "homepit");
        migrationBuilder.CreateIndex("IX_integration_idempotency_records_IntegrationConnectionId_Operation_IdempotencyKey", "integration_idempotency_records", new[] { "IntegrationConnectionId", "Operation", "IdempotencyKey" }, schema: "homepit", unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "integration_audit_events", schema: "homepit");
        migrationBuilder.DropTable(name: "integration_idempotency_records", schema: "homepit");
        migrationBuilder.DropTable(name: "integration_connections", schema: "homepit");
    }
}
