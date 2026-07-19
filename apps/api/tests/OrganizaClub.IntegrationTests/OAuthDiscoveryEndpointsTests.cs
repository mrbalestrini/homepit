using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using OrganizaClub.Application.Common;
using OrganizaClub.Application.Storage;
using OrganizaClub.Domain.Spaces;
using OrganizaClub.Domain.Integrations;
using OrganizaClub.Infrastructure.Data;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenIddict.Abstractions;
using Xunit;

namespace OrganizaClub.IntegrationTests;

public sealed class OAuthDiscoveryEndpointsTests
{
    private const string CanonicalMcpResource = "https://api.organiza.club/mcp";

    static OAuthDiscoveryEndpointsTests()
    {
        Environment.SetEnvironmentVariable("Integrations__Enabled", "true");
        Environment.SetEnvironmentVariable("Mcp__Enabled", "true");
        Environment.SetEnvironmentVariable("OAuth__Issuer", "https://api.organiza.club");
        Environment.SetEnvironmentVariable("OAuth__WebConsentUrl", "https://organiza.club/oauth/consent");
        Environment.SetEnvironmentVariable("OAuth__SigningKey", "MDEyMzQ1Njc4OUFCQ0RFRjAxMjM0NTY3ODlBQkNERUY=");
        Environment.SetEnvironmentVariable("OAuth__EncryptionKey", "RkVEQ0JBOTg3NjU0MzIxMEZFRENCQTk4NzY1NDMyMTA=");
    }

    [Fact]
    public async Task Discovery_advertises_dynamic_registration_and_public_token_authentication()
    {
        await using var factory = new OrganizaClubApiFactory();
        using var client = CreateOAuthClient(factory);

        var response = await client.GetAsync("/.well-known/oauth-authorization-server");

        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var metadata = document.RootElement;
        Assert.Equal("https://api.organiza.club/connect/register", metadata.GetProperty("registration_endpoint").GetString());
        Assert.Contains(metadata.GetProperty("token_endpoint_auth_methods_supported").EnumerateArray(),
            method => method.GetString() == "none");
        Assert.Contains(metadata.GetProperty("scopes_supported").EnumerateArray(),
            scope => scope.GetString() == OpenIddictConstants.Scopes.OpenId);
    }

    [Fact]
    public async Task Dynamic_registration_creates_a_public_client_without_a_secret()
    {
        await using var factory = new OrganizaClubApiFactory();
        using var client = CreateOAuthClient(factory);

        var response = await RegisterDynamicClientAsync(client);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var registration = document.RootElement;
        Assert.False(string.IsNullOrWhiteSpace(registration.GetProperty("client_id").GetString()));
        Assert.False(registration.TryGetProperty("client_secret", out _));
        Assert.Equal("none", registration.GetProperty("token_endpoint_auth_method").GetString());
    }

