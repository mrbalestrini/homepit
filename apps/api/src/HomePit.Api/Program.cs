using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using System.Net;
using HomePit.Api.Security;
using HomePit.Api.Integrations;
using HomePit.Api.Mcp;
using HomePit.Application;
using HomePit.Application.Auth;
using HomePit.Application.Common;
using HomePit.Application.Finance;
using HomePit.Application.Gsm;
using HomePit.Application.Households;
using HomePit.Application.Institutional;
using HomePit.Application.Integrations;
using HomePit.Application.Platform;
using HomePit.Application.Plans;
using HomePit.Application.Prompts;
using HomePit.Application.Projects;
using HomePit.Infrastructure;
using HomePit.Infrastructure.Auth;
using HomePit.Infrastructure.Data;
using HomePit.Infrastructure.ObjectStorage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore;
using Microsoft.IdentityModel.Tokens;
using ModelContextProtocol.Server;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using OpenIddict.Validation.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUserContext, HttpUserContext>();
builder.Services.AddHomePitApplication();
builder.Services.AddHomePitInfrastructure(builder.Configuration);

var oauthOptions = builder.Configuration.GetSection(OAuthOptions.SectionName).Get<OAuthOptions>() ?? new OAuthOptions();
var oauthEnabled = builder.Configuration.GetValue("Integrations:Enabled", false) &&
    builder.Configuration.GetValue("Mcp:Enabled", false) &&
    !string.IsNullOrWhiteSpace(oauthOptions.SigningKey) &&
    !string.IsNullOrWhiteSpace(oauthOptions.EncryptionKey);
builder.Services.Configure<OAuthOptions>(builder.Configuration.GetSection(OAuthOptions.SectionName));

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    })
    .AddScheme<AuthenticationSchemeOptions, IntegrationTokenAuthenticationHandler>(
        IntegrationTokenAuthenticationHandler.SchemeName,
        _ => { });

if (oauthEnabled)
{
    EnsureOAuthConfiguration(oauthOptions);
    if (builder.Environment.IsProduction() && oauthOptions.TrustedProxies.Length == 0)
    {
        throw new InvalidOperationException("OAuth em produção exige ao menos um proxy confiável para X-Forwarded-Proto.");
    }

    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedProto;
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
        foreach (var proxy in oauthOptions.TrustedProxies)
        {
            if (!IPAddress.TryParse(proxy, out var address))
            {
                throw new InvalidOperationException("OAuth:TrustedProxies deve conter somente endereços IP válidos.");
            }

            options.KnownProxies.Add(address);
        }
    });
    builder.Services.AddOpenIddict()
        .AddCore(options => options.UseEntityFrameworkCore().UseDbContext<HomePitDbContext>())
        .AddServer(options =>
        {
            options.SetIssuer(new Uri(oauthOptions.Issuer))
                .SetAuthorizationEndpointUris("/connect/authorize")
                .SetTokenEndpointUris("/connect/token")
                .SetRevocationEndpointUris("/connect/revocation")
                .AllowAuthorizationCodeFlow()
                .AllowRefreshTokenFlow()
                .RequireProofKeyForCodeExchange()
                .RegisterScopes("homepit.read", "homepit.write")
                .SetAccessTokenLifetime(TimeSpan.FromMinutes(oauthOptions.AccessTokenMinutes))
                .SetRefreshTokenLifetime(TimeSpan.FromDays(oauthOptions.RefreshTokenDays))
                .UseReferenceAccessTokens()
                .UseReferenceRefreshTokens()
                .AddSigningKey(new SymmetricSecurityKey(Convert.FromBase64String(oauthOptions.SigningKey)))
                .AddEncryptionKey(new SymmetricSecurityKey(Convert.FromBase64String(oauthOptions.EncryptionKey)));
            options.UseAspNetCore()
                .EnableAuthorizationEndpointPassthrough();
        })
        .AddValidation(options =>
        {
            options.UseLocalServer();
            options.UseAspNetCore();
            options.EnableTokenEntryValidation();
            options.EnableAuthorizationEntryValidation();
        });
    builder.Services.AddScoped<OAuthConsentService>();
    builder.Services.AddScoped<IAuthorizationHandler, OAuthMcpAuthorizationHandler>();
}

