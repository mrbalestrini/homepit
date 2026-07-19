using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrganizaClub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class OrganizaClubBaseline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "organiza_club");

            migrationBuilder.CreateTable(
                name: "institutional_pages",
                schema: "organiza_club",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Slug = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    SeoTitle = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    SeoDescription = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    SeoImageObjectKey = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    SeoImageContentType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    SeoImageUpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    BrandName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    BrandTagline = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    HeroEyebrow = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    HeroTitle = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    HeroDescription = table.Column<string>(type: "character varying(1200)", maxLength: 1200, nullable: false),
                    PrimaryCtaLabel = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    PrimaryCtaUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    BenefitsTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    BenefitsDescription = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false),
                    StepsTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    StepsDescription = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false),
                    HighlightEyebrow = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    HighlightTitle = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    HighlightDescription = table.Column<string>(type: "character varying(1200)", maxLength: 1200, nullable: false),
                    FinalCtaTitle = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    FinalCtaDescription = table.Column<string>(type: "character varying(1200)", maxLength: 1200, nullable: false),
                    FooterText = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false),
                    HeroImageAlt = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    HeroImageObjectKey = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    HeroImageContentType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    HeroImageUpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    HighlightImageAlt = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    HighlightImageObjectKey = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    HighlightImageContentType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    HighlightImageUpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_institutional_pages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OpenIddictApplications",
                schema: "organiza_club",
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
                schema: "organiza_club",
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
                name: "plan_definitions",
                schema: "organiza_club",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Slug = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    MonthlyPrice = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    AnnualPrice = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    MaxOwnedSpaces = table.Column<int>(type: "integer", nullable: false),
                    MaxCores = table.Column<int>(type: "integer", nullable: false),
                    MaxProjects = table.Column<int>(type: "integer", nullable: false),
                    MaxInvitedMembers = table.Column<int>(type: "integer", nullable: true),
                    MaxOriginalImages = table.Column<int>(type: "integer", nullable: false),
                    ShowInCatalog = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    IsPopular = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plan_definitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "platform_settings",
                schema: "organiza_club",
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

            migrationBuilder.CreateTable(
                name: "users",
                schema: "organiza_club",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    PhoneNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    ProfilePhotoObjectKey = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    ProfilePhotoUpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SystemRole = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    AccountState = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ScheduledDeletionAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeactivatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeactivatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_users_users_DeactivatedByUserId",
                        column: x => x.DeactivatedByUserId,
                        principalSchema: "organiza_club",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "institutional_benefits",
                schema: "organiza_club",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InstitutionalPageId = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_institutional_benefits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_institutional_benefits_institutional_pages_InstitutionalPag~",
                        column: x => x.InstitutionalPageId,
                        principalSchema: "organiza_club",
                        principalTable: "institutional_pages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "institutional_steps",
                schema: "organiza_club",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InstitutionalPageId = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_institutional_steps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_institutional_steps_institutional_pages_InstitutionalPageId",
                        column: x => x.InstitutionalPageId,
                        principalSchema: "organiza_club",
                        principalTable: "institutional_pages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OpenIddictAuthorizations",
                schema: "organiza_club",
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
                        principalSchema: "organiza_club",
                        principalTable: "OpenIddictApplications",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                schema: "organiza_club",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refresh_tokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_refresh_tokens_users_UserId",
                        column: x => x.UserId,
                        principalSchema: "organiza_club",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "spaces",
                schema: "organiza_club",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_spaces", x => x.Id);
                    table.ForeignKey(
                        name: "FK_spaces_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalSchema: "organiza_club",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tool_improvement_suggestions",
                schema: "organiza_club",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SuggestionText = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Priority = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    InternalComment = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    LastReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastReviewedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tool_improvement_suggestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tool_improvement_suggestions_users_LastReviewedByUserId",
                        column: x => x.LastReviewedByUserId,
                        principalSchema: "organiza_club",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_tool_improvement_suggestions_users_UserId",
                        column: x => x.UserId,
                        principalSchema: "organiza_club",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_plan_image_assets",
                schema: "organiza_club",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Module = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    ObjectKey = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    UploadedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsDegraded = table.Column<bool>(type: "boolean", nullable: false),
                    DegradedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_plan_image_assets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_plan_image_assets_users_UserId",
                        column: x => x.UserId,
                        principalSchema: "organiza_club",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_subscriptions",
                schema: "organiza_club",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    BillingCycle = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AmountPaid = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    AdminNote = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_subscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_subscriptions_plan_definitions_PlanDefinitionId",
                        column: x => x.PlanDefinitionId,
                        principalSchema: "organiza_club",
                        principalTable: "plan_definitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_subscriptions_users_UserId",
                        column: x => x.UserId,
                        principalSchema: "organiza_club",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OpenIddictTokens",
                schema: "organiza_club",
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
                        principalSchema: "organiza_club",
                        principalTable: "OpenIddictApplications",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_OpenIddictTokens_OpenIddictAuthorizations_AuthorizationId",
                        column: x => x.AuthorizationId,
                        principalSchema: "organiza_club",
                        principalTable: "OpenIddictAuthorizations",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "finance_periods",
                schema: "organiza_club",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SpaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Month = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_finance_periods", x => x.Id);
                    table.ForeignKey(
                        name: "FK_finance_periods_spaces_SpaceId",
                        column: x => x.SpaceId,
                        principalSchema: "organiza_club",
                        principalTable: "spaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "integration_connections",
                schema: "organiza_club",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SpaceId = table.Column<Guid>(type: "uuid", nullable: false),
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
                        name: "FK_integration_connections_spaces_SpaceId",
                        column: x => x.SpaceId,
                        principalSchema: "organiza_club",
                        principalTable: "spaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_integration_connections_users_UserId",
                        column: x => x.UserId,
                        principalSchema: "organiza_club",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "space_invitations",
                schema: "organiza_club",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SpaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    InviterUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    InviteeEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    Role = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    InvitedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RespondedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_space_invitations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_space_invitations_spaces_SpaceId",
                        column: x => x.SpaceId,
                        principalSchema: "organiza_club",
                        principalTable: "spaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_space_invitations_users_InviterUserId",
                        column: x => x.InviterUserId,
                        principalSchema: "organiza_club",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "space_members",
                schema: "organiza_club",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SpaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_space_members", x => x.Id);
                    table.ForeignKey(
                        name: "FK_space_members_spaces_SpaceId",
                        column: x => x.SpaceId,
                        principalSchema: "organiza_club",
                        principalTable: "spaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_space_members_users_UserId",
                        column: x => x.UserId,
                        principalSchema: "organiza_club",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "integration_audit_events",
                schema: "organiza_club",
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
                        name: "FK_integration_audit_events_integration_connections_Integratio~",
                        column: x => x.IntegrationConnectionId,
                        principalSchema: "organiza_club",
                        principalTable: "integration_connections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "integration_idempotency_records",
                schema: "organiza_club",
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
                        name: "FK_integration_idempotency_records_integration_connections_Int~",
                        column: x => x.IntegrationConnectionId,
                        principalSchema: "organiza_club",
                        principalTable: "integration_connections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "oauth_authorization_interactions",
                schema: "organiza_club",
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
                        principalSchema: "organiza_club",
                        principalTable: "integration_connections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_oauth_authorization_interactions_users_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalSchema: "organiza_club",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "assets",
                schema: "organiza_club",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SpaceId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_assets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_assets_space_members_CreatedByMemberId",
                        column: x => x.CreatedByMemberId,
                        principalSchema: "organiza_club",
                        principalTable: "space_members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_assets_spaces_SpaceId",
                        column: x => x.SpaceId,
                        principalSchema: "organiza_club",
                        principalTable: "spaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "cores",
                schema: "organiza_club",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SpaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByMemberId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    ImageUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ImageObjectKey = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    ImageContentType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ImageUpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cores_space_members_CreatedByMemberId",
                        column: x => x.CreatedByMemberId,
                        principalSchema: "organiza_club",
                        principalTable: "space_members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_cores_spaces_SpaceId",
                        column: x => x.SpaceId,
                        principalSchema: "organiza_club",
                        principalTable: "spaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "credit_card_accounts",
                schema: "organiza_club",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SpaceId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_credit_card_accounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_credit_card_accounts_space_members_CreatedByMemberId",
                        column: x => x.CreatedByMemberId,
                        principalSchema: "organiza_club",
                        principalTable: "space_members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_credit_card_accounts_spaces_SpaceId",
                        column: x => x.SpaceId,
                        principalSchema: "organiza_club",
                        principalTable: "spaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "finance_categories",
                schema: "organiza_club",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SpaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByMemberId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_finance_categories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_finance_categories_space_members_CreatedByMemberId",
                        column: x => x.CreatedByMemberId,
                        principalSchema: "organiza_club",
                        principalTable: "space_members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_finance_categories_spaces_SpaceId",
                        column: x => x.SpaceId,
                        principalSchema: "organiza_club",
                        principalTable: "spaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "gsm_numbers",
                schema: "organiza_club",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SpaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByMemberId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    NormalizedNumber = table.Column<string>(type: "character varying(13)", maxLength: 13, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Plan = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    MonthlyCost = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    DaysWithoutRecharge = table.Column<int>(type: "integer", nullable: true),
                    AcquiredOn = table.Column<DateOnly>(type: "date", nullable: false),
                    LastRechargeOn = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gsm_numbers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_gsm_numbers_space_members_CreatedByMemberId",
                        column: x => x.CreatedByMemberId,
                        principalSchema: "organiza_club",
                        principalTable: "space_members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_gsm_numbers_spaces_SpaceId",
                        column: x => x.SpaceId,
                        principalSchema: "organiza_club",
                        principalTable: "spaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notification_preferences",
                schema: "organiza_club",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SpaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    SpaceMemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    DailyDigestEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    WhatsAppPhoneNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    DailyDigestTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    TimeZoneId = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_preferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_notification_preferences_space_members_SpaceMemberId",
                        column: x => x.SpaceMemberId,
                        principalSchema: "organiza_club",
                        principalTable: "space_members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_notification_preferences_spaces_SpaceId",
                        column: x => x.SpaceId,
                        principalSchema: "organiza_club",
                        principalTable: "spaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notification_runs",
                schema: "organiza_club",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SpaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    SpaceMemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    LocalDate = table.Column<DateOnly>(type: "date", nullable: false),
                    SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ProviderMessageId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_runs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_notification_runs_space_members_SpaceMemberId",
                        column: x => x.SpaceMemberId,
                        principalSchema: "organiza_club",
                        principalTable: "space_members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_notification_runs_spaces_SpaceId",
                        column: x => x.SpaceId,
                        principalSchema: "organiza_club",
                        principalTable: "spaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "prompt_categories",
                schema: "organiza_club",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SpaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByMemberId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prompt_categories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_prompt_categories_space_members_CreatedByMemberId",
                        column: x => x.CreatedByMemberId,
                        principalSchema: "organiza_club",
                        principalTable: "space_members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_prompt_categories_spaces_SpaceId",
                        column: x => x.SpaceId,
                        principalSchema: "organiza_club",
                        principalTable: "spaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "asset_property_details",
                schema: "organiza_club",
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
                    table.PrimaryKey("PK_asset_property_details", x => x.AssetId);
                    table.ForeignKey(
                        name: "FK_asset_property_details_assets_AssetId",
                        column: x => x.AssetId,
                        principalSchema: "organiza_club",
                        principalTable: "assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "asset_valuations",
                schema: "organiza_club",
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
                    table.PrimaryKey("PK_asset_valuations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_asset_valuations_assets_AssetId",
                        column: x => x.AssetId,
                        principalSchema: "organiza_club",
                        principalTable: "assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "asset_vehicle_details",
                schema: "organiza_club",
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
                    table.PrimaryKey("PK_asset_vehicle_details", x => x.AssetId);
                    table.ForeignKey(
                        name: "FK_asset_vehicle_details_assets_AssetId",
                        column: x => x.AssetId,
                        principalSchema: "organiza_club",
                        principalTable: "assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "projects",
                schema: "organiza_club",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SpaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    CoreId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByMemberId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_projects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_projects_cores_CoreId",
                        column: x => x.CoreId,
                        principalSchema: "organiza_club",
                        principalTable: "cores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_projects_space_members_CreatedByMemberId",
                        column: x => x.CreatedByMemberId,
                        principalSchema: "organiza_club",
                        principalTable: "space_members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "prompts",
                schema: "organiza_club",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SpaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByMemberId = table.Column<Guid>(type: "uuid", nullable: true),
                    CoreId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    PromptText = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: false),
                    LinkUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    LinkTitle = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                    IsArchived = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ImageObjectKey = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    ImageContentType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ImageUpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prompts", x => x.Id);
                    table.CheckConstraint("CK_prompts_link_url_title_pair", "(\"LinkUrl\" IS NULL AND \"LinkTitle\" IS NULL)\nOR\n(\"LinkUrl\" IS NOT NULL AND \"LinkTitle\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_prompts_cores_CoreId",
                        column: x => x.CoreId,
                        principalSchema: "organiza_club",
                        principalTable: "cores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_prompts_space_members_CreatedByMemberId",
                        column: x => x.CreatedByMemberId,
                        principalSchema: "organiza_club",
                        principalTable: "space_members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_prompts_spaces_SpaceId",
                        column: x => x.SpaceId,
                        principalSchema: "organiza_club",
                        principalTable: "spaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "credit_card_statements",
                schema: "organiza_club",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SpaceId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_credit_card_statements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_credit_card_statements_credit_card_accounts_CreditCardAccou~",
                        column: x => x.CreditCardAccountId,
                        principalSchema: "organiza_club",
                        principalTable: "credit_card_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_credit_card_statements_space_members_CreatedByMemberId",
                        column: x => x.CreatedByMemberId,
                        principalSchema: "organiza_club",
                        principalTable: "space_members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_credit_card_statements_spaces_SpaceId",
                        column: x => x.SpaceId,
                        principalSchema: "organiza_club",
                        principalTable: "spaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "gsm_recharges",
                schema: "organiza_club",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SpaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    GsmNumberId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByMemberId = table.Column<Guid>(type: "uuid", nullable: true),
                    RechargedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    Note = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gsm_recharges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_gsm_recharges_gsm_numbers_GsmNumberId",
                        column: x => x.GsmNumberId,
                        principalSchema: "organiza_club",
                        principalTable: "gsm_numbers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_gsm_recharges_space_members_CreatedByMemberId",
                        column: x => x.CreatedByMemberId,
                        principalSchema: "organiza_club",
                        principalTable: "space_members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_gsm_recharges_spaces_SpaceId",
                        column: x => x.SpaceId,
                        principalSchema: "organiza_club",
                        principalTable: "spaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "activities",
                schema: "organiza_club",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SpaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResponsibleMemberId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedByMemberId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ImageObjectKey = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    ImageContentType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ImageUpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Priority = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Size = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_activities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_activities_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "organiza_club",
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_activities_space_members_CreatedByMemberId",
                        column: x => x.CreatedByMemberId,
                        principalSchema: "organiza_club",
                        principalTable: "space_members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_activities_space_members_ResponsibleMemberId",
                        column: x => x.ResponsibleMemberId,
                        principalSchema: "organiza_club",
                        principalTable: "space_members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "finance_recurring_templates",
                schema: "organiza_club",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SpaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByMemberId = table.Column<Guid>(type: "uuid", nullable: true),
                    CoreId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_finance_recurring_templates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_finance_recurring_templates_cores_CoreId",
                        column: x => x.CoreId,
                        principalSchema: "organiza_club",
                        principalTable: "cores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_finance_recurring_templates_finance_categories_CategoryId",
                        column: x => x.CategoryId,
                        principalSchema: "organiza_club",
                        principalTable: "finance_categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_finance_recurring_templates_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "organiza_club",
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_finance_recurring_templates_space_members_CreatedByMemberId",
                        column: x => x.CreatedByMemberId,
                        principalSchema: "organiza_club",
                        principalTable: "space_members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_finance_recurring_templates_spaces_SpaceId",
                        column: x => x.SpaceId,
                        principalSchema: "organiza_club",
                        principalTable: "spaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "member_effort_allocations",
                schema: "organiza_club",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SpaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    SpaceMemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    CoreId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    ScopeType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Weekday = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Points = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_member_effort_allocations", x => x.Id);
                    table.CheckConstraint("CK_member_effort_allocations_points_non_negative", "\"Points\" >= 0");
                    table.CheckConstraint("CK_member_effort_allocations_scope", "(\"ScopeType\" = 'Space' AND \"CoreId\" IS NULL AND \"ProjectId\" IS NULL) OR (\"ScopeType\" = 'Core' AND \"CoreId\" IS NOT NULL AND \"ProjectId\" IS NULL) OR (\"ScopeType\" = 'Project' AND \"CoreId\" IS NULL AND \"ProjectId\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_member_effort_allocations_cores_CoreId",
                        column: x => x.CoreId,
                        principalSchema: "organiza_club",
                        principalTable: "cores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_member_effort_allocations_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "organiza_club",
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_member_effort_allocations_space_members_SpaceMemberId",
                        column: x => x.SpaceMemberId,
                        principalSchema: "organiza_club",
                        principalTable: "space_members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_member_effort_allocations_spaces_SpaceId",
                        column: x => x.SpaceId,
                        principalSchema: "organiza_club",
                        principalTable: "spaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "prompt_category_assignments",
                schema: "organiza_club",
                columns: table => new
                {
                    PromptId = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prompt_category_assignments", x => new { x.PromptId, x.CategoryId });
                    table.ForeignKey(
                        name: "FK_prompt_category_assignments_prompt_categories_CategoryId",
                        column: x => x.CategoryId,
                        principalSchema: "organiza_club",
                        principalTable: "prompt_categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_prompt_category_assignments_prompts_PromptId",
                        column: x => x.PromptId,
                        principalSchema: "organiza_club",
                        principalTable: "prompts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "credit_card_transactions",
                schema: "organiza_club",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SpaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreditCardAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreditCardStatementId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedByMemberId = table.Column<Guid>(type: "uuid", nullable: true),
                    CoreId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_credit_card_transactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_credit_card_transactions_cores_CoreId",
                        column: x => x.CoreId,
                        principalSchema: "organiza_club",
                        principalTable: "cores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_credit_card_transactions_credit_card_accounts_CreditCardAcc~",
                        column: x => x.CreditCardAccountId,
                        principalSchema: "organiza_club",
                        principalTable: "credit_card_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_credit_card_transactions_credit_card_statements_CreditCardS~",
                        column: x => x.CreditCardStatementId,
                        principalSchema: "organiza_club",
                        principalTable: "credit_card_statements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_credit_card_transactions_finance_categories_CategoryId",
                        column: x => x.CategoryId,
                        principalSchema: "organiza_club",
                        principalTable: "finance_categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_credit_card_transactions_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "organiza_club",
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_credit_card_transactions_space_members_CreatedByMemberId",
                        column: x => x.CreatedByMemberId,
                        principalSchema: "organiza_club",
                        principalTable: "space_members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_credit_card_transactions_spaces_SpaceId",
                        column: x => x.SpaceId,
                        principalSchema: "organiza_club",
                        principalTable: "spaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "activity_comments",
                schema: "organiza_club",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SpaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActivityId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorMemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    Body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_activity_comments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_activity_comments_activities_ActivityId",
                        column: x => x.ActivityId,
                        principalSchema: "organiza_club",
                        principalTable: "activities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_activity_comments_space_members_AuthorMemberId",
                        column: x => x.AuthorMemberId,
                        principalSchema: "organiza_club",
                        principalTable: "space_members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pending_items",
                schema: "organiza_club",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SpaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActivityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Priority = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    SnoozeDays = table.Column<int>(type: "integer", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pending_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_pending_items_activities_ActivityId",
                        column: x => x.ActivityId,
                        principalSchema: "organiza_club",
                        principalTable: "activities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "finance_entries",
                schema: "organiza_club",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SpaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    FinancePeriodId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByMemberId = table.Column<Guid>(type: "uuid", nullable: true),
                    RecurringTemplateId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreditCardStatementId = table.Column<Guid>(type: "uuid", nullable: true),
                    CoreId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_finance_entries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_finance_entries_cores_CoreId",
                        column: x => x.CoreId,
                        principalSchema: "organiza_club",
                        principalTable: "cores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_finance_entries_credit_card_statements_CreditCardStatementId",
                        column: x => x.CreditCardStatementId,
                        principalSchema: "organiza_club",
                        principalTable: "credit_card_statements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_finance_entries_finance_categories_CategoryId",
                        column: x => x.CategoryId,
                        principalSchema: "organiza_club",
                        principalTable: "finance_categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_finance_entries_finance_periods_FinancePeriodId",
                        column: x => x.FinancePeriodId,
                        principalSchema: "organiza_club",
                        principalTable: "finance_periods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_finance_entries_finance_recurring_templates_RecurringTempla~",
                        column: x => x.RecurringTemplateId,
                        principalSchema: "organiza_club",
                        principalTable: "finance_recurring_templates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_finance_entries_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "organiza_club",
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_finance_entries_space_members_CreatedByMemberId",
                        column: x => x.CreatedByMemberId,
                        principalSchema: "organiza_club",
                        principalTable: "space_members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_finance_entries_spaces_SpaceId",
                        column: x => x.SpaceId,
                        principalSchema: "organiza_club",
                        principalTable: "spaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_activities_CreatedByMemberId",
                schema: "organiza_club",
                table: "activities",
                column: "CreatedByMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_activities_ProjectId",
                schema: "organiza_club",
                table: "activities",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_activities_ResponsibleMemberId",
                schema: "organiza_club",
                table: "activities",
                column: "ResponsibleMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_activity_comments_ActivityId",
                schema: "organiza_club",
                table: "activity_comments",
                column: "ActivityId");

            migrationBuilder.CreateIndex(
                name: "IX_activity_comments_AuthorMemberId",
                schema: "organiza_club",
                table: "activity_comments",
                column: "AuthorMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_activity_comments_SpaceId_ActivityId_CreatedAt",
                schema: "organiza_club",
                table: "activity_comments",
                columns: new[] { "SpaceId", "ActivityId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_asset_valuations_AssetId_ReferenceYear_Label",
                schema: "organiza_club",
                table: "asset_valuations",
                columns: new[] { "AssetId", "ReferenceYear", "Label" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_assets_CreatedByMemberId",
                schema: "organiza_club",
                table: "assets",
                column: "CreatedByMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_assets_SpaceId_Type_Title",
                schema: "organiza_club",
                table: "assets",
                columns: new[] { "SpaceId", "Type", "Title" });

            migrationBuilder.CreateIndex(
                name: "IX_cores_CreatedByMemberId",
                schema: "organiza_club",
                table: "cores",
                column: "CreatedByMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_cores_SpaceId_Name",
                schema: "organiza_club",
                table: "cores",
                columns: new[] { "SpaceId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_credit_card_accounts_CreatedByMemberId",
                schema: "organiza_club",
                table: "credit_card_accounts",
                column: "CreatedByMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_credit_card_accounts_SpaceId_Name",
                schema: "organiza_club",
                table: "credit_card_accounts",
                columns: new[] { "SpaceId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_credit_card_statements_CreatedByMemberId",
                schema: "organiza_club",
                table: "credit_card_statements",
                column: "CreatedByMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_credit_card_statements_CreditCardAccountId_DueDate",
                schema: "organiza_club",
                table: "credit_card_statements",
                columns: new[] { "CreditCardAccountId", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_credit_card_statements_SpaceId",
                schema: "organiza_club",
                table: "credit_card_statements",
                column: "SpaceId");

            migrationBuilder.CreateIndex(
                name: "IX_credit_card_transactions_CategoryId",
                schema: "organiza_club",
                table: "credit_card_transactions",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_credit_card_transactions_CoreId",
                schema: "organiza_club",
                table: "credit_card_transactions",
                column: "CoreId");

            migrationBuilder.CreateIndex(
                name: "IX_credit_card_transactions_CreatedByMemberId",
                schema: "organiza_club",
                table: "credit_card_transactions",
                column: "CreatedByMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_credit_card_transactions_CreditCardAccountId_PurchasedOn",
                schema: "organiza_club",
                table: "credit_card_transactions",
                columns: new[] { "CreditCardAccountId", "PurchasedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_credit_card_transactions_CreditCardStatementId",
                schema: "organiza_club",
                table: "credit_card_transactions",
                column: "CreditCardStatementId");

            migrationBuilder.CreateIndex(
                name: "IX_credit_card_transactions_ProjectId",
                schema: "organiza_club",
                table: "credit_card_transactions",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_credit_card_transactions_SpaceId",
                schema: "organiza_club",
                table: "credit_card_transactions",
                column: "SpaceId");

            migrationBuilder.CreateIndex(
                name: "IX_finance_categories_CreatedByMemberId",
                schema: "organiza_club",
                table: "finance_categories",
                column: "CreatedByMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_finance_categories_SpaceId_Name",
                schema: "organiza_club",
                table: "finance_categories",
                columns: new[] { "SpaceId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_finance_categories_SpaceId_SortOrder",
                schema: "organiza_club",
                table: "finance_categories",
                columns: new[] { "SpaceId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_finance_entries_CategoryId",
                schema: "organiza_club",
                table: "finance_entries",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_finance_entries_CoreId",
                schema: "organiza_club",
                table: "finance_entries",
                column: "CoreId");

            migrationBuilder.CreateIndex(
                name: "IX_finance_entries_CreatedByMemberId",
                schema: "organiza_club",
                table: "finance_entries",
                column: "CreatedByMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_finance_entries_CreditCardStatementId",
                schema: "organiza_club",
                table: "finance_entries",
                column: "CreditCardStatementId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_finance_entries_FinancePeriodId",
                schema: "organiza_club",
                table: "finance_entries",
                column: "FinancePeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_finance_entries_ProjectId",
                schema: "organiza_club",
                table: "finance_entries",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_finance_entries_RecurringTemplateId",
                schema: "organiza_club",
                table: "finance_entries",
                column: "RecurringTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_finance_entries_SpaceId_FinancePeriodId_ReferenceDate",
                schema: "organiza_club",
                table: "finance_entries",
                columns: new[] { "SpaceId", "FinancePeriodId", "ReferenceDate" });

            migrationBuilder.CreateIndex(
                name: "IX_finance_periods_SpaceId_Year_Month",
                schema: "organiza_club",
                table: "finance_periods",
                columns: new[] { "SpaceId", "Year", "Month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_finance_recurring_templates_CategoryId",
                schema: "organiza_club",
                table: "finance_recurring_templates",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_finance_recurring_templates_CoreId",
                schema: "organiza_club",
                table: "finance_recurring_templates",
                column: "CoreId");

            migrationBuilder.CreateIndex(
                name: "IX_finance_recurring_templates_CreatedByMemberId",
                schema: "organiza_club",
                table: "finance_recurring_templates",
                column: "CreatedByMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_finance_recurring_templates_ProjectId",
                schema: "organiza_club",
                table: "finance_recurring_templates",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_finance_recurring_templates_SpaceId_IsActive_Recurrence",
                schema: "organiza_club",
                table: "finance_recurring_templates",
                columns: new[] { "SpaceId", "IsActive", "Recurrence" });

            migrationBuilder.CreateIndex(
                name: "IX_gsm_numbers_CreatedByMemberId",
                schema: "organiza_club",
                table: "gsm_numbers",
                column: "CreatedByMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_gsm_numbers_SpaceId_NormalizedNumber",
                schema: "organiza_club",
                table: "gsm_numbers",
                columns: new[] { "SpaceId", "NormalizedNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_gsm_recharges_CreatedByMemberId",
                schema: "organiza_club",
                table: "gsm_recharges",
                column: "CreatedByMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_gsm_recharges_GsmNumberId",
                schema: "organiza_club",
                table: "gsm_recharges",
                column: "GsmNumberId");

            migrationBuilder.CreateIndex(
                name: "IX_gsm_recharges_SpaceId_GsmNumberId_RechargedOn",
                schema: "organiza_club",
                table: "gsm_recharges",
                columns: new[] { "SpaceId", "GsmNumberId", "RechargedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_institutional_benefits_InstitutionalPageId_Position",
                schema: "organiza_club",
                table: "institutional_benefits",
                columns: new[] { "InstitutionalPageId", "Position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_institutional_pages_Slug",
                schema: "organiza_club",
                table: "institutional_pages",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_institutional_steps_InstitutionalPageId_Position",
                schema: "organiza_club",
                table: "institutional_steps",
                columns: new[] { "InstitutionalPageId", "Position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_integration_audit_events_CreatedAt",
                schema: "organiza_club",
                table: "integration_audit_events",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_integration_audit_events_IntegrationConnectionId_CreatedAt",
                schema: "organiza_club",
                table: "integration_audit_events",
                columns: new[] { "IntegrationConnectionId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_integration_connections_KeyId",
                schema: "organiza_club",
                table: "integration_connections",
                column: "KeyId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_integration_connections_SpaceId",
                schema: "organiza_club",
                table: "integration_connections",
                column: "SpaceId");

            migrationBuilder.CreateIndex(
                name: "IX_integration_connections_UserId_SpaceId_ExpiresAt",
                schema: "organiza_club",
                table: "integration_connections",
                columns: new[] { "UserId", "SpaceId", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_integration_idempotency_records_ExpiresAt",
                schema: "organiza_club",
                table: "integration_idempotency_records",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_integration_idempotency_records_IntegrationConnectionId_Ope~",
                schema: "organiza_club",
                table: "integration_idempotency_records",
                columns: new[] { "IntegrationConnectionId", "Operation", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_member_effort_allocations_CoreId",
                schema: "organiza_club",
                table: "member_effort_allocations",
                column: "CoreId");

            migrationBuilder.CreateIndex(
                name: "IX_member_effort_allocations_ProjectId",
                schema: "organiza_club",
                table: "member_effort_allocations",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_member_effort_allocations_SpaceId",
                schema: "organiza_club",
                table: "member_effort_allocations",
                column: "SpaceId");

            migrationBuilder.CreateIndex(
                name: "IX_member_effort_allocations_SpaceMemberId_CoreId_Weekday",
                schema: "organiza_club",
                table: "member_effort_allocations",
                columns: new[] { "SpaceMemberId", "CoreId", "Weekday" },
                unique: true,
                filter: "\"ScopeType\" = 'Core'");

            migrationBuilder.CreateIndex(
                name: "IX_member_effort_allocations_SpaceMemberId_ProjectId_Weekday",
                schema: "organiza_club",
                table: "member_effort_allocations",
                columns: new[] { "SpaceMemberId", "ProjectId", "Weekday" },
                unique: true,
                filter: "\"ScopeType\" = 'Project'");

            migrationBuilder.CreateIndex(
                name: "IX_member_effort_allocations_SpaceMemberId_Weekday",
                schema: "organiza_club",
                table: "member_effort_allocations",
                columns: new[] { "SpaceMemberId", "Weekday" },
                unique: true,
                filter: "\"ScopeType\" = 'Space'");

            migrationBuilder.CreateIndex(
                name: "IX_notification_preferences_SpaceId",
                schema: "organiza_club",
                table: "notification_preferences",
                column: "SpaceId");

            migrationBuilder.CreateIndex(
                name: "IX_notification_preferences_SpaceMemberId",
                schema: "organiza_club",
                table: "notification_preferences",
                column: "SpaceMemberId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_notification_runs_SpaceId_SpaceMemberId_Kind_LocalDate",
                schema: "organiza_club",
                table: "notification_runs",
                columns: new[] { "SpaceId", "SpaceMemberId", "Kind", "LocalDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_notification_runs_SpaceMemberId",
                schema: "organiza_club",
                table: "notification_runs",
                column: "SpaceMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_oauth_authorization_interactions_ApprovedByUserId",
                schema: "organiza_club",
                table: "oauth_authorization_interactions",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_oauth_authorization_interactions_ExpiresAt",
                schema: "organiza_club",
                table: "oauth_authorization_interactions",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_oauth_authorization_interactions_IntegrationConnectionId",
                schema: "organiza_club",
                table: "oauth_authorization_interactions",
                column: "IntegrationConnectionId");

            migrationBuilder.CreateIndex(
                name: "IX_oauth_authorization_interactions_TokenHash",
                schema: "organiza_club",
                table: "oauth_authorization_interactions",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OpenIddictApplications_ClientId",
                schema: "organiza_club",
                table: "OpenIddictApplications",
                column: "ClientId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OpenIddictAuthorizations_ApplicationId_Status_Subject_Type",
                schema: "organiza_club",
                table: "OpenIddictAuthorizations",
                columns: new[] { "ApplicationId", "Status", "Subject", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_OpenIddictScopes_Name",
                schema: "organiza_club",
                table: "OpenIddictScopes",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OpenIddictTokens_ApplicationId_Status_Subject_Type",
                schema: "organiza_club",
                table: "OpenIddictTokens",
                columns: new[] { "ApplicationId", "Status", "Subject", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_OpenIddictTokens_AuthorizationId",
                schema: "organiza_club",
                table: "OpenIddictTokens",
                column: "AuthorizationId");

            migrationBuilder.CreateIndex(
                name: "IX_OpenIddictTokens_ReferenceId",
                schema: "organiza_club",
                table: "OpenIddictTokens",
                column: "ReferenceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_pending_items_ActivityId",
                schema: "organiza_club",
                table: "pending_items",
                column: "ActivityId");

            migrationBuilder.CreateIndex(
                name: "IX_plan_definitions_Slug",
                schema: "organiza_club",
                table: "plan_definitions",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_platform_settings_Key",
                schema: "organiza_club",
                table: "platform_settings",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_projects_CoreId",
                schema: "organiza_club",
                table: "projects",
                column: "CoreId");

            migrationBuilder.CreateIndex(
                name: "IX_projects_CreatedByMemberId",
                schema: "organiza_club",
                table: "projects",
                column: "CreatedByMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_projects_SpaceId_CoreId_Name",
                schema: "organiza_club",
                table: "projects",
                columns: new[] { "SpaceId", "CoreId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_prompt_categories_CreatedByMemberId",
                schema: "organiza_club",
                table: "prompt_categories",
                column: "CreatedByMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_prompt_categories_SpaceId_Name",
                schema: "organiza_club",
                table: "prompt_categories",
                columns: new[] { "SpaceId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_prompt_category_assignments_CategoryId",
                schema: "organiza_club",
                table: "prompt_category_assignments",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_prompt_category_assignments_PromptId_CategoryId",
                schema: "organiza_club",
                table: "prompt_category_assignments",
                columns: new[] { "PromptId", "CategoryId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_prompts_CoreId",
                schema: "organiza_club",
                table: "prompts",
                column: "CoreId");

            migrationBuilder.CreateIndex(
                name: "IX_prompts_CreatedByMemberId",
                schema: "organiza_club",
                table: "prompts",
                column: "CreatedByMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_prompts_SpaceId_IsArchived_CoreId_UpdatedAt",
                schema: "organiza_club",
                table: "prompts",
                columns: new[] { "SpaceId", "IsArchived", "CoreId", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_prompts_SpaceId_IsArchived_UpdatedAt",
                schema: "organiza_club",
                table: "prompts",
                columns: new[] { "SpaceId", "IsArchived", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_TokenHash",
                schema: "organiza_club",
                table: "refresh_tokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_UserId",
                schema: "organiza_club",
                table: "refresh_tokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_space_invitations_InviteeEmail_Status",
                schema: "organiza_club",
                table: "space_invitations",
                columns: new[] { "InviteeEmail", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_space_invitations_InviterUserId",
                schema: "organiza_club",
                table: "space_invitations",
                column: "InviterUserId");

            migrationBuilder.CreateIndex(
                name: "IX_space_invitations_SpaceId_InviteeEmail",
                schema: "organiza_club",
                table: "space_invitations",
                columns: new[] { "SpaceId", "InviteeEmail" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_space_members_SpaceId_UserId",
                schema: "organiza_club",
                table: "space_members",
                columns: new[] { "SpaceId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_space_members_UserId",
                schema: "organiza_club",
                table: "space_members",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_spaces_CreatedByUserId",
                schema: "organiza_club",
                table: "spaces",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_tool_improvement_suggestions_LastReviewedByUserId",
                schema: "organiza_club",
                table: "tool_improvement_suggestions",
                column: "LastReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_tool_improvement_suggestions_Priority",
                schema: "organiza_club",
                table: "tool_improvement_suggestions",
                column: "Priority");

            migrationBuilder.CreateIndex(
                name: "IX_tool_improvement_suggestions_Status",
                schema: "organiza_club",
                table: "tool_improvement_suggestions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_tool_improvement_suggestions_SubmittedAt",
                schema: "organiza_club",
                table: "tool_improvement_suggestions",
                column: "SubmittedAt");

            migrationBuilder.CreateIndex(
                name: "IX_tool_improvement_suggestions_UserId",
                schema: "organiza_club",
                table: "tool_improvement_suggestions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_user_plan_image_assets_Module_EntityId",
                schema: "organiza_club",
                table: "user_plan_image_assets",
                columns: new[] { "Module", "EntityId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_plan_image_assets_UserId_UploadedAt",
                schema: "organiza_club",
                table: "user_plan_image_assets",
                columns: new[] { "UserId", "UploadedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_user_subscriptions_PlanDefinitionId",
                schema: "organiza_club",
                table: "user_subscriptions",
                column: "PlanDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_user_subscriptions_UserId_EndsAt",
                schema: "organiza_club",
                table: "user_subscriptions",
                columns: new[] { "UserId", "EndsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_user_subscriptions_UserId_StartsAt",
                schema: "organiza_club",
                table: "user_subscriptions",
                columns: new[] { "UserId", "StartsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_users_DeactivatedByUserId",
                schema: "organiza_club",
                table: "users",
                column: "DeactivatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_users_Email",
                schema: "organiza_club",
                table: "users",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "activity_comments",
                schema: "organiza_club");

            migrationBuilder.DropTable(
                name: "asset_property_details",
                schema: "organiza_club");

            migrationBuilder.DropTable(
                name: "asset_valuations",
                schema: "organiza_club");

            migrationBuilder.DropTable(
                name: "asset_vehicle_details",
                schema: "organiza_club");

            migrationBuilder.DropTable(
                name: "credit_card_transactions",
                schema: "organiza_club");

            migrationBuilder.DropTable(
                name: "finance_entries",
                schema: "organiza_club");

            migrationBuilder.DropTable(
                name: "gsm_recharges",
                schema: "organiza_club");

            migrationBuilder.DropTable(
                name: "institutional_benefits",
                schema: "organiza_club");

            migrationBuilder.DropTable(
                name: "institutional_steps",
                schema: "organiza_club");

            migrationBuilder.DropTable(
                name: "integration_audit_events",
                schema: "organiza_club");

            migrationBuilder.DropTable(
                name: "integration_idempotency_records",
                schema: "organiza_club");

            migrationBuilder.DropTable(
                name: "member_effort_allocations",
                schema: "organiza_club");

            migrationBuilder.DropTable(
                name: "notification_preferences",
                schema: "organiza_club");

            migrationBuilder.DropTable(
                name: "notification_runs",
                schema: "organiza_club");

            migrationBuilder.DropTable(
                name: "oauth_authorization_interactions",
                schema: "organiza_club");

            migrationBuilder.DropTable(
                name: "OpenIddictScopes",
                schema: "organiza_club");

            migrationBuilder.DropTable(
                name: "OpenIddictTokens",
                schema: "organiza_club");

            migrationBuilder.DropTable(
                name: "pending_items",
                schema: "organiza_club");

            migrationBuilder.DropTable(
                name: "platform_settings",
                schema: "organiza_club");

            migrationBuilder.DropTable(
                name: "prompt_category_assignments",
                schema: "organiza_club");

            migrationBuilder.DropTable(
                name: "refresh_tokens",
                schema: "organiza_club");

            migrationBuilder.DropTable(
                name: "space_invitations",
                schema: "organiza_club");

            migrationBuilder.DropTable(
                name: "tool_improvement_suggestions",
                schema: "organiza_club");

            migrationBuilder.DropTable(
                name: "user_plan_image_assets",
                schema: "organiza_club");

            migrationBuilder.DropTable(
                name: "user_subscriptions",
                schema: "organiza_club");

            migrationBuilder.DropTable(
                name: "assets",
                schema: "organiza_club");

            migrationBuilder.DropTable(
                name: "credit_card_statements",
                schema: "organiza_club");

            migrationBuilder.DropTable(
                name: "finance_periods",
                schema: "organiza_club");

            migrationBuilder.DropTable(
                name: "finance_recurring_templates",
                schema: "organiza_club");

            migrationBuilder.DropTable(
                name: "gsm_numbers",
                schema: "organiza_club");

            migrationBuilder.DropTable(
                name: "institutional_pages",
                schema: "organiza_club");

            migrationBuilder.DropTable(
                name: "integration_connections",
                schema: "organiza_club");

            migrationBuilder.DropTable(
                name: "OpenIddictAuthorizations",
                schema: "organiza_club");

            migrationBuilder.DropTable(
                name: "activities",
                schema: "organiza_club");

            migrationBuilder.DropTable(
                name: "prompt_categories",
                schema: "organiza_club");

            migrationBuilder.DropTable(
                name: "prompts",
                schema: "organiza_club");

            migrationBuilder.DropTable(
                name: "plan_definitions",
                schema: "organiza_club");

            migrationBuilder.DropTable(
                name: "credit_card_accounts",
                schema: "organiza_club");

            migrationBuilder.DropTable(
                name: "finance_categories",
                schema: "organiza_club");

            migrationBuilder.DropTable(
                name: "OpenIddictApplications",
                schema: "organiza_club");

            migrationBuilder.DropTable(
                name: "projects",
                schema: "organiza_club");

            migrationBuilder.DropTable(
                name: "cores",
                schema: "organiza_club");

            migrationBuilder.DropTable(
                name: "space_members",
                schema: "organiza_club");

            migrationBuilder.DropTable(
                name: "spaces",
                schema: "organiza_club");

            migrationBuilder.DropTable(
                name: "users",
                schema: "organiza_club");
        }
    }
}