    [Fact]
    public async Task Dynamic_registration_grants_the_canonical_mcp_resource_permission()
    {
        await using var factory = new OrganizaClubApiFactory();
        using var client = CreateOAuthClient(factory);

        var response = await RegisterDynamicClientAsync(client);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var clientId = document.RootElement.GetProperty("client_id").GetString();

        using var scope = factory.Services.CreateScope();
        var applications = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var application = await applications.FindByClientIdAsync(clientId!);
        var permissions = await applications.GetPermissionsAsync(application!);

        Assert.Contains(permissions, permission => permission.EndsWith(CanonicalMcpResource, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Authorization_with_the_canonical_resource_reaches_the_consent_flow_without_invalid_target()
    {
        await using var factory = new OrganizaClubApiFactory();
        using var client = CreateOAuthClient(factory, allowAutoRedirect: false);
        var registration = await RegisterDynamicClientAsync(client);
        using var document = JsonDocument.Parse(await registration.Content.ReadAsStringAsync());
        var clientId = document.RootElement.GetProperty("client_id").GetString();

        var response = await client.GetAsync(CreateAuthorizationRequest(clientId!, CanonicalMcpResource));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("https://organiza.club/oauth/consent?interaction=", response.Headers.Location?.ToString());
    }

    [Theory]
    [InlineData("openid offline_access organiza.read organiza.write")]
    [InlineData("openid organiza.read")]
    [InlineData("offline_access organiza.read")]
    public async Task Authorization_with_supported_scopes_reaches_the_consent_flow(string scope)
    {
        await using var factory = new OrganizaClubApiFactory();
        using var client = CreateOAuthClient(factory, allowAutoRedirect: false);
        var registration = await RegisterDynamicClientAsync(client);
        using var document = JsonDocument.Parse(await registration.Content.ReadAsStringAsync());
        var clientId = document.RootElement.GetProperty("client_id").GetString();

        var response = await client.GetAsync(CreateAuthorizationRequest(clientId!, CanonicalMcpResource, scope));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("https://organiza.club/oauth/consent?interaction=", response.Headers.Location?.ToString());
    }

    [Theory]
    [InlineData("openid offline_access")]
    [InlineData("openid organiza.read scope_invalido")]
    public async Task Authorization_without_read_or_with_an_unknown_scope_is_rejected(string scope)
    {
        await using var factory = new OrganizaClubApiFactory();
        using var client = CreateOAuthClient(factory, allowAutoRedirect: false);
        var registration = await RegisterDynamicClientAsync(client);
        using var document = JsonDocument.Parse(await registration.Content.ReadAsStringAsync());
        var clientId = document.RootElement.GetProperty("client_id").GetString();

        var response = await client.GetAsync(CreateAuthorizationRequest(clientId!, CanonicalMcpResource, scope));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Authorization_code_with_openid_issues_an_id_token_with_only_the_stable_subject_claim()
    {
        await using var factory = new OrganizaClubApiFactory();
        using var client = CreateOAuthClient(factory, allowAutoRedirect: false);
        var registration = await RegisterDynamicClientAsync(client);
        using var registrationDocument = JsonDocument.Parse(await registration.Content.ReadAsStringAsync());
        var clientId = registrationDocument.RootElement.GetProperty("client_id").GetString()!;
        const string verifier = "this-is-a-valid-pkce-verifier-with-at-least-forty-three-characters";
        var challenge = CreateCodeChallenge(verifier);
        const string scope = "openid offline_access organiza.read organiza.write";

        var start = await client.GetAsync(CreateAuthorizationRequest(clientId, CanonicalMcpResource, scope, challenge));
        Assert.Equal(HttpStatusCode.Redirect, start.StatusCode);
        var startLocation = start.Headers.Location ?? throw new InvalidOperationException("Redirecionamento de consentimento ausente.");
        var interactionToken = QueryHelpers.ParseQuery(startLocation.Query)["interaction"].Single()
            ?? throw new InvalidOperationException("Interação OAuth ausente.");

        Guid userId;
        await using (var serviceScope = factory.Services.CreateAsyncScope())
        {
            var db = serviceScope.ServiceProvider.GetRequiredService<OrganizaClubDbContext>();
            var user = new AppUser
            {
                Email = "oidc@organiza.club",
                PasswordHash = "hash",
                DisplayName = "OIDC",
                SystemRole = SystemRole.User
            };
            var space = new Space { Name = "Espaço OIDC", CreatedByUserId = user.Id };
            var connection = new IntegrationConnection
            {
                User = user,
                Space = space,
                Name = "MCP Inspector",
                CredentialKind = IntegrationCredentialKind.OAuthGrant,
                AccessMode = IntegrationAccessMode.ReadWrite,
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(30)
            };
            db.AddRange(user, space, new SpaceMember { User = user, Space = space, Role = SpaceRole.Owner }, connection);
            var interaction = await db.OAuthAuthorizationInteractions.SingleAsync();
            interaction.ApprovedAt = DateTimeOffset.UtcNow;
            interaction.ApprovedByUser = user;
            interaction.IntegrationConnection = connection;
            await db.SaveChangesAsync();
            userId = user.Id;
        }

        var callback = await client.GetAsync(CreateAuthorizationRequest(clientId, CanonicalMcpResource, scope, challenge) +
            $"&interaction={Uri.EscapeDataString(interactionToken)}");
        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        var callbackLocation = callback.Headers.Location ?? throw new InvalidOperationException("Callback OAuth ausente.");
        var code = QueryHelpers.ParseQuery(callbackLocation.Query)["code"].Single()
            ?? throw new InvalidOperationException("Código OAuth ausente.");
        using var tokenResponse = await client.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = clientId,
            ["redirect_uri"] = "http://localhost:6274/oauth/callback",
            ["code"] = code,
            ["code_verifier"] = verifier,
            ["resource"] = CanonicalMcpResource
        }));

        tokenResponse.EnsureSuccessStatusCode();
        using var tokenDocument = JsonDocument.Parse(await tokenResponse.Content.ReadAsStringAsync());
        var idToken = tokenDocument.RootElement.GetProperty("id_token").GetString()!;
        using var payload = JsonDocument.Parse(Base64UrlDecode(idToken.Split('.')[1]));
        Assert.Equal(userId.ToString(), payload.RootElement.GetProperty("sub").GetString());
        Assert.False(payload.RootElement.TryGetProperty("integration_connection_id", out _));
        Assert.False(payload.RootElement.TryGetProperty("integration_space_id", out _));
        Assert.False(payload.RootElement.TryGetProperty("integration_access_mode", out _));
        Assert.False(payload.RootElement.TryGetProperty("name", out _));
    }

    [Fact]
    public async Task Authorization_with_a_different_resource_is_rejected()
    {
        await using var factory = new OrganizaClubApiFactory();
        using var client = CreateOAuthClient(factory, allowAutoRedirect: false);
        var registration = await RegisterDynamicClientAsync(client);
        using var document = JsonDocument.Parse(await registration.Content.ReadAsStringAsync());
        var clientId = document.RootElement.GetProperty("client_id").GetString();

        var response = await client.GetAsync(CreateAuthorizationRequest(clientId!, "https://api.organiza.club/outro"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("error:invalid_target", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Protected_resource_metadata_keeps_the_canonical_mcp_resource()
    {
        await using var factory = new OrganizaClubApiFactory();
        using var client = CreateOAuthClient(factory);

        var response = await client.GetAsync("/.well-known/oauth-protected-resource/mcp");

        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(CanonicalMcpResource, document.RootElement.GetProperty("resource").GetString());
    }

    private static async Task<HttpResponseMessage> RegisterDynamicClientAsync(HttpClient client) =>
        await client.PostAsJsonAsync("/connect/register", new
        {
            client_name = "MCP Inspector",
            redirect_uris = new[]
            {
                "http://localhost:6274/oauth/callback",
                "http://localhost:6274/oauth/callback/debug"
            },
            grant_types = new[] { "authorization_code", "refresh_token" },
            response_types = new[] { "code" },
            token_endpoint_auth_method = "none"
        });

    private static string CreateAuthorizationRequest(
        string clientId,
        string resource,
        string scope = "organiza.read",
        string codeChallenge = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNO12345678901") =>
        "/connect/authorize?response_type=code" +
        $"&client_id={Uri.EscapeDataString(clientId)}" +
        "&redirect_uri=http%3A%2F%2Flocalhost%3A6274%2Foauth%2Fcallback" +
        $"&scope={Uri.EscapeDataString(scope)}" +
        "&state=state" +
        $"&code_challenge={Uri.EscapeDataString(codeChallenge)}" +
        "&code_challenge_method=S256" +
        $"&resource={Uri.EscapeDataString(resource)}";

    private static string CreateCodeChallenge(string verifier) => Convert.ToBase64String(SHA256.HashData(System.Text.Encoding.ASCII.GetBytes(verifier)))
        .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded += new string('=', (4 - padded.Length % 4) % 4);
        return Convert.FromBase64String(padded);
    }

    private static HttpClient CreateOAuthClient(WebApplicationFactory<Program> factory, bool allowAutoRedirect = true) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://api.organiza.club"),
            AllowAutoRedirect = allowAutoRedirect
        });

    private sealed class OrganizaClubApiFactory : WebApplicationFactory<Program>, IAsyncDisposable
    {
        private readonly string databaseName = Guid.NewGuid().ToString("N");
        private readonly FakeObjectStorage storage = new();

        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            builder.UseSetting(Microsoft.AspNetCore.Hosting.WebHostDefaults.EnvironmentKey, "Development");
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Database:ApplyMigrationsOnStartup"] = "false",
                    ["Notifications:DailyDigestEnabled"] = "false",
                    ["AccountLifecycle:Enabled"] = "false",
                    ["ObjectStorage:CreateBucketOnStartup"] = "true",
                    ["Integrations:Enabled"] = "true",
                    ["Mcp:Enabled"] = "true",
                    ["OAuth:Issuer"] = "https://api.organiza.club",
                    ["OAuth:WebConsentUrl"] = "https://organiza.club/oauth/consent",
                    ["OAuth:SigningKey"] = "MDEyMzQ1Njc4OUFCQ0RFRjAxMjM0NTY3ODlBQkNERUY=",
                    ["OAuth:EncryptionKey"] = "RkVEQ0JBOTg3NjU0MzIxMEZFRENCQTk4NzY1NDMyMTA="
                });
            });

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<OrganizaClubDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<OrganizaClubDbContext>>();
                services.RemoveAll<OrganizaClubDbContext>();
                services.RemoveAll<IOrganizaClubDbContext>();
                services.RemoveAll<IObjectStorage>();

                services.AddDbContext<OrganizaClubDbContext>(options => options.UseInMemoryDatabase(databaseName));
                services.AddScoped<IOrganizaClubDbContext>(provider => provider.GetRequiredService<OrganizaClubDbContext>());
                services.AddSingleton<IObjectStorage>(storage);
            });
        }

        public new async ValueTask DisposeAsync()
        {
            await base.DisposeAsync();
        }
    }

    private sealed class FakeObjectStorage : IObjectStorage
    {
        public Task EnsureBucketExistsAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<StoredObject> GetAsync(string objectKey, CancellationToken cancellationToken) =>
            throw new NotFoundException("Arquivo não encontrado.");

        public Task PutAsync(ObjectStoragePutRequest request, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task DeleteAsync(string objectKey, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
