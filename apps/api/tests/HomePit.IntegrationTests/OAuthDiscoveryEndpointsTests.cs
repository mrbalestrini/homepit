using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HomePit.Application.Common;
using HomePit.Application.Storage;
using HomePit.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenIddict.Abstractions;
using Xunit;

namespace HomePit.IntegrationTests;

public sealed class OAuthDiscoveryEndpointsTests
{
    private const string CanonicalMcpResource = "https://api.organiza.club/mcp";

    static OAuthDiscoveryEndpointsTests()
    {
        Environment.SetEnvironmentVariable("Integrations__Enabled", "true");
        Environment.SetEnvironmentVariable("Mcp__Enabled", "true");
        Environment.SetEnvironmentVariable("OAuth__Issuer", "https://api.organiza.club");
        Environment.SetEnvironmentVariable("OAuth__WebConsentUrl", "https://homepit.organiza.club/oauth/consent");
        Environment.SetEnvironmentVariable("OAuth__SigningKey", "MDEyMzQ1Njc4OUFCQ0RFRjAxMjM0NTY3ODlBQkNERUY=");
        Environment.SetEnvironmentVariable("OAuth__EncryptionKey", "RkVEQ0JBOTg3NjU0MzIxMEZFRENCQTk4NzY1NDMyMTA=");
    }

    [Fact]
    public async Task Discovery_advertises_dynamic_registration_and_public_token_authentication()
    {
        await using var factory = new HomePitApiFactory();
        using var client = CreateOAuthClient(factory);

        var response = await client.GetAsync("/.well-known/oauth-authorization-server");

        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var metadata = document.RootElement;
        Assert.Equal("https://api.organiza.club/connect/register", metadata.GetProperty("registration_endpoint").GetString());
        Assert.Contains(metadata.GetProperty("token_endpoint_auth_methods_supported").EnumerateArray(),
            method => method.GetString() == "none");
    }

    [Fact]
    public async Task Dynamic_registration_creates_a_public_client_without_a_secret()
    {
        await using var factory = new HomePitApiFactory();
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
        await using var factory = new HomePitApiFactory();
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
        await using var factory = new HomePitApiFactory();
        using var client = CreateOAuthClient(factory, allowAutoRedirect: false);
        var registration = await RegisterDynamicClientAsync(client);
        using var document = JsonDocument.Parse(await registration.Content.ReadAsStringAsync());
        var clientId = document.RootElement.GetProperty("client_id").GetString();

        var response = await client.GetAsync(CreateAuthorizationRequest(clientId!, CanonicalMcpResource));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("https://homepit.organiza.club/oauth/consent?interaction=", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task Authorization_with_a_different_resource_is_rejected()
    {
        await using var factory = new HomePitApiFactory();
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
        await using var factory = new HomePitApiFactory();
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

    private static string CreateAuthorizationRequest(string clientId, string resource) =>
        "/connect/authorize?response_type=code" +
        $"&client_id={Uri.EscapeDataString(clientId)}" +
        "&redirect_uri=http%3A%2F%2Flocalhost%3A6274%2Foauth%2Fcallback" +
        "&scope=homepit.read" +
        "&state=state" +
        "&code_challenge=abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNO12345678901" +
        "&code_challenge_method=S256" +
        $"&resource={Uri.EscapeDataString(resource)}";

    private static HttpClient CreateOAuthClient(WebApplicationFactory<Program> factory, bool allowAutoRedirect = true) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://api.organiza.club"),
            AllowAutoRedirect = allowAutoRedirect
        });

    private sealed class HomePitApiFactory : WebApplicationFactory<Program>, IAsyncDisposable
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
                    ["OAuth:WebConsentUrl"] = "https://homepit.organiza.club/oauth/consent",
                    ["OAuth:SigningKey"] = "MDEyMzQ1Njc4OUFCQ0RFRjAxMjM0NTY3ODlBQkNERUY=",
                    ["OAuth:EncryptionKey"] = "RkVEQ0JBOTg3NjU0MzIxMEZFRENCQTk4NzY1NDMyMTA="
                });
            });

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<HomePitDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<HomePitDbContext>>();
                services.RemoveAll<HomePitDbContext>();
                services.RemoveAll<IHomePitDbContext>();
                services.RemoveAll<IObjectStorage>();

                services.AddDbContext<HomePitDbContext>(options => options.UseInMemoryDatabase(databaseName));
                services.AddScoped<IHomePitDbContext>(provider => provider.GetRequiredService<HomePitDbContext>());
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
