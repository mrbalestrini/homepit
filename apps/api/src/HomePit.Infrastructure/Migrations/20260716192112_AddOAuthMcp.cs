using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HomePit.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOAuthMcp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "oauth_authorization_interactions",
                schema: "homepit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ClientId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ClientName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    RedirectUri = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    Scope = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    State = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    CodeChallenge = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    CodeChallengeMethod = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Resource = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeniedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ConsumedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ApprovedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    IntegrationConnectionId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_oauth_authorization_interactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_oauth_authorization_interactions_integration_connections_In~",
                        column: x => x.IntegrationConnectionId,
                        principalSchema: "homepit",
                        principalTable: "integration_connections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_oauth_authorization_interactions_users_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalSchema: "homepit",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "OpenIddictApplications",
                schema: "homepit",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    ApplicationType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ClientId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ClientSecret = table.Column<string>(type: "text", nullable: true),
                    ClientType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ConcurrencyToken = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ConsentType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    DisplayName = table.Column<string>(type: "text", nullable: true),
                    DisplayNames = table.Column<string>(type: "text", nullable: true),
                    JsonWebKeySet = table.Column<string>(type: "text", nullable: true),
                    Permissions = table.Column<string>(type: "text", nullable: true),
                    PostLogoutRedirectUris = table.Column<string>(type: "text", nullable: true),
                    Properties = table.Column<string>(type: "text", nullable: true),
                    RedirectUris = table.Column<string>(type: "text", nullable: true),
                    Requirements = table.Column<string>(type: "text", nullable: true),
                    Settings = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpenIddictApplications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OpenIddictScopes",
                schema: "homepit",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyToken = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Descriptions = table.Column<string>(type: "text", nullable: true),
                    DisplayName = table.Column<string>(type: "text", nullable: true),
                    DisplayNames = table.Column<string>(type: "text", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Properties = table.Column<string>(type: "text", nullable: true),
                    Resources = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpenIddictScopes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OpenIddictAuthorizations",
                schema: "homepit",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    ApplicationId = table.Column<string>(type: "text", nullable: true),
                    ConcurrencyToken = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Properties = table.Column<string>(type: "text", nullable: true),
                    Scopes = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Subject = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpenIddictAuthorizations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OpenIddictAuthorizations_OpenIddictApplications_Application~",
                        column: x => x.ApplicationId,
                        principalSchema: "homepit",
                        principalTable: "OpenIddictApplications",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "OpenIddictTokens",
                schema: "homepit",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    ApplicationId = table.Column<string>(type: "text", nullable: true),
                    AuthorizationId = table.Column<string>(type: "text", nullable: true),
                    ConcurrencyToken = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpirationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Payload = table.Column<string>(type: "text", nullable: true),
                    Properties = table.Column<string>(type: "text", nullable: true),
                    RedemptionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReferenceId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Subject = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    Type = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpenIddictTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OpenIddictTokens_OpenIddictApplications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalSchema: "homepit",
                        principalTable: "OpenIddictApplications",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_OpenIddictTokens_OpenIddictAuthorizations_AuthorizationId",
                        column: x => x.AuthorizationId,
                        principalSchema: "homepit",
                        principalTable: "OpenIddictAuthorizations",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_oauth_authorization_interactions_ApprovedByUserId",
                schema: "homepit",
                table: "oauth_authorization_interactions",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_oauth_authorization_interactions_ExpiresAt",
                schema: "homepit",
                table: "oauth_authorization_interactions",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_oauth_authorization_interactions_IntegrationConnectionId",
                schema: "homepit",
                table: "oauth_authorization_interactions",
                column: "IntegrationConnectionId");

            migrationBuilder.CreateIndex(
                name: "IX_oauth_authorization_interactions_TokenHash",
                schema: "homepit",
                table: "oauth_authorization_interactions",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OpenIddictApplications_ClientId",
                schema: "homepit",
                table: "OpenIddictApplications",
                column: "ClientId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OpenIddictAuthorizations_ApplicationId_Status_Subject_Type",
                schema: "homepit",
                table: "OpenIddictAuthorizations",
                columns: new[] { "ApplicationId", "Status", "Subject", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_OpenIddictScopes_Name",
                schema: "homepit",
                table: "OpenIddictScopes",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OpenIddictTokens_ApplicationId_Status_Subject_Type",
                schema: "homepit",
                table: "OpenIddictTokens",
                columns: new[] { "ApplicationId", "Status", "Subject", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_OpenIddictTokens_AuthorizationId",
                schema: "homepit",
                table: "OpenIddictTokens",
                column: "AuthorizationId");

            migrationBuilder.CreateIndex(
                name: "IX_OpenIddictTokens_ReferenceId",
                schema: "homepit",
                table: "OpenIddictTokens",
                column: "ReferenceId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "oauth_authorization_interactions",
                schema: "homepit");

            migrationBuilder.DropTable(
                name: "OpenIddictScopes",
                schema: "homepit");

            migrationBuilder.DropTable(
                name: "OpenIddictTokens",
                schema: "homepit");

            migrationBuilder.DropTable(
                name: "OpenIddictAuthorizations",
                schema: "homepit");

            migrationBuilder.DropTable(
                name: "OpenIddictApplications",
                schema: "homepit");
        }
    }
}