builder.Services.AddAuthorization(options =>
{
    if (oauthEnabled)
    {
        options.AddPolicy("mcp-oauth", policy =>
        {
            policy.AddAuthenticationSchemes(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
            policy.RequireAuthenticatedUser();
            policy.AddRequirements(new OAuthMcpRequirement());
        });
    }
});
builder.Services.AddScoped<IntegrationRestSupport>();
builder.Services.AddMcpServer()
    .WithHttpTransport(options => options.Stateless = true)
    .WithTools<IntegrationMcpTools>()
    .WithResources<IntegrationMcpResources>();
var integrationRequestsPerMinute = builder.Configuration.GetValue("Integrations:RequestsPerMinute", 60);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = (context, _) =>
    {
        context.HttpContext.Response.Headers.RetryAfter = "60";
        return ValueTask.CompletedTask;
    };
    options.AddPolicy("integrations", context =>
    {
        var key = context.User.FindFirst("integration_connection_id")?.Value
            ?? context.Connection.RemoteIpAddress?.ToString()
            ?? "anonymous";
        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = integrationRequestsPerMinute,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        });
    });
    options.AddPolicy("oauth-registration", context =>
    {
        var key = context.Connection.RemoteIpAddress?.ToString() ?? "anonymous";
        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = Math.Max(1, oauthOptions.DynamicRegistrationRequestsPerMinute),
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        });
    });
});
builder.Services.AddCors(options =>
{
    options.AddPolicy("web", policy =>
    {
        var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        if (origins.Length == 0)
        {
            policy.AllowAnyOrigin();
        }
        else
        {
            policy.WithOrigins(origins);
        }

        policy.AllowAnyHeader().AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseForwardedHeaders();
app.UseHomePitErrors();
app.UseCors("web");
app.UseAuthentication();
app.Use(async (context, next) =>
{
    await next(context);
    if (oauthEnabled && context.Request.Path.StartsWithSegments("/mcp") && context.Response.StatusCode == StatusCodes.Status401Unauthorized)
    {
        context.Response.Headers.WWWAuthenticate = $"Bearer resource_metadata=\"{oauthOptions.Issuer.TrimEnd('/')}/.well-known/oauth-protected-resource/mcp\", scope=\"homepit.read homepit.write\"";
    }
});
app.UseAuthorization();
app.UseRateLimiter();
app.UseAccountStateGuard();

var applyMigrationsOnStartup = app.Configuration.GetValue("Database:ApplyMigrationsOnStartup", true);
if (applyMigrationsOnStartup)
{
    await app.Services.MigrateHomePitDatabaseAsync();
}
else
{
    await app.Services.EnsureNoPendingHomePitMigrationsAsync();
}
await app.Services.EnsureHomePitObjectStorageAsync();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/api/system/info", () => Results.Ok(new
{
    name = "HomePit API",
    version = "0.5.0",
    timezone = "America/Sao_Paulo"
}));

if (oauthEnabled)
{
    app.MapGet("/.well-known/oauth-protected-resource/mcp", () => Results.Ok(new
    {
        resource = oauthOptions.CanonicalMcpResource,
        authorization_servers = new[] { oauthOptions.Issuer.TrimEnd('/') },
        scopes_supported = new[] { "homepit.read", "homepit.write" },
        bearer_methods_supported = new[] { "header" }
    }));

    app.MapPost("/connect/register", async (
        DynamicClientRegistrationRequest request,
        IOpenIddictApplicationManager applications,
        CancellationToken cancellationToken) =>
    {
        var clientName = string.IsNullOrWhiteSpace(request.ClientName) ? null : request.ClientName.Trim();
        if (clientName is null || clientName.Length > 160 || request.RedirectUris is null || request.RedirectUris.Count == 0 ||
            (request.GrantTypes is not null && request.GrantTypes.Any(item => item is not "authorization_code" and not "refresh_token")) ||
            (request.ResponseTypes is not null && request.ResponseTypes.Any(item => item != "code")) ||
            (!string.IsNullOrWhiteSpace(request.TokenEndpointAuthMethod) && request.TokenEndpointAuthMethod != "none"))
        {
            throw new ValidationException("O registro dinâmico aceita apenas cliente público com Authorization Code e PKCE.");
        }

        var redirectUris = request.RedirectUris.Select(NormalizeDynamicRedirectUri).Distinct(StringComparer.Ordinal).ToArray();
        var clientId = $"mcp_{Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16)).ToLowerInvariant()}";
        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = clientId,
            DisplayName = clientName,
            ClientType = OpenIddictConstants.ClientTypes.Public,
            ConsentType = OpenIddictConstants.ConsentTypes.Explicit
        };
        foreach (var uri in redirectUris)
        {
            descriptor.RedirectUris.Add(new Uri(uri));
        }
        descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Authorization);
        descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Token);
        descriptor.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode);
        descriptor.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.RefreshToken);
        descriptor.Permissions.Add(OpenIddictConstants.Permissions.ResponseTypes.Code);
        descriptor.Permissions.Add(OpenIddictConstants.Permissions.Prefixes.Scope + "homepit.read");
        descriptor.Permissions.Add(OpenIddictConstants.Permissions.Prefixes.Scope + "homepit.write");
        await applications.CreateAsync(descriptor, cancellationToken);

        return Results.Created($"/connect/register/{clientId}", new
        {
            client_id = clientId,
            client_name = clientName,
            redirect_uris = redirectUris,
            grant_types = new[] { "authorization_code", "refresh_token" },
            response_types = new[] { "code" },
            token_endpoint_auth_method = "none"
        });
    }).RequireRateLimiting("oauth-registration");

    app.MapGet("/connect/authorize", async (
        HttpContext context,
        OAuthConsentService consent,
        IOpenIddictApplicationManager applications,
        IOpenIddictAuthorizationManager authorizations,
        CancellationToken cancellationToken) =>
    {
        var request = context.GetOpenIddictServerRequest() ?? throw new InvalidOperationException("Solicitação OAuth ausente.");
        var interactionToken = request.GetParameter("interaction")?.ToString();
        if (string.IsNullOrWhiteSpace(interactionToken))
        {
            return Results.Redirect(await consent.StartAsync(request, cancellationToken));
        }

        var interaction = await consent.ConsumeApprovedAsync(request, interactionToken, cancellationToken);
        if (interaction.IntegrationConnection is null)
        {
            throw new UnauthorizedException("A conexão OAuth não está disponível.");
        }

        var application = await applications.FindByClientIdAsync(request.ClientId!, cancellationToken)
            ?? throw new UnauthorizedException("Cliente OAuth não encontrado.");
        var identity = new System.Security.Claims.ClaimsIdentity(
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            OpenIddictConstants.Claims.Name,
            OpenIddictConstants.Claims.Role);
        identity.SetClaim(OpenIddictConstants.Claims.Subject, interaction.IntegrationConnection.UserId.ToString());
        identity.SetClaim(OpenIddictConstants.Claims.Name, interaction.IntegrationConnection.Name);
        identity.SetClaim("system_role", "User");
        identity.SetClaim("integration", bool.TrueString);
        identity.SetClaim("integration_connection_id", interaction.IntegrationConnection.Id.ToString());
        identity.SetClaim("integration_household_id", interaction.IntegrationConnection.HouseholdId.ToString());
        identity.SetClaim("integration_access_mode", interaction.IntegrationConnection.AccessMode.ToString());
        identity.SetScopes(interaction.Scope.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        identity.SetResources(oauthOptions.CanonicalMcpResource);
        identity.SetAccessTokenLifetime(TimeSpan.FromMinutes(Math.Min(oauthOptions.AccessTokenMinutes,
            Math.Max(1, (int)Math.Floor((interaction.IntegrationConnection.ExpiresAt - DateTimeOffset.UtcNow).TotalMinutes)))));
        identity.SetRefreshTokenLifetime(TimeSpan.FromDays(Math.Min(oauthOptions.RefreshTokenDays,
            Math.Max(1, (int)Math.Floor((interaction.IntegrationConnection.ExpiresAt - DateTimeOffset.UtcNow).TotalDays)))));
        var authorization = await authorizations.CreateAsync(
            identity,
            interaction.IntegrationConnection.UserId.ToString(),
            await applications.GetIdAsync(application, cancellationToken) ?? throw new UnauthorizedException("Cliente OAuth inválido."),
            OpenIddictConstants.AuthorizationTypes.Permanent,
            identity.GetScopes(),
            cancellationToken);
        interaction.IntegrationConnection.OAuthAuthorizationId = await authorizations.GetIdAsync(authorization, cancellationToken);
        identity.SetAuthorizationId(interaction.IntegrationConnection.OAuthAuthorizationId);
        identity.SetDestinations(_ => new[] { OpenIddictConstants.Destinations.AccessToken });
        await context.RequestServices.GetRequiredService<HomePitDbContext>().SaveChangesAsync(cancellationToken);
        return Results.SignIn(new System.Security.Claims.ClaimsPrincipal(identity), properties: null,
            authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    });
}

var auth = app.MapGroup("/api/auth");
auth.MapPost("/register", async (RegisterRequest request, AuthService service, CancellationToken cancellationToken) =>
    Results.Created("/api/households", await service.RegisterAsync(request, cancellationToken)));
auth.MapPost("/login", async (LoginRequest request, AuthService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.LoginAsync(request, cancellationToken)));
auth.MapPost("/refresh", async (RefreshRequest request, AuthService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.RefreshAsync(request, cancellationToken)));

app.MapGet("/api/institutional-page", async (
    HttpContext context,
    InstitutionalPageService service,
    CancellationToken cancellationToken) =>
{
    context.Response.Headers.CacheControl = "no-store";
    return Results.Ok(await service.GetPublicAsync(cancellationToken));
});
app.MapGet("/api/institutional-page/images/{slot}", async (
    string slot,
    HttpContext context,
    InstitutionalPageService service,
    CancellationToken cancellationToken) =>
{
    var image = await service.GetImageAsync(slot, cancellationToken);
    context.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
    return Results.File(image.Content, image.ContentType);
});
app.MapGet("/api/platform-settings", async (
    HttpContext context,
    PlatformSettingsService service,
    CancellationToken cancellationToken) =>
{
    context.Response.Headers.CacheControl = "no-store";
    return Results.Ok(await service.GetPublicAsync(cancellationToken));
});
app.MapGet("/api/plans", async (CommercialPlanService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.ListPublicPlansAsync(cancellationToken)));

var api = app.MapGroup("/api").RequireAuthorization();

if (oauthEnabled)
{
    var oauthConsent = api.MapGroup("/oauth/consent");
    oauthConsent.MapGet("/{interaction}", async (string interaction, OAuthConsentService service, CancellationToken cancellationToken) =>
        Results.Ok(await service.GetAsync(interaction, cancellationToken)));
    oauthConsent.MapPost("/{interaction}/approve", async (string interaction, ApproveOAuthConsentRequest request, OAuthConsentService service, CancellationToken cancellationToken) =>
        Results.Ok(await service.ApproveAsync(interaction, request, cancellationToken)));
    oauthConsent.MapPost("/{interaction}/deny", async (string interaction, OAuthConsentService service, CancellationToken cancellationToken) =>
        Results.Ok(await service.DenyAsync(interaction, cancellationToken)));
}

api.MapGet("/admin/institutional-page", async (
    InstitutionalPageService service,
    CancellationToken cancellationToken) =>
        Results.Ok(await service.GetAdminAsync(cancellationToken)));
api.MapPut("/admin/institutional-page", async (
    UpdateInstitutionalPageRequest request,
    InstitutionalPageService service,
    CancellationToken cancellationToken) =>
        Results.Ok(await service.UpdateAsync(request, cancellationToken)));
api.MapPost("/admin/institutional-page/images/{slot}", async (
    string slot,
    HttpRequest request,
    InstitutionalPageService service,
    CancellationToken cancellationToken) =>
{
    if (!request.HasFormContentType)
    {
        throw new ValidationException("Envie a imagem institucional em multipart/form-data.");
    }

    var form = await request.ReadFormAsync(cancellationToken);
    var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
    if (file is null)
    {
        throw new ValidationException("Selecione uma imagem para a página institucional.");
    }

    await using var stream = file.OpenReadStream();
    return Results.Ok(await service.UploadImageAsync(slot, stream, file.Length, file.ContentType, cancellationToken));
});
api.MapDelete("/admin/institutional-page/images/{slot}", async (
    string slot,
    InstitutionalPageService service,
    CancellationToken cancellationToken) =>
        Results.Ok(await service.DeleteImageAsync(slot, cancellationToken)));
api.MapGet("/admin/platform/settings", async (
    PlatformSettingsService service,
    CancellationToken cancellationToken) =>
        Results.Ok(await service.GetAdminAsync(cancellationToken)));
api.MapPut("/admin/platform/settings", async (
    UpdatePlatformSettingsRequest request,
    PlatformSettingsService service,
    CancellationToken cancellationToken) =>
        Results.Ok(await service.UpdateAsync(request, cancellationToken)));

api.MapGet("/households", async (HouseholdService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.ListAsync(cancellationToken)));
api.MapPost("/households", async (CreateHouseholdRequest request, HouseholdService service, CancellationToken cancellationToken) =>
    Results.Created("/api/households", await service.CreateAsync(request, cancellationToken)));
api.MapPut("/households/{id:guid}", async (
    Guid id,
    UpdateHouseholdRequest request,
    HouseholdService service,
    CancellationToken cancellationToken) =>
        Results.Ok(await service.UpdateAsync(id, request, cancellationToken)));
api.MapDelete("/households/{id:guid}", async (
    Guid id,
    HouseholdService service,
    CancellationToken cancellationToken) =>
{
    await service.DeleteAsync(id, cancellationToken);
    return Results.NoContent();
});
api.MapGet("/households/members", async (HouseholdService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.ListMembersAsync(cancellationToken)));
api.MapPost("/households/share", async (ShareHouseholdRequest request, HouseholdService service, CancellationToken cancellationToken) =>
    Results.Created("/api/households/invitations", await service.ShareAsync(request, cancellationToken)));
api.MapGet("/households/invitations", async (HouseholdService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.ListInvitationsAsync(cancellationToken)));
api.MapPost("/households/invitations/{id:guid}/accept", async (
    Guid id,
    HouseholdService service,
    CancellationToken cancellationToken) =>
        Results.Ok(await service.AcceptInvitationAsync(id, cancellationToken)));
api.MapPost("/households/invitations/{id:guid}/decline", async (
    Guid id,
    HouseholdService service,
    CancellationToken cancellationToken) =>
{
    await service.DeclineInvitationAsync(id, cancellationToken);
    return Results.NoContent();
});
api.MapPut("/households/members/{id:guid}", async (
    Guid id,
    UpdateHouseholdMemberRequest request,
    HouseholdService service,
    CancellationToken cancellationToken) =>
        Results.Ok(await service.UpdateMemberAsync(id, request, cancellationToken)));
api.MapDelete("/households/members/{id:guid}", async (
    Guid id,
    HouseholdService service,
    CancellationToken cancellationToken) =>
{
    await service.RemoveMemberAsync(id, cancellationToken);
    return Results.NoContent();
});
api.MapPut("/users/me", async (UpdateProfileRequest request, AuthService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.UpdateProfileAsync(request, cancellationToken)));
api.MapDelete("/users/me", async (AuthService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.DeleteOwnAccountAsync(cancellationToken)));
api.MapPost("/users/me/reactivate", async (AuthService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.ReactivateOwnAccountAsync(cancellationToken)));
api.MapGet("/users/me/plan", async (CommercialPlanService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.GetCurrentUserPlanAsync(cancellationToken)));
api.MapGet("/users/me/plan/creations/{scope}", async (
    string scope,
    CommercialPlanService service,
    CancellationToken cancellationToken) =>
        Results.Ok(await service.ListCurrentUserCreationsAsync(scope, cancellationToken)));
api.MapPost("/users/me/tool-improvement-suggestions", async (
    CreateToolImprovementSuggestionRequest request,
    ToolImprovementSuggestionService service,
    CancellationToken cancellationToken) =>
        Results.Created("/api/users/me/tool-improvement-suggestions", await service.SubmitAsync(request, cancellationToken)));
api.MapGet("/users/me/integration-connections", async (IntegrationConnectionService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.ListCurrentUserAsync(cancellationToken)));
api.MapPost("/users/me/integration-connections", async (
    CreateIntegrationConnectionRequest request,
    HttpContext context,
    IntegrationConnectionService service,
    CancellationToken cancellationToken) =>
{
    var created = await service.CreateAsync(request, cancellationToken);
    context.Response.Headers.CacheControl = "no-store";
    return Results.Created($"/api/users/me/integration-connections/{created.Connection.Id}", created);
});
api.MapPost("/users/me/integration-connections/{id:guid}/revoke", async (
    Guid id,
    IntegrationConnectionService service,
    IServiceProvider services,
    CancellationToken cancellationToken) =>
{
    var authorizationId = await service.RevokeCurrentUserConnectionAsync(id, cancellationToken);
    if (oauthEnabled && !string.IsNullOrWhiteSpace(authorizationId))
    {
        var authorizations = services.GetRequiredService<IOpenIddictAuthorizationManager>();
        await authorizations.TryRevokeAsync(authorizationId, cancellationToken);
    }
    return Results.NoContent();
});
api.MapPost("/users/me/profile-photo", async (
    HttpRequest request,
    AuthService service,
    CancellationToken cancellationToken) =>
{
    if (!request.HasFormContentType)
    {
        throw new ValidationException("Envie a foto de perfil em multipart/form-data.");
    }

    var form = await request.ReadFormAsync(cancellationToken);
    var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
    if (file is null)
    {
        throw new ValidationException("Selecione uma imagem para a foto de perfil.");
    }

    await using var stream = file.OpenReadStream();
    return Results.Ok(await service.UploadProfilePhotoAsync(stream, file.Length, file.ContentType, cancellationToken));
});
api.MapGet("/users/me/profile-photo", async (
    HttpContext context,
    AuthService service,
    CancellationToken cancellationToken) =>
{
    var photo = await service.GetProfilePhotoAsync(cancellationToken);
    context.Response.Headers.CacheControl = "no-store";
    return Results.File(photo.Content, photo.ContentType);
});
api.MapGet("/users/{userId:guid}/profile-photo", async (
    Guid userId,
    HttpContext context,
    AuthService service,
    CancellationToken cancellationToken) =>
{
    var photo = await service.GetProfilePhotoAsync(userId, cancellationToken);
    context.Response.Headers.CacheControl = "no-store";
    return Results.File(photo.Content, photo.ContentType);
});
api.MapGet("/admin/users", async (AuthService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.ListAdminUsersAsync(cancellationToken)));
api.MapPost("/admin/users/{id:guid}/deactivate", async (
    Guid id,
    AuthService service,
    CancellationToken cancellationToken) =>
        Results.Ok(await service.DeactivateUserAsSuperAdminAsync(id, cancellationToken)));
api.MapPost("/admin/users/{id:guid}/reactivate", async (
    Guid id,
    AuthService service,
    CancellationToken cancellationToken) =>
        Results.Ok(await service.ReactivateUserAsSuperAdminAsync(id, cancellationToken)));
api.MapDelete("/admin/users/{id:guid}", async (
    Guid id,
    AuthService service,
    CancellationToken cancellationToken) =>
{
    await service.DeleteUserAsSuperAdminAsync(id, cancellationToken);
    return Results.NoContent();
});
api.MapGet("/admin/platform/plans", async (CommercialPlanService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.ListPlansAsync(cancellationToken)));
api.MapPut("/admin/platform/plans/{id:guid}", async (
    Guid id,
    UpdatePlanDefinitionRequest request,
    CommercialPlanService service,
    CancellationToken cancellationToken) =>
        Results.Ok(await service.UpdatePlanAsync(id, request, cancellationToken)));
api.MapGet("/admin/platform/subscriptions", async (CommercialPlanService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.ListSubscriptionsAsync(cancellationToken)));
api.MapPost("/admin/platform/subscriptions", async (
    CreateUserSubscriptionRequest request,
    CommercialPlanService service,
    CancellationToken cancellationToken) =>
        Results.Created("/api/admin/platform/subscriptions", await service.CreateSubscriptionAsync(request, cancellationToken)));
api.MapPut("/admin/platform/subscriptions/{id:guid}", async (
    Guid id,
    UpdateUserSubscriptionRequest request,
    CommercialPlanService service,
    CancellationToken cancellationToken) =>
        Results.Ok(await service.UpdateSubscriptionAsync(id, request, cancellationToken)));
api.MapGet("/admin/platform/tool-improvement-suggestions", async (
    ToolImprovementSuggestionService service,
    CancellationToken cancellationToken) =>
        Results.Ok(await service.ListAdminAsync(cancellationToken)));
api.MapPut("/admin/platform/tool-improvement-suggestions/{id:guid}", async (
    Guid id,
    UpdateToolImprovementSuggestionRequest request,
    ToolImprovementSuggestionService service,
    CancellationToken cancellationToken) =>
        Results.Ok(await service.UpdateAsync(id, request, cancellationToken)));
api.MapPost("/admin/platform/tool-improvement-suggestions/bulk-update", async (
    BulkUpdateToolImprovementSuggestionsRequest request,
    ToolImprovementSuggestionService service,
    CancellationToken cancellationToken) =>
        Results.Ok(await service.BulkUpdateAsync(request, cancellationToken)));
var finance = api.MapGroup("/finance");

finance.MapGet("/periods", async (FinanceService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.ListPeriodsAsync(cancellationToken)));
finance.MapGet("/periods/{year:int}/{month:int}", async (
    int year,
    int month,
    FinanceService service,
    CancellationToken cancellationToken) =>
        Results.Ok(await service.GetPeriodAsync(year, month, cancellationToken)));
finance.MapPost("/periods/{year:int}/{month:int}/generate", async (
    int year,
    int month,
    GenerateFinancePeriodRequest request,
    FinanceService service,
    CancellationToken cancellationToken) =>
        Results.Ok(await service.GeneratePeriodAsync(year, month, request, cancellationToken)));

finance.MapGet("/categories", async (FinanceService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.ListCategoriesAsync(cancellationToken)));
finance.MapPost("/categories", async (
    CreateFinanceCategoryRequest request,
    FinanceService service,
    CancellationToken cancellationToken) =>
        Results.Created("/api/finance/categories", await service.CreateCategoryAsync(request, cancellationToken)));
finance.MapPut("/categories/{id:guid}", async (
    Guid id,
    UpdateFinanceCategoryRequest request,
    FinanceService service,
    CancellationToken cancellationToken) =>
        Results.Ok(await service.UpdateCategoryAsync(id, request, cancellationToken)));
finance.MapDelete("/categories/{id:guid}", async (
    Guid id,
    FinanceService service,
    CancellationToken cancellationToken) =>
{
    await service.DeleteCategoryAsync(id, cancellationToken);
    return Results.NoContent();
});

finance.MapGet("/recurring-templates", async (FinanceService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.ListRecurringTemplatesAsync(cancellationToken)));
finance.MapPost("/recurring-templates", async (
    CreateFinanceRecurringTemplateRequest request,
    FinanceService service,
    CancellationToken cancellationToken) =>
        Results.Created("/api/finance/recurring-templates", await service.CreateRecurringTemplateAsync(request, cancellationToken)));
finance.MapPut("/recurring-templates/{id:guid}", async (
    Guid id,
    UpdateFinanceRecurringTemplateRequest request,
    FinanceService service,
    CancellationToken cancellationToken) =>
        Results.Ok(await service.UpdateRecurringTemplateAsync(id, request, cancellationToken)));
finance.MapDelete("/recurring-templates/{id:guid}", async (
    Guid id,
    FinanceService service,
    CancellationToken cancellationToken) =>
{
    await service.DeleteRecurringTemplateAsync(id, cancellationToken);
    return Results.NoContent();
});

finance.MapGet("/entries", async (
    HttpRequest request,
    FinanceService service,
    CancellationToken cancellationToken) =>
        Results.Ok(await service.ListEntriesAsync(
            ReadOptionalIntQuery(request, "year"),
            ReadOptionalIntQuery(request, "month"),
            cancellationToken)));
finance.MapPost("/entries", async (
    CreateFinanceEntryRequest request,
    FinanceService service,
    CancellationToken cancellationToken) =>
        Results.Created("/api/finance/entries", await service.CreateEntryAsync(request, cancellationToken)));
finance.MapPut("/entries/{id:guid}", async (
    Guid id,
    UpdateFinanceEntryRequest request,
    FinanceService service,
    CancellationToken cancellationToken) =>
        Results.Ok(await service.UpdateEntryAsync(id, request, cancellationToken)));
finance.MapDelete("/entries/{id:guid}", async (
    Guid id,
    FinanceService service,
    CancellationToken cancellationToken) =>
{
    await service.DeleteEntryAsync(id, cancellationToken);
    return Results.NoContent();
});

finance.MapGet("/assets", async (FinanceService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.ListAssetsAsync(cancellationToken)));
finance.MapPost("/assets", async (
    CreateAssetRequest request,
    FinanceService service,
    CancellationToken cancellationToken) =>
        Results.Created("/api/finance/assets", await service.CreateAssetAsync(request, cancellationToken)));
finance.MapPut("/assets/{id:guid}", async (
    Guid id,
    UpdateAssetRequest request,
    FinanceService service,
    CancellationToken cancellationToken) =>
        Results.Ok(await service.UpdateAssetAsync(id, request, cancellationToken)));
finance.MapDelete("/assets/{id:guid}", async (
    Guid id,
    FinanceService service,
    CancellationToken cancellationToken) =>
{
    await service.DeleteAssetAsync(id, cancellationToken);
    return Results.NoContent();
});
finance.MapGet("/assets/{id:guid}/valuations", async (
    Guid id,
    FinanceService service,
    CancellationToken cancellationToken) =>
        Results.Ok(await service.ListAssetValuationsAsync(id, cancellationToken)));
finance.MapPost("/assets/{id:guid}/valuations", async (
    Guid id,
    CreateAssetValuationRequest request,
    FinanceService service,
    CancellationToken cancellationToken) =>
        Results.Created($"/api/finance/assets/{id}/valuations", await service.CreateAssetValuationAsync(id, request, cancellationToken)));
finance.MapPut("/assets/{id:guid}/valuations/{valuationId:guid}", async (
    Guid id,
    Guid valuationId,
    UpdateAssetValuationRequest request,
    FinanceService service,
    CancellationToken cancellationToken) =>
        Results.Ok(await service.UpdateAssetValuationAsync(id, valuationId, request, cancellationToken)));
finance.MapDelete("/assets/{id:guid}/valuations/{valuationId:guid}", async (
    Guid id,
    Guid valuationId,
    FinanceService service,
    CancellationToken cancellationToken) =>
{
    await service.DeleteAssetValuationAsync(id, valuationId, cancellationToken);
    return Results.NoContent();
});

finance.MapGet("/credit-cards", async (FinanceService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.ListCreditCardAccountsAsync(cancellationToken)));
finance.MapPost("/credit-cards", async (
    CreateCreditCardAccountRequest request,
    FinanceService service,
    CancellationToken cancellationToken) =>
        Results.Created("/api/finance/credit-cards", await service.CreateCreditCardAccountAsync(request, cancellationToken)));
finance.MapPut("/credit-cards/{id:guid}", async (
    Guid id,
    UpdateCreditCardAccountRequest request,
    FinanceService service,
    CancellationToken cancellationToken) =>
        Results.Ok(await service.UpdateCreditCardAccountAsync(id, request, cancellationToken)));
finance.MapDelete("/credit-cards/{id:guid}", async (
    Guid id,
    FinanceService service,
    CancellationToken cancellationToken) =>
{
    await service.DeleteCreditCardAccountAsync(id, cancellationToken);
    return Results.NoContent();
});
finance.MapGet("/credit-cards/{id:guid}/transactions", async (
    Guid id,
    FinanceService service,
    CancellationToken cancellationToken) =>
        Results.Ok(await service.ListCreditCardTransactionsAsync(id, cancellationToken)));
finance.MapPost("/credit-cards/{id:guid}/transactions", async (
    Guid id,
    CreateCreditCardTransactionRequest request,
    FinanceService service,
    CancellationToken cancellationToken) =>
        Results.Created($"/api/finance/credit-cards/{id}/transactions", await service.CreateCreditCardTransactionAsync(id, request, cancellationToken)));
finance.MapPost("/credit-cards/{id:guid}/transactions/import", async (
    Guid id,
    ImportCreditCardTransactionsRequest request,
    FinanceService service,
    CancellationToken cancellationToken) =>
        Results.Ok(await service.ImportCreditCardTransactionsAsync(id, request, cancellationToken)));
finance.MapPut("/credit-cards/{id:guid}/transactions/{transactionId:guid}", async (
    Guid id,
    Guid transactionId,
    UpdateCreditCardTransactionRequest request,
    FinanceService service,
    CancellationToken cancellationToken) =>
        Results.Ok(await service.UpdateCreditCardTransactionAsync(id, transactionId, request, cancellationToken)));
finance.MapDelete("/credit-cards/{id:guid}/transactions/{transactionId:guid}", async (
    Guid id,
    Guid transactionId,
    FinanceService service,
    CancellationToken cancellationToken) =>
{
    await service.DeleteCreditCardTransactionAsync(id, transactionId, cancellationToken);
    return Results.NoContent();
});
finance.MapGet("/credit-cards/{id:guid}/statements", async (
    Guid id,
    FinanceService service,
    CancellationToken cancellationToken) =>
        Results.Ok(await service.ListCreditCardStatementsAsync(id, cancellationToken)));
finance.MapPost("/credit-cards/{id:guid}/statements", async (
    Guid id,
    CreateCreditCardStatementRequest request,
    FinanceService service,
    CancellationToken cancellationToken) =>
        Results.Created($"/api/finance/credit-cards/{id}/statements", await service.CreateCreditCardStatementAsync(id, request, cancellationToken)));
finance.MapPut("/credit-cards/{id:guid}/statements/{statementId:guid}", async (
    Guid id,
    Guid statementId,
    UpdateCreditCardStatementRequest request,
    FinanceService service,
    CancellationToken cancellationToken) =>
        Results.Ok(await service.UpdateCreditCardStatementAsync(id, statementId, request, cancellationToken)));
finance.MapDelete("/credit-cards/{id:guid}/statements/{statementId:guid}", async (
    Guid id,
    Guid statementId,
    FinanceService service,
    CancellationToken cancellationToken) =>
{
    await service.DeleteCreditCardStatementAsync(id, statementId, cancellationToken);
    return Results.NoContent();
});

api.MapGet("/gsm-numbers", async (GsmNumberService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.ListAsync(cancellationToken)));
api.MapPost("/gsm-numbers", async (
    CreateGsmNumberRequest request,
    GsmNumberService service,
    CancellationToken cancellationToken) =>
        Results.Created("/api/gsm-numbers", await service.CreateAsync(request, cancellationToken)));
api.MapPut("/gsm-numbers/{id:guid}", async (
    Guid id,
    UpdateGsmNumberRequest request,
    GsmNumberService service,
    CancellationToken cancellationToken) =>
        Results.Ok(await service.UpdateAsync(id, request, cancellationToken)));
api.MapGet("/gsm-numbers/{id:guid}/recharges", async (
    Guid id,
    GsmNumberService service,
    CancellationToken cancellationToken) =>
        Results.Ok(await service.ListRechargesAsync(id, cancellationToken)));
api.MapPost("/gsm-numbers/{id:guid}/recharges", async (
    Guid id,
    CreateGsmRechargeRequest request,
    GsmNumberService service,
    CancellationToken cancellationToken) =>
        Results.Created($"/api/gsm-numbers/{id}/recharges", await service.CreateRechargeAsync(id, request, cancellationToken)));
api.MapPut("/gsm-numbers/{id:guid}/recharges/{rechargeId:guid}", async (
    Guid id,
    Guid rechargeId,
    UpdateGsmRechargeRequest request,
    GsmNumberService service,
    CancellationToken cancellationToken) =>
        Results.Ok(await service.UpdateRechargeAsync(id, rechargeId, request, cancellationToken)));
api.MapDelete("/gsm-numbers/{id:guid}/recharges/{rechargeId:guid}", async (
    Guid id,
    Guid rechargeId,
    GsmNumberService service,
    CancellationToken cancellationToken) =>
{
    await service.DeleteRechargeAsync(id, rechargeId, cancellationToken);
    return Results.NoContent();
});
api.MapDelete("/gsm-numbers/{id:guid}", async (
    Guid id,
    GsmNumberService service,
    CancellationToken cancellationToken) =>
{
    await service.DeleteAsync(id, cancellationToken);
    return Results.NoContent();
});

api.MapGet("/universes", async (ProjectService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.ListUniversesAsync(cancellationToken)));
api.MapPost("/universes", async (CreateUniverseRequest request, ProjectService service, CancellationToken cancellationToken) =>
    Results.Created("/api/universes", await service.CreateUniverseAsync(request, cancellationToken)));
api.MapPut("/universes/{id:guid}", async (
    Guid id,
    UpdateUniverseRequest request,
    ProjectService service,
    CancellationToken cancellationToken) =>
        Results.Ok(await service.UpdateUniverseAsync(id, request, cancellationToken)));
api.MapDelete("/universes/{id:guid}", async (
    Guid id,
    ProjectService service,
    CancellationToken cancellationToken) =>
{
    await service.DeleteUniverseAsync(id, cancellationToken);
    return Results.NoContent();
});
api.MapPost("/universes/{id:guid}/image", async (
    Guid id,
    HttpRequest request,
    ProjectService service,
    CancellationToken cancellationToken) =>
{
    if (!request.HasFormContentType)
    {
        throw new ValidationException("Envie a imagem do universo em multipart/form-data.");
    }

    var form = await request.ReadFormAsync(cancellationToken);
    var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
    if (file is null)
    {
        throw new ValidationException("Selecione uma imagem para o universo.");
    }

    await using var stream = file.OpenReadStream();
    return Results.Ok(await service.UploadUniverseImageAsync(id, stream, file.Length, file.ContentType, cancellationToken));
});
api.MapGet("/universes/{id:guid}/image", async (
    Guid id,
    HttpContext context,
    ProjectService service,
    CancellationToken cancellationToken) =>
{
    var image = await service.GetUniverseImageAsync(id, cancellationToken);
    context.Response.Headers.CacheControl = "no-store";
    return Results.File(image.Content, image.ContentType);
});
api.MapDelete("/universes/{id:guid}/image", async (
    Guid id,
    ProjectService service,
    CancellationToken cancellationToken) =>
        Results.Ok(await service.DeleteUniverseImageAsync(id, cancellationToken)));

api.MapGet("/projects", async (Guid? universeId, ProjectService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.ListProjectsAsync(universeId, cancellationToken)));
api.MapPost("/projects", async (CreateProjectRequest request, ProjectService service, CancellationToken cancellationToken) =>
    Results.Created("/api/projects", await service.CreateProjectAsync(request, cancellationToken)));
api.MapPut("/projects/{id:guid}", async (
    Guid id,
    UpdateProjectRequest request,
    ProjectService service,
    CancellationToken cancellationToken) =>
        Results.Ok(await service.UpdateProjectAsync(id, request, cancellationToken)));
api.MapDelete("/projects/{id:guid}", async (
    Guid id,
    ProjectService service,
    CancellationToken cancellationToken) =>
{
    await service.DeleteProjectAsync(id, cancellationToken);
    return Results.NoContent();
});

api.MapGet("/activities", async (
    Guid? projectId,
    HomePit.Domain.Projects.ActivityStatus? status,
    ProjectService service,
    CancellationToken cancellationToken) =>
        Results.Ok(await service.ListActivitiesAsync(projectId, status, cancellationToken)));
api.MapGet("/activities/relevance", async (
    DateOnly date,
    int utcOffsetMinutes,
    EffortPlanningService service,
    CancellationToken cancellationToken) =>
    Results.Ok(await service.GetRelevanceAsync(date, utcOffsetMinutes, cancellationToken)));
api.MapGet("/effort-plan", async (EffortPlanningService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.GetPlanAsync(cancellationToken)));
api.MapPut("/effort-plan", async (
    UpdateEffortPlanRequest request,
    EffortPlanningService service,
    CancellationToken cancellationToken) =>
    Results.Ok(await service.UpdatePlanAsync(request, cancellationToken)));
api.MapPost("/activities", async (CreateActivityRequest request, ProjectService service, CancellationToken cancellationToken) =>
    Results.Created("/api/activities", await service.CreateActivityAsync(request, cancellationToken)));
api.MapPut("/activities/{id:guid}", async (
    Guid id,
    UpdateActivityRequest request,
    ProjectService service,
    CancellationToken cancellationToken) =>
        Results.Ok(await service.UpdateActivityAsync(id, request, cancellationToken)));
api.MapDelete("/activities/{id:guid}", async (
    Guid id,
    ProjectService service,
    CancellationToken cancellationToken) =>
{
    await service.DeleteActivityAsync(id, cancellationToken);
    return Results.NoContent();
});
api.MapPatch("/activities/{id:guid}/status", async (
    Guid id,
    UpdateActivityStatusRequest request,
    ProjectService service,
    CancellationToken cancellationToken) =>
        Results.Ok(await service.UpdateActivityStatusAsync(id, request, cancellationToken)));
api.MapPost("/activities/{id:guid}/image", async (
    Guid id,
    HttpRequest request,
    ProjectService service,
    CancellationToken cancellationToken) =>
{
    if (!request.HasFormContentType)
    {
        throw new ValidationException("Envie a imagem da atividade em multipart/form-data.");
    }

    var form = await request.ReadFormAsync(cancellationToken);
    var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
    if (file is null)
    {
        throw new ValidationException("Selecione uma imagem para a atividade.");
    }

    await using var stream = file.OpenReadStream();
    return Results.Ok(await service.UploadActivityImageAsync(id, stream, file.Length, file.ContentType, cancellationToken));
});
api.MapGet("/activities/{id:guid}/image", async (
    Guid id,
    HttpContext context,
    ProjectService service,
    CancellationToken cancellationToken) =>
{
    var image = await service.GetActivityImageAsync(id, cancellationToken);
    context.Response.Headers.CacheControl = "no-store";
    return Results.File(image.Content, image.ContentType);
});
api.MapDelete("/activities/{id:guid}/image", async (
    Guid id,
    ProjectService service,
    CancellationToken cancellationToken) =>
        Results.Ok(await service.DeleteActivityImageAsync(id, cancellationToken)));
api.MapGet("/activities/{id:guid}/comments", async (
    Guid id,
    ProjectService service,
    CancellationToken cancellationToken) =>
        Results.Ok(await service.ListActivityCommentsAsync(id, cancellationToken)));
api.MapPost("/activities/{id:guid}/comments", async (
    Guid id,
    CreateActivityCommentRequest request,
    ProjectService service,
    CancellationToken cancellationToken) =>
        Results.Created($"/api/activities/{id}/comments", await service.CreateActivityCommentAsync(id, request, cancellationToken)));
api.MapPut("/activities/{activityId:guid}/comments/{commentId:guid}", async (
    Guid activityId,
    Guid commentId,
    UpdateActivityCommentRequest request,
    ProjectService service,
    CancellationToken cancellationToken) =>
        Results.Ok(await service.UpdateActivityCommentAsync(activityId, commentId, request, cancellationToken)));
api.MapDelete("/activities/{activityId:guid}/comments/{commentId:guid}", async (
    Guid activityId,
    Guid commentId,
    ProjectService service,
    CancellationToken cancellationToken) =>
{
    await service.DeleteActivityCommentAsync(activityId, commentId, cancellationToken);
    return Results.NoContent();
});

api.MapGet("/activities/{id:guid}/pending-items", async (
    Guid id,
    ProjectService service,
    CancellationToken cancellationToken) =>
        Results.Ok(await service.ListPendingItemsAsync(id, cancellationToken)));
api.MapPost("/activities/{id:guid}/pending-items", async (
    Guid id,
    CreatePendingItemRequest request,
    ProjectService service,
    CancellationToken cancellationToken) =>
        Results.Created($"/api/activities/{id}/pending-items", await service.CreatePendingItemAsync(id, request, cancellationToken)));

api.MapGet("/prompts", async (
    HttpRequest request,
    PromptService service,
    CancellationToken cancellationToken) =>
        Results.Ok(await service.ListPromptsAsync(
            ReadOptionalQueryString(request, "search"),
            ReadOptionalGuidQuery(request, "universeId"),
            ReadOptionalBoolQuery(request, "withoutUniverse") ?? false,
            ReadOptionalBoolQuery(request, "archivedOnly") ?? false,
            ReadGuidCollectionQuery(request, "categoryId"),
            ReadOptionalIntQuery(request, "page") ?? 1,
            ReadOptionalIntQuery(request, "pageSize") ?? 12,
            cancellationToken)));
api.MapGet("/prompts/{id:guid}", async (Guid id, PromptService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.GetPromptAsync(id, cancellationToken)));
api.MapPost("/prompts", async (CreatePromptRequest request, PromptService service, CancellationToken cancellationToken) =>
    Results.Created("/api/prompts", await service.CreatePromptAsync(request, cancellationToken)));
api.MapPut("/prompts/{id:guid}", async (
    Guid id,
    UpdatePromptRequest request,
    PromptService service,
    CancellationToken cancellationToken) =>
        Results.Ok(await service.UpdatePromptAsync(id, request, cancellationToken)));
api.MapDelete("/prompts/{id:guid}", async (
    Guid id,
    PromptService service,
    CancellationToken cancellationToken) =>
{
    await service.DeletePromptAsync(id, cancellationToken);
    return Results.NoContent();
});
api.MapPost("/prompts/{id:guid}/archive", async (
    Guid id,
    PromptService service,
    CancellationToken cancellationToken) =>
        Results.Ok(await service.ArchivePromptAsync(id, cancellationToken)));
api.MapDelete("/prompts/{id:guid}/archive", async (
    Guid id,
    PromptService service,
    CancellationToken cancellationToken) =>
        Results.Ok(await service.UnarchivePromptAsync(id, cancellationToken)));
api.MapPost("/prompts/{id:guid}/image", async (
    Guid id,
    HttpRequest request,
    PromptService service,
    CancellationToken cancellationToken) =>
{
    if (!request.HasFormContentType)
    {
        throw new ValidationException("Envie a imagem do prompt em multipart/form-data.");
    }

    var form = await request.ReadFormAsync(cancellationToken);
    var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
    if (file is null)
    {
        throw new ValidationException("Selecione uma imagem para o prompt.");
    }

    await using var stream = file.OpenReadStream();
    return Results.Ok(await service.UploadPromptImageAsync(id, stream, file.Length, file.ContentType, cancellationToken));
});
api.MapGet("/prompts/{id:guid}/image", async (
    Guid id,
    HttpContext context,
    PromptService service,
    CancellationToken cancellationToken) =>
{
    var image = await service.GetPromptImageAsync(id, cancellationToken);
    context.Response.Headers.CacheControl = "no-store";
    return Results.File(image.Content, image.ContentType);
});
api.MapDelete("/prompts/{id:guid}/image", async (
    Guid id,
    PromptService service,
    CancellationToken cancellationToken) =>
        Results.Ok(await service.DeletePromptImageAsync(id, cancellationToken)));

api.MapGet("/prompt-categories", async (PromptService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.ListCategoriesAsync(cancellationToken)));
api.MapPost("/prompt-categories", async (CreatePromptCategoryRequest request, PromptService service, CancellationToken cancellationToken) =>
    Results.Created("/api/prompt-categories", await service.CreateCategoryAsync(request, cancellationToken)));
api.MapPut("/prompt-categories/{id:guid}", async (
    Guid id,
    UpdatePromptCategoryRequest request,
    PromptService service,
    CancellationToken cancellationToken) =>
        Results.Ok(await service.UpdateCategoryAsync(id, request, cancellationToken)));
api.MapDelete("/prompt-categories/{id:guid}", async (
    Guid id,
    Guid? replacementCategoryId,
    PromptService service,
    CancellationToken cancellationToken) =>
{
    await service.DeleteCategoryAsync(id, replacementCategoryId, cancellationToken);
    return Results.NoContent();
});

// This surface is deliberately separate from the web API. A connection carries its
// household context, so callers cannot select a household with X-Household-Id.
var integrations = app.MapGroup("/api/integrations/v1")
    .RequireAuthorization(new AuthorizeAttribute
    {
        AuthenticationSchemes = IntegrationTokenAuthenticationHandler.SchemeName
    })
    .RequireRateLimiting("integrations")
    .AddEndpointFilter(IntegrationRequestGuard.InvokeAsync)
    .AddEndpointFilter(IntegrationConcurrencyFilter.InvokeAsync)
    .AddEndpointFilter(IntegrationAuditFilter.InvokeAsync);

integrations.MapGet("/space", async (IntegrationConnectionService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.GetCurrentSpaceAsync(cancellationToken)));

var integrationFinance = integrations.MapGroup("/finance");
integrationFinance.MapGet("/periods", async (HttpRequest request, FinanceService service, IntegrationRestSupport rest, HomePitDbContext db, CancellationToken cancellationToken) =>
    Results.Ok(await rest.PageAsync(await service.ListPeriodsAsync(cancellationToken), item => item.Id, db.FinancePeriods,
        "finance.periods", ReadOptionalQueryString(request, "cursor"), ReadOptionalIntQuery(request, "limit"), cancellationToken)));
integrationFinance.MapGet("/periods/{year:int}/{month:int}", async (int year, int month, FinanceService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.GetPeriodAsync(year, month, cancellationToken)));
integrationFinance.MapPost("/periods/{year:int}/{month:int}/generate", async (
    int year, int month, GenerateFinancePeriodRequest request, HttpRequest httpRequest,
    FinanceService service, IntegrationIdempotencyService idempotency, CancellationToken cancellationToken) =>
{
    var result = await idempotency.ExecuteAsync("finance_generate_period", httpRequest.Headers["Idempotency-Key"].ToString(),
        new { year, month, request }, () => service.GeneratePeriodAsync(year, month, request, cancellationToken), cancellationToken);
    return Results.Ok(result);
});
integrationFinance.MapGet("/categories", async (HttpRequest request, FinanceService service, IntegrationRestSupport rest, HomePitDbContext db, CancellationToken cancellationToken) =>
    Results.Ok(await rest.PageAsync(await service.ListCategoriesAsync(cancellationToken), item => item.Id, db.FinanceCategories,
        "finance.categories", ReadOptionalQueryString(request, "cursor"), ReadOptionalIntQuery(request, "limit"), cancellationToken)));
integrationFinance.MapPost("/categories", async (
    CreateFinanceCategoryRequest request, HttpRequest httpRequest, HttpResponse response, FinanceService service,
    IntegrationIdempotencyService idempotency, IntegrationRestSupport rest, HomePitDbContext db, CancellationToken cancellationToken) =>
{
    var result = await idempotency.ExecuteAsync("finance_create_category", httpRequest.Headers["Idempotency-Key"].ToString(), request,
        () => service.CreateCategoryAsync(request, cancellationToken), cancellationToken);
    var resource = await rest.ResourceAsync(result, result.Id, db.FinanceCategories, cancellationToken);
    IntegrationRestSupport.SetEtag(response, resource.Etag);
    return Results.Created("/api/integrations/v1/finance/categories", resource);
});
integrationFinance.MapPut("/categories/{id:guid}", async (Guid id, UpdateFinanceCategoryRequest request, HttpRequest httpRequest, HttpResponse response, FinanceService service, IntegrationRestSupport rest, HomePitDbContext db, CancellationToken cancellationToken) =>
{
    var expected = await rest.ReadExpectedVersionAsync(id, httpRequest.Headers.IfMatch.ToString(), db.FinanceCategories, cancellationToken);
    var updated = await service.UpdateCategoryAsync(id, request, cancellationToken, expected);
    var resource = await rest.ResourceAsync(updated, id, db.FinanceCategories, cancellationToken);
    IntegrationRestSupport.SetEtag(response, resource.Etag);
    return Results.Ok(resource);
});
integrationFinance.MapDelete("/categories/{id:guid}", async (Guid id, HttpRequest httpRequest, FinanceService service, IntegrationRestSupport rest, HomePitDbContext db, CancellationToken cancellationToken) =>
{
    var expected = await rest.ReadExpectedVersionAsync(id, httpRequest.Headers.IfMatch.ToString(), db.FinanceCategories, cancellationToken);
    await service.DeleteCategoryAsync(id, cancellationToken, expected);
    return Results.NoContent();
});
integrationFinance.MapGet("/recurring-templates", async (HttpRequest request, FinanceService service, IntegrationRestSupport rest, HomePitDbContext db, CancellationToken cancellationToken) =>
    Results.Ok(await rest.PageAsync(await service.ListRecurringTemplatesAsync(cancellationToken), item => item.Id, db.FinanceRecurringTemplates,
        "finance.recurring-templates", ReadOptionalQueryString(request, "cursor"), ReadOptionalIntQuery(request, "limit"), cancellationToken)));
integrationFinance.MapPost("/recurring-templates", async (
    CreateFinanceRecurringTemplateRequest request, HttpRequest httpRequest, HttpResponse response, FinanceService service,
    IntegrationIdempotencyService idempotency, IntegrationRestSupport rest, HomePitDbContext db, CancellationToken cancellationToken) =>
{
    var result = await idempotency.ExecuteAsync("finance_create_recurring_template", httpRequest.Headers["Idempotency-Key"].ToString(), request,
        () => service.CreateRecurringTemplateAsync(request, cancellationToken), cancellationToken);
    var resource = await rest.ResourceAsync(result, result.Id, db.FinanceRecurringTemplates, cancellationToken);
    IntegrationRestSupport.SetEtag(response, resource.Etag);
    return Results.Created("/api/integrations/v1/finance/recurring-templates", resource);
});
integrationFinance.MapPut("/recurring-templates/{id:guid}", async (Guid id, UpdateFinanceRecurringTemplateRequest request, HttpRequest httpRequest, HttpResponse response, FinanceService service, IntegrationRestSupport rest, HomePitDbContext db, CancellationToken cancellationToken) =>
{
    var expected = await rest.ReadExpectedVersionAsync(id, httpRequest.Headers.IfMatch.ToString(), db.FinanceRecurringTemplates, cancellationToken);
    var updated = await service.UpdateRecurringTemplateAsync(id, request, cancellationToken, expected);
    var resource = await rest.ResourceAsync(updated, id, db.FinanceRecurringTemplates, cancellationToken);
    IntegrationRestSupport.SetEtag(response, resource.Etag);
    return Results.Ok(resource);
});
integrationFinance.MapDelete("/recurring-templates/{id:guid}", async (Guid id, HttpRequest httpRequest, FinanceService service, IntegrationRestSupport rest, HomePitDbContext db, CancellationToken cancellationToken) =>
{
    var expected = await rest.ReadExpectedVersionAsync(id, httpRequest.Headers.IfMatch.ToString(), db.FinanceRecurringTemplates, cancellationToken);
    await service.DeleteRecurringTemplateAsync(id, cancellationToken, expected);
    return Results.NoContent();
});
integrationFinance.MapGet("/entries", async (HttpRequest request, FinanceService service, IntegrationRestSupport rest, HomePitDbContext db, CancellationToken cancellationToken) =>
    Results.Ok(await rest.PageAsync(await service.ListEntriesAsync(ReadOptionalIntQuery(request, "year"), ReadOptionalIntQuery(request, "month"), cancellationToken), item => item.Id, db.FinanceEntries,
        $"finance.entries:{ReadOptionalIntQuery(request, "year")}:{ReadOptionalIntQuery(request, "month")}", ReadOptionalQueryString(request, "cursor"), ReadOptionalIntQuery(request, "limit"), cancellationToken)));
integrationFinance.MapPost("/entries", async (
    CreateFinanceEntryRequest request, HttpRequest httpRequest, HttpResponse response, FinanceService service,
    IntegrationIdempotencyService idempotency, IntegrationRestSupport rest, HomePitDbContext db, CancellationToken cancellationToken) =>
{
    var result = await idempotency.ExecuteAsync("finance_create_entry", httpRequest.Headers["Idempotency-Key"].ToString(), request,
        () => service.CreateEntryAsync(request, cancellationToken), cancellationToken);
    var resource = await rest.ResourceAsync(result, result.Id, db.FinanceEntries, cancellationToken);
    IntegrationRestSupport.SetEtag(response, resource.Etag);
    return Results.Created("/api/integrations/v1/finance/entries", resource);
});
integrationFinance.MapPut("/entries/{id:guid}", async (Guid id, UpdateFinanceEntryRequest request, HttpRequest httpRequest, HttpResponse response, FinanceService service, IntegrationRestSupport rest, HomePitDbContext db, CancellationToken cancellationToken) =>
{
    var expected = await rest.ReadExpectedVersionAsync(id, httpRequest.Headers.IfMatch.ToString(), db.FinanceEntries, cancellationToken);
    var updated = await service.UpdateEntryAsync(id, request, cancellationToken, expected);
    var resource = await rest.ResourceAsync(updated, id, db.FinanceEntries, cancellationToken);
    IntegrationRestSupport.SetEtag(response, resource.Etag);
    return Results.Ok(resource);
});
integrationFinance.MapDelete("/entries/{id:guid}", async (Guid id, HttpRequest httpRequest, FinanceService service, IntegrationRestSupport rest, HomePitDbContext db, CancellationToken cancellationToken) =>
{
    var expected = await rest.ReadExpectedVersionAsync(id, httpRequest.Headers.IfMatch.ToString(), db.FinanceEntries, cancellationToken);
    await service.DeleteEntryAsync(id, cancellationToken, expected);
    return Results.NoContent();
});
integrationFinance.MapGet("/assets", async (HttpRequest request, FinanceService service, IntegrationRestSupport rest, HomePitDbContext db, CancellationToken cancellationToken) =>
    Results.Ok(await rest.PageAsync(await service.ListAssetsAsync(cancellationToken), item => item.Id, db.Assets,
        "finance.assets", ReadOptionalQueryString(request, "cursor"), ReadOptionalIntQuery(request, "limit"), cancellationToken)));
integrationFinance.MapPost("/assets", async (
    CreateAssetRequest request, HttpRequest httpRequest, HttpResponse response, FinanceService service,
    IntegrationIdempotencyService idempotency, IntegrationRestSupport rest, HomePitDbContext db, CancellationToken cancellationToken) =>
{
    var result = await idempotency.ExecuteAsync("finance_create_asset", httpRequest.Headers["Idempotency-Key"].ToString(), request,
        () => service.CreateAssetAsync(request, cancellationToken), cancellationToken);
    var resource = await rest.ResourceAsync(result, result.Id, db.Assets, cancellationToken);
    IntegrationRestSupport.SetEtag(response, resource.Etag);
    return Results.Created("/api/integrations/v1/finance/assets", resource);
});
integrationFinance.MapPut("/assets/{id:guid}", async (Guid id, UpdateAssetRequest request, HttpRequest httpRequest, HttpResponse response, FinanceService service, IntegrationRestSupport rest, HomePitDbContext db, CancellationToken cancellationToken) =>
{
    var expected = await rest.ReadExpectedVersionAsync(id, httpRequest.Headers.IfMatch.ToString(), db.Assets, cancellationToken);
    var updated = await service.UpdateAssetAsync(id, request, cancellationToken, expected);
    var resource = await rest.ResourceAsync(updated, id, db.Assets, cancellationToken);
    IntegrationRestSupport.SetEtag(response, resource.Etag);
    return Results.Ok(resource);
});
integrationFinance.MapDelete("/assets/{id:guid}", async (Guid id, HttpRequest httpRequest, FinanceService service, IntegrationRestSupport rest, HomePitDbContext db, CancellationToken cancellationToken) =>
{
    var expected = await rest.ReadExpectedVersionAsync(id, httpRequest.Headers.IfMatch.ToString(), db.Assets, cancellationToken);
    await service.DeleteAssetAsync(id, cancellationToken, expected);
    return Results.NoContent();
});
integrationFinance.MapGet("/assets/{id:guid}/valuations", async (Guid id, HttpRequest request, FinanceService service, IntegrationRestSupport rest, HomePitDbContext db, CancellationToken cancellationToken) =>
    Results.Ok(await rest.PageAsync(await service.ListAssetValuationsAsync(id, cancellationToken), item => item.Id, db.AssetValuations,
        $"finance.asset-valuations:{id}", ReadOptionalQueryString(request, "cursor"), ReadOptionalIntQuery(request, "limit"), cancellationToken)));
integrationFinance.MapPost("/assets/{id:guid}/valuations", async (
    Guid id, CreateAssetValuationRequest request, HttpRequest httpRequest, HttpResponse response, FinanceService service,
    IntegrationIdempotencyService idempotency, IntegrationRestSupport rest, HomePitDbContext db, CancellationToken cancellationToken) =>
{
    var result = await idempotency.ExecuteAsync("finance_create_asset_valuation", httpRequest.Headers["Idempotency-Key"].ToString(), new { id, request },
        () => service.CreateAssetValuationAsync(id, request, cancellationToken), cancellationToken);
    var resource = await rest.ResourceAsync(result, result.Id, db.AssetValuations, cancellationToken);
    IntegrationRestSupport.SetEtag(response, resource.Etag);
    return Results.Created($"/api/integrations/v1/finance/assets/{id}/valuations", resource);
});
integrationFinance.MapPut("/assets/{id:guid}/valuations/{valuationId:guid}", async (Guid id, Guid valuationId, UpdateAssetValuationRequest request, HttpRequest httpRequest, HttpResponse response, FinanceService service, IntegrationRestSupport rest, HomePitDbContext db, CancellationToken cancellationToken) =>
{
    var expected = await rest.ReadExpectedVersionAsync(valuationId, httpRequest.Headers.IfMatch.ToString(), db.AssetValuations, cancellationToken);
    var updated = await service.UpdateAssetValuationAsync(id, valuationId, request, cancellationToken, expected);
    var resource = await rest.ResourceAsync(updated, valuationId, db.AssetValuations, cancellationToken);
    IntegrationRestSupport.SetEtag(response, resource.Etag);
    return Results.Ok(resource);
});
integrationFinance.MapDelete("/assets/{id:guid}/valuations/{valuationId:guid}", async (Guid id, Guid valuationId, HttpRequest httpRequest, FinanceService service, IntegrationRestSupport rest, HomePitDbContext db, CancellationToken cancellationToken) =>
{
    var expected = await rest.ReadExpectedVersionAsync(valuationId, httpRequest.Headers.IfMatch.ToString(), db.AssetValuations, cancellationToken);
    await service.DeleteAssetValuationAsync(id, valuationId, cancellationToken, expected);
    return Results.NoContent();
});
integrationFinance.MapGet("/credit-cards", async (HttpRequest request, FinanceService service, IntegrationRestSupport rest, HomePitDbContext db, CancellationToken cancellationToken) =>
    Results.Ok(await rest.PageAsync(await service.ListCreditCardAccountsAsync(cancellationToken), item => item.Id, db.CreditCardAccounts,
        "finance.credit-cards", ReadOptionalQueryString(request, "cursor"), ReadOptionalIntQuery(request, "limit"), cancellationToken)));
integrationFinance.MapPost("/credit-cards", async (
    CreateCreditCardAccountRequest request, HttpRequest httpRequest, HttpResponse response, FinanceService service,
    IntegrationIdempotencyService idempotency, IntegrationRestSupport rest, HomePitDbContext db, CancellationToken cancellationToken) =>
{
    var result = await idempotency.ExecuteAsync("finance_create_credit_card", httpRequest.Headers["Idempotency-Key"].ToString(), request,
        () => service.CreateCreditCardAccountAsync(request, cancellationToken), cancellationToken);
    var resource = await rest.ResourceAsync(result, result.Id, db.CreditCardAccounts, cancellationToken);
    IntegrationRestSupport.SetEtag(response, resource.Etag);
    return Results.Created("/api/integrations/v1/finance/credit-cards", resource);
});
integrationFinance.MapPut("/credit-cards/{id:guid}", async (Guid id, UpdateCreditCardAccountRequest request, HttpRequest httpRequest, HttpResponse response, FinanceService service, IntegrationRestSupport rest, HomePitDbContext db, CancellationToken cancellationToken) =>
{
    var expected = await rest.ReadExpectedVersionAsync(id, httpRequest.Headers.IfMatch.ToString(), db.CreditCardAccounts, cancellationToken);
    var updated = await service.UpdateCreditCardAccountAsync(id, request, cancellationToken, expected);
    var resource = await rest.ResourceAsync(updated, id, db.CreditCardAccounts, cancellationToken);
    IntegrationRestSupport.SetEtag(response, resource.Etag);
    return Results.Ok(resource);
});
integrationFinance.MapDelete("/credit-cards/{id:guid}", async (Guid id, HttpRequest httpRequest, FinanceService service, IntegrationRestSupport rest, HomePitDbContext db, CancellationToken cancellationToken) =>
{
    var expected = await rest.ReadExpectedVersionAsync(id, httpRequest.Headers.IfMatch.ToString(), db.CreditCardAccounts, cancellationToken);
    await service.DeleteCreditCardAccountAsync(id, cancellationToken, expected);
    return Results.NoContent();
});
integrationFinance.MapGet("/credit-cards/{id:guid}/transactions", async (Guid id, HttpRequest request, FinanceService service, IntegrationRestSupport rest, HomePitDbContext db, CancellationToken cancellationToken) =>
    Results.Ok(await rest.PageAsync(await service.ListCreditCardTransactionsAsync(id, cancellationToken), item => item.Id, db.CreditCardTransactions,
        $"finance.credit-card-transactions:{id}", ReadOptionalQueryString(request, "cursor"), ReadOptionalIntQuery(request, "limit"), cancellationToken)));
integrationFinance.MapPost("/credit-cards/{id:guid}/transactions", async (
    Guid id, CreateCreditCardTransactionRequest request, HttpRequest httpRequest, HttpResponse response, FinanceService service,
    IntegrationIdempotencyService idempotency, IntegrationRestSupport rest, HomePitDbContext db, CancellationToken cancellationToken) =>
{
    var result = await idempotency.ExecuteAsync("finance_create_credit_card_transaction", httpRequest.Headers["Idempotency-Key"].ToString(), new { id, request },
        () => service.CreateCreditCardTransactionAsync(id, request, cancellationToken), cancellationToken);
    var resource = await rest.ResourceAsync(result, result.Id, db.CreditCardTransactions, cancellationToken);
    IntegrationRestSupport.SetEtag(response, resource.Etag);
    return Results.Created($"/api/integrations/v1/finance/credit-cards/{id}/transactions", resource);
});
integrationFinance.MapPost("/credit-cards/{id:guid}/transactions/import", async (
    Guid id, ImportCreditCardTransactionsRequest request, HttpRequest httpRequest, FinanceService service,
    IntegrationIdempotencyService idempotency, CancellationToken cancellationToken) =>
{
    var result = await idempotency.ExecuteAsync("finance_import_credit_card_transactions", httpRequest.Headers["Idempotency-Key"].ToString(), new { id, request },
        () => service.ImportCreditCardTransactionsAsync(id, request, cancellationToken), cancellationToken);
    return Results.Ok(result);
});
integrationFinance.MapPut("/credit-cards/{id:guid}/transactions/{transactionId:guid}", async (Guid id, Guid transactionId, UpdateCreditCardTransactionRequest request, HttpRequest httpRequest, HttpResponse response, FinanceService service, IntegrationRestSupport rest, HomePitDbContext db, CancellationToken cancellationToken) =>
{
    var expected = await rest.ReadExpectedVersionAsync(transactionId, httpRequest.Headers.IfMatch.ToString(), db.CreditCardTransactions, cancellationToken);
    var updated = await service.UpdateCreditCardTransactionAsync(id, transactionId, request, cancellationToken, expected);
    var resource = await rest.ResourceAsync(updated, transactionId, db.CreditCardTransactions, cancellationToken);
    IntegrationRestSupport.SetEtag(response, resource.Etag);
    return Results.Ok(resource);
});
integrationFinance.MapDelete("/credit-cards/{id:guid}/transactions/{transactionId:guid}", async (Guid id, Guid transactionId, HttpRequest httpRequest, FinanceService service, IntegrationRestSupport rest, HomePitDbContext db, CancellationToken cancellationToken) =>
{
    var expected = await rest.ReadExpectedVersionAsync(transactionId, httpRequest.Headers.IfMatch.ToString(), db.CreditCardTransactions, cancellationToken);
    await service.DeleteCreditCardTransactionAsync(id, transactionId, cancellationToken, expected);
    return Results.NoContent();
});
integrationFinance.MapGet("/credit-cards/{id:guid}/statements", async (Guid id, HttpRequest request, FinanceService service, IntegrationRestSupport rest, HomePitDbContext db, CancellationToken cancellationToken) =>
    Results.Ok(await rest.PageAsync(await service.ListCreditCardStatementsAsync(id, cancellationToken), item => item.Id, db.CreditCardStatements,
        $"finance.credit-card-statements:{id}", ReadOptionalQueryString(request, "cursor"), ReadOptionalIntQuery(request, "limit"), cancellationToken)));
integrationFinance.MapPost("/credit-cards/{id:guid}/statements", async (Guid id, CreateCreditCardStatementRequest request, HttpRequest httpRequest, HttpResponse response, FinanceService service, IntegrationIdempotencyService idempotency, IntegrationRestSupport rest, HomePitDbContext db, CancellationToken cancellationToken) =>
{
    var result = await idempotency.ExecuteAsync("finance_create_credit_card_statement", httpRequest.Headers["Idempotency-Key"].ToString(), new { id, request },
        () => service.CreateCreditCardStatementAsync(id, request, cancellationToken), cancellationToken);
    var resource = await rest.ResourceAsync(result, result.Id, db.CreditCardStatements, cancellationToken);
    IntegrationRestSupport.SetEtag(response, resource.Etag);
    return Results.Created($"/api/integrations/v1/finance/credit-cards/{id}/statements", resource);
});
integrationFinance.MapPut("/credit-cards/{id:guid}/statements/{statementId:guid}", async (Guid id, Guid statementId, UpdateCreditCardStatementRequest request, HttpRequest httpRequest, HttpResponse response, FinanceService service, IntegrationRestSupport rest, HomePitDbContext db, CancellationToken cancellationToken) =>
{
    var expected = await rest.ReadExpectedVersionAsync(statementId, httpRequest.Headers.IfMatch.ToString(), db.CreditCardStatements, cancellationToken);
    var updated = await service.UpdateCreditCardStatementAsync(id, statementId, request, cancellationToken, expected);
    var resource = await rest.ResourceAsync(updated, statementId, db.CreditCardStatements, cancellationToken);
    IntegrationRestSupport.SetEtag(response, resource.Etag);
    return Results.Ok(resource);
});
integrationFinance.MapDelete("/credit-cards/{id:guid}/statements/{statementId:guid}", async (Guid id, Guid statementId, HttpRequest httpRequest, FinanceService service, IntegrationRestSupport rest, HomePitDbContext db, CancellationToken cancellationToken) =>
{
    var expected = await rest.ReadExpectedVersionAsync(statementId, httpRequest.Headers.IfMatch.ToString(), db.CreditCardStatements, cancellationToken);
    await service.DeleteCreditCardStatementAsync(id, statementId, cancellationToken, expected);
    return Results.NoContent();
});

var integrationProjects = integrations.MapGroup("/projects");
integrationProjects.MapGet("/universes", async (HttpRequest request, ProjectService service, IntegrationRestSupport rest, HomePitDbContext db, CancellationToken cancellationToken) =>
    Results.Ok(await rest.PageAsync((await service.ListUniversesAsync(cancellationToken)).Select(IntegrationExternalDto.ToExternal).ToArray(), item => item.Id, db.Universes,
        "projects.universes", ReadOptionalQueryString(request, "cursor"), ReadOptionalIntQuery(request, "limit"), cancellationToken)));
integrationProjects.MapPost("/universes", async (CreateUniverseRequest request, HttpRequest httpRequest, HttpResponse response, ProjectService service, IntegrationIdempotencyService idempotency, IntegrationRestSupport rest, HomePitDbContext db, CancellationToken cancellationToken) =>
{
    var result = await idempotency.ExecuteAsync("projects_create_universe", httpRequest.Headers["Idempotency-Key"].ToString(), request,
        () => service.CreateUniverseAsync(request, cancellationToken), cancellationToken);
    var resource = await rest.ResourceAsync(IntegrationExternalDto.ToExternal(result), result.Id, db.Universes, cancellationToken);
    IntegrationRestSupport.SetEtag(response, resource.Etag);
    return Results.Created("/api/integrations/v1/projects/universes", resource);
});
integrationProjects.MapPut("/universes/{id:guid}", async (Guid id, UpdateUniverseRequest request, HttpRequest httpRequest, HttpResponse response, ProjectService service, IntegrationRestSupport rest, HomePitDbContext db, CancellationToken cancellationToken) =>
{
    var expected = await rest.ReadExpectedVersionAsync(id, httpRequest.Headers.IfMatch.ToString(), db.Universes, cancellationToken);
    var updated = await service.UpdateUniverseAsync(id, request, cancellationToken, expected);
    var resource = await rest.ResourceAsync(IntegrationExternalDto.ToExternal(updated), id, db.Universes, cancellationToken);
    IntegrationRestSupport.SetEtag(response, resource.Etag);
    return Results.Ok(resource);
});
integrationProjects.MapDelete("/universes/{id:guid}", async (Guid id, HttpRequest httpRequest, ProjectService service, IntegrationRestSupport rest, HomePitDbContext db, CancellationToken cancellationToken) =>
{
    var expected = await rest.ReadExpectedVersionAsync(id, httpRequest.Headers.IfMatch.ToString(), db.Universes, cancellationToken);
    await service.DeleteUniverseAsync(id, cancellationToken, expected);
    return Results.NoContent();
});
integrationProjects.MapGet("/items", async (HttpRequest request, ProjectService service, IntegrationRestSupport rest, HomePitDbContext db, CancellationToken cancellationToken) =>
{
    var universeId = ReadOptionalGuidQuery(request, "universeId");
    return Results.Ok(await rest.PageAsync((await service.ListProjectsAsync(universeId, cancellationToken)).Select(IntegrationExternalDto.ToExternal).ToArray(), item => item.Id, db.Projects,
        $"projects.items:{universeId}", ReadOptionalQueryString(request, "cursor"), ReadOptionalIntQuery(request, "limit"), cancellationToken));
});
integrationProjects.MapPost("/items", async (CreateProjectRequest request, HttpRequest httpRequest, HttpResponse response, ProjectService service, IntegrationIdempotencyService idempotency, IntegrationRestSupport rest, HomePitDbContext db, CancellationToken cancellationToken) =>
{
    var result = await idempotency.ExecuteAsync("projects_create_project", httpRequest.Headers["Idempotency-Key"].ToString(), request,
        () => service.CreateProjectAsync(request, cancellationToken), cancellationToken);
    var resource = await rest.ResourceAsync(IntegrationExternalDto.ToExternal(result), result.Id, db.Projects, cancellationToken);
    IntegrationRestSupport.SetEtag(response, resource.Etag);
    return Results.Created("/api/integrations/v1/projects/items", resource);
});
integrationProjects.MapPut("/items/{id:guid}", async (Guid id, UpdateProjectRequest request, HttpRequest httpRequest, HttpResponse response, ProjectService service, IntegrationRestSupport rest, HomePitDbContext db, CancellationToken cancellationToken) =>
{
    var expected = await rest.ReadExpectedVersionAsync(id, httpRequest.Headers.IfMatch.ToString(), db.Projects, cancellationToken);
    var updated = await service.UpdateProjectAsync(id, request, cancellationToken, expected);
    var resource = await rest.ResourceAsync(IntegrationExternalDto.ToExternal(updated), id, db.Projects, cancellationToken);
    IntegrationRestSupport.SetEtag(response, resource.Etag);
    return Results.Ok(resource);
});
integrationProjects.MapDelete("/items/{id:guid}", async (Guid id, HttpRequest httpRequest, ProjectService service, IntegrationRestSupport rest, HomePitDbContext db, CancellationToken cancellationToken) =>
{
    var expected = await rest.ReadExpectedVersionAsync(id, httpRequest.Headers.IfMatch.ToString(), db.Projects, cancellationToken);
    await service.DeleteProjectAsync(id, cancellationToken, expected);
    return Results.NoContent();
});
integrationProjects.MapGet("/activities", async (HttpRequest request, ProjectService service, IntegrationRestSupport rest, HomePitDbContext db, CancellationToken cancellationToken) =>
{
    var projectId = ReadOptionalGuidQuery(request, "projectId");
    var statusRaw = ReadOptionalQueryString(request, "status");
    HomePit.Domain.Projects.ActivityStatus? status = statusRaw is null ? null : Enum.TryParse<HomePit.Domain.Projects.ActivityStatus>(statusRaw, true, out var parsed)
        ? parsed : throw new ValidationException("O parâmetro 'status' é inválido.");
    return Results.Ok(await rest.PageAsync((await service.ListActivitiesAsync(projectId, status, cancellationToken)).Select(IntegrationExternalDto.ToExternal).ToArray(), item => item.Id, db.Activities,
        $"projects.activities:{projectId}:{status}", ReadOptionalQueryString(request, "cursor"), ReadOptionalIntQuery(request, "limit"), cancellationToken));
});
integrationProjects.MapPost("/activities", async (CreateActivityRequest request, HttpRequest httpRequest, HttpResponse response, ProjectService service, IntegrationIdempotencyService idempotency, IntegrationRestSupport rest, HomePitDbContext db, CancellationToken cancellationToken) =>
{
    var result = await idempotency.ExecuteAsync("projects_create_activity", httpRequest.Headers["Idempotency-Key"].ToString(), request,
        () => service.CreateActivityAsync(request, cancellationToken), cancellationToken);
    var resource = await rest.ResourceAsync(IntegrationExternalDto.ToExternal(result), result.Id, db.Activities, cancellationToken);
    IntegrationRestSupport.SetEtag(response, resource.Etag);
    return Results.Created("/api/integrations/v1/projects/activities", resource);
});
integrationProjects.MapPut("/activities/{id:guid}", async (Guid id, UpdateActivityRequest request, HttpRequest httpRequest, HttpResponse response, ProjectService service, IntegrationRestSupport rest, HomePitDbContext db, CancellationToken cancellationToken) =>
{
    var expected = await rest.ReadExpectedVersionAsync(id, httpRequest.Headers.IfMatch.ToString(), db.Activities, cancellationToken);
    var updated = await service.UpdateActivityAsync(id, request, cancellationToken, expected);
    var resource = await rest.ResourceAsync(IntegrationExternalDto.ToExternal(updated), id, db.Activities, cancellationToken);
    IntegrationRestSupport.SetEtag(response, resource.Etag);
    return Results.Ok(resource);
});
integrationProjects.MapPatch("/activities/{id:guid}/status", async (Guid id, UpdateActivityStatusRequest request, HttpRequest httpRequest, HttpResponse response, ProjectService service, IntegrationRestSupport rest, HomePitDbContext db, CancellationToken cancellationToken) =>
{
    var expected = await rest.ReadExpectedVersionAsync(id, httpRequest.Headers.IfMatch.ToString(), db.Activities, cancellationToken);
    var updated = await service.UpdateActivityStatusAsync(id, request, cancellationToken, expected);
    var resource = await rest.ResourceAsync(IntegrationExternalDto.ToExternal(updated), id, db.Activities, cancellationToken);
    IntegrationRestSupport.SetEtag(response, resource.Etag);
    return Results.Ok(resource);
});
integrationProjects.MapDelete("/activities/{id:guid}", async (Guid id, HttpRequest httpRequest, ProjectService service, IntegrationRestSupport rest, HomePitDbContext db, CancellationToken cancellationToken) =>
{
    var expected = await rest.ReadExpectedVersionAsync(id, httpRequest.Headers.IfMatch.ToString(), db.Activities, cancellationToken);
    await service.DeleteActivityAsync(id, cancellationToken, expected);
    return Results.NoContent();
});
integrationProjects.MapGet("/activities/{id:guid}/comments", async (Guid id, HttpRequest request, ProjectService service, IntegrationRestSupport rest, HomePitDbContext db, CancellationToken cancellationToken) =>
    Results.Ok(await rest.PageAsync(await service.ListActivityCommentsAsync(id, cancellationToken), item => item.Id, db.ActivityComments,
        $"projects.comments:{id}", ReadOptionalQueryString(request, "cursor"), ReadOptionalIntQuery(request, "limit"), cancellationToken)));
integrationProjects.MapPost("/activities/{id:guid}/comments", async (Guid id, CreateActivityCommentRequest request, HttpRequest httpRequest, HttpResponse response, ProjectService service, IntegrationIdempotencyService idempotency, IntegrationRestSupport rest, HomePitDbContext db, CancellationToken cancellationToken) =>
{
    var result = await idempotency.ExecuteAsync("projects_create_comment", httpRequest.Headers["Idempotency-Key"].ToString(), new { id, request },
        () => service.CreateActivityCommentAsync(id, request, cancellationToken), cancellationToken);
    var resource = await rest.ResourceAsync(result, result.Id, db.ActivityComments, cancellationToken);
    IntegrationRestSupport.SetEtag(response, resource.Etag);
    return Results.Created($"/api/integrations/v1/projects/activities/{id}/comments", resource);
});
integrationProjects.MapPut("/activities/{activityId:guid}/comments/{commentId:guid}", async (Guid activityId, Guid commentId, UpdateActivityCommentRequest request, HttpRequest httpRequest, HttpResponse response, ProjectService service, IntegrationRestSupport rest, HomePitDbContext db, CancellationToken cancellationToken) =>
{
    var expected = await rest.ReadExpectedVersionAsync(commentId, httpRequest.Headers.IfMatch.ToString(), db.ActivityComments, cancellationToken);
    var updated = await service.UpdateActivityCommentAsync(activityId, commentId, request, cancellationToken, expected);
    var resource = await rest.ResourceAsync(updated, commentId, db.ActivityComments, cancellationToken);
    IntegrationRestSupport.SetEtag(response, resource.Etag);
    return Results.Ok(resource);
});
integrationProjects.MapDelete("/activities/{activityId:guid}/comments/{commentId:guid}", async (Guid activityId, Guid commentId, HttpRequest httpRequest, ProjectService service, IntegrationRestSupport rest, HomePitDbContext db, CancellationToken cancellationToken) =>
{
    var expected = await rest.ReadExpectedVersionAsync(commentId, httpRequest.Headers.IfMatch.ToString(), db.ActivityComments, cancellationToken);
    await service.DeleteActivityCommentAsync(activityId, commentId, cancellationToken, expected);
    return Results.NoContent();
});
integrationProjects.MapGet("/activities/{id:guid}/pending-items", async (Guid id, HttpRequest request, ProjectService service, IntegrationRestSupport rest, HomePitDbContext db, CancellationToken cancellationToken) =>
    Results.Ok(await rest.PageAsync(await service.ListPendingItemsAsync(id, cancellationToken), item => item.Id, db.PendingItems,
        $"projects.pending-items:{id}", ReadOptionalQueryString(request, "cursor"), ReadOptionalIntQuery(request, "limit"), cancellationToken)));
integrationProjects.MapPost("/activities/{id:guid}/pending-items", async (Guid id, CreatePendingItemRequest request, HttpRequest httpRequest, HttpResponse response, ProjectService service, IntegrationIdempotencyService idempotency, IntegrationRestSupport rest, HomePitDbContext db, CancellationToken cancellationToken) =>
{
    var result = await idempotency.ExecuteAsync("projects_create_pending_item", httpRequest.Headers["Idempotency-Key"].ToString(), new { id, request },
        () => service.CreatePendingItemAsync(id, request, cancellationToken), cancellationToken);
    var resource = await rest.ResourceAsync(result, result.Id, db.PendingItems, cancellationToken);
    IntegrationRestSupport.SetEtag(response, resource.Etag);
    return Results.Created($"/api/integrations/v1/projects/activities/{id}/pending-items", resource);
});

if (oauthEnabled)
{
    app.MapMcp("/mcp")
        .RequireAuthorization("mcp-oauth")
        .RequireRateLimiting("integrations");
}

await app.RunAsync();

static string? ReadOptionalQueryString(HttpRequest request, string key)
{
    return request.Query.TryGetValue(key, out var values)
        ? string.IsNullOrWhiteSpace(values.ToString()) ? null : values.ToString()
        : null;
}

static Guid? ReadOptionalGuidQuery(HttpRequest request, string key)
{
    var rawValue = ReadOptionalQueryString(request, key);
    if (rawValue is null)
    {
        return null;
    }

    if (Guid.TryParse(rawValue, out var guid))
    {
        return guid;
    }

    throw new ValidationException($"O parâmetro '{key}' deve ser um GUID válido.");
}

static void EnsureOAuthConfiguration(OAuthOptions options)
{
    if (!Uri.TryCreate(options.Issuer, UriKind.Absolute, out var issuer) || issuer.Scheme != Uri.UriSchemeHttps ||
        !Uri.TryCreate(options.WebConsentUrl, UriKind.Absolute, out var consent) || consent.Scheme != Uri.UriSchemeHttps ||
        options.AccessTokenMinutes is < 1 or > 60 || options.RefreshTokenDays is < 1 or > 365 ||
        options.InteractionMinutes is < 1 or > 30 || !IsValidOAuthKey(options.SigningKey) || !IsValidOAuthKey(options.EncryptionKey))
    {
        throw new InvalidOperationException("OAuth exige Issuer e URL de consentimento HTTPS, além de chaves Base64 distintas de 32 bytes.");
    }

    if (string.Equals(options.SigningKey, options.EncryptionKey, StringComparison.Ordinal))
    {
        throw new InvalidOperationException("OAuth exige chaves de assinatura e criptografia distintas.");
    }
}

static bool IsValidOAuthKey(string value)
{
    try
    {
        return Convert.FromBase64String(value).Length >= 32;
    }
    catch (FormatException)
    {
        return false;
    }
}

static string NormalizeDynamicRedirectUri(string value)
{
    if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || !string.IsNullOrEmpty(uri.Fragment) ||
        !string.IsNullOrEmpty(uri.UserInfo) || string.IsNullOrEmpty(uri.Host) || uri.Query.Contains('*') || uri.AbsolutePath.Contains('*'))
    {
        throw new ValidationException("A URI de retorno OAuth é inválida.");
    }

    var isLoopback = uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || uri.Host.Equals("127.0.0.1", StringComparison.Ordinal);
    if (uri.Scheme != Uri.UriSchemeHttps && !(isLoopback && uri.Scheme == Uri.UriSchemeHttp))
    {
        throw new ValidationException("A URI de retorno deve usar HTTPS, exceto localhost ou 127.0.0.1.");
    }

    return uri.AbsoluteUri;
}

static bool? ReadOptionalBoolQuery(HttpRequest request, string key)
{
    var rawValue = ReadOptionalQueryString(request, key);
    if (rawValue is null)
    {
        return null;
    }

    if (bool.TryParse(rawValue, out var value))
    {
        return value;
    }

    throw new ValidationException($"O parâmetro '{key}' deve ser verdadeiro ou falso.");
}

static int? ReadOptionalIntQuery(HttpRequest request, string key)
{
    var rawValue = ReadOptionalQueryString(request, key);
    if (rawValue is null)
    {
        return null;
    }

    if (int.TryParse(rawValue, out var value))
    {
        return value;
    }

    throw new ValidationException($"O parâmetro '{key}' deve ser um número inteiro válido.");
}

static Guid[] ReadGuidCollectionQuery(HttpRequest request, string key)
{
    if (!request.Query.TryGetValue(key, out var values))
    {
        return [];
    }

    var results = new List<Guid>();
    foreach (var rawValue in values)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            continue;
        }

        if (!Guid.TryParse(rawValue, out var guid))
        {
            throw new ValidationException($"O parâmetro '{key}' deve conter apenas GUIDs válidos.");
        }

        results.Add(guid);
    }

    return results.ToArray();
}

public partial class Program;
