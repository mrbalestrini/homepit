using System.Net;
using System.Net.Http.Headers;
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
using Xunit;

namespace HomePit.IntegrationTests;

public sealed class ToolImprovementSuggestionEndpointsTests
{
    [Fact]
    public async Task Authenticated_user_can_submit_and_superadmin_can_triage_suggestions()
    {
        await using var factory = new HomePitApiFactory();
        using var client = factory.CreateClient();

        var userAuth = await RegisterAndAuthenticateAsync(client, "user@homepit.dev", "User");
        var createResponse = await SendAuthorizedAsync(
            client,
            userAuth.AccessToken,
            HttpMethod.Post,
            "/api/users/me/tool-improvement-suggestions",
            JsonContent.Create(new
            {
                suggestionText = "Melhorar a seção de filtros do módulo Projetos."
            }));

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<ToolImprovementSuggestionResponse>(JsonSerializerOptions.Web);
        Assert.NotNull(created);
        Assert.Equal("NaoLido", created.Status);
        Assert.Equal("Media", created.Priority);

        var superAdminAuth = await LoginAsSuperAdminAsync(client);
        var listResponse = await SendAuthorizedAsync(
            client,
            superAdminAuth.AccessToken,
            HttpMethod.Get,
            "/api/admin/platform/tool-improvement-suggestions");

        listResponse.EnsureSuccessStatusCode();
        var suggestions = await listResponse.Content.ReadFromJsonAsync<IReadOnlyCollection<ToolImprovementSuggestionResponse>>(JsonSerializerOptions.Web);
        var suggestion = Assert.Single(suggestions!);
        Assert.Equal("user@homepit.dev", suggestion.UserEmail);

        var updateResponse = await SendAuthorizedAsync(
            client,
            superAdminAuth.AccessToken,
            HttpMethod.Put,
            $"/api/admin/platform/tool-improvement-suggestions/{suggestion.Id}",
            JsonContent.Create(new
            {
                status = "EmExecucao",
                priority = "Alta",
                internalComment = "Mapear impacto no módulo de projetos."
            }));

        updateResponse.EnsureSuccessStatusCode();
        var updated = await updateResponse.Content.ReadFromJsonAsync<ToolImprovementSuggestionResponse>(JsonSerializerOptions.Web);
        Assert.NotNull(updated);
        Assert.Equal("EmExecucao", updated.Status);
        Assert.Equal("Alta", updated.Priority);
        Assert.Equal("Mapear impacto no módulo de projetos.", updated.InternalComment);

        var bulkResponse = await SendAuthorizedAsync(
            client,
            superAdminAuth.AccessToken,
            HttpMethod.Post,
            "/api/admin/platform/tool-improvement-suggestions/bulk-update",
            JsonContent.Create(new
            {
                suggestionIds = new[] { suggestion.Id },
                status = "Feito",
                priority = "Urgente"
            }));

        bulkResponse.EnsureSuccessStatusCode();
        var bulkUpdated = await bulkResponse.Content.ReadFromJsonAsync<IReadOnlyCollection<ToolImprovementSuggestionResponse>>(JsonSerializerOptions.Web);
        var completed = Assert.Single(bulkUpdated!);
        Assert.Equal("Feito", completed.Status);
        Assert.Equal("Urgente", completed.Priority);
    }

    [Fact]
    public async Task Non_superadmin_cannot_access_admin_suggestions()
    {
        await using var factory = new HomePitApiFactory();
        using var client = factory.CreateClient();

        var userAuth = await RegisterAndAuthenticateAsync(client, "member@homepit.dev", "Member");

        var response = await SendAuthorizedAsync(
            client,
            userAuth.AccessToken,
            HttpMethod.Get,
            "/api/admin/platform/tool-improvement-suggestions");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static async Task<AuthResponse> RegisterAndAuthenticateAsync(HttpClient client, string email, string displayName)
    {
        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = "secret123",
            displayName,
            phoneNumber = (string?)null
        });

        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponse>(JsonSerializerOptions.Web))!;
    }

    private static async Task<AuthResponse> LoginAsSuperAdminAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "superadmin@homepit.dev",
            password = "super-secret"
        });

        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponse>(JsonSerializerOptions.Web))!;
    }

    private static async Task<HttpResponseMessage> SendAuthorizedAsync(
        HttpClient client,
        string accessToken,
        HttpMethod method,
        string path,
        HttpContent? content = null)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = content;
        return await client.SendAsync(request);
    }

    private sealed record AuthResponse(string AccessToken, string RefreshToken, string ExpiresAt, AuthUserResponse User, IReadOnlyCollection<object> Households);

    private sealed record AuthUserResponse(string Id, string Email, string DisplayName, string SystemRole);

    private sealed record ToolImprovementSuggestionResponse(
        Guid Id,
        Guid UserId,
        string UserDisplayName,
        string UserEmail,
        DateTimeOffset SubmittedAt,
        string SuggestionText,
        string Status,
        string Priority,
        string? InternalComment,
        DateTimeOffset? LastReviewedAt,
        Guid? LastReviewedByUserId,
        string? LastReviewedByDisplayName);

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
                    ["ObjectStorage:CreateBucketOnStartup"] = "true",
                    ["SuperAdmin:Email"] = "superadmin@homepit.dev",
                    ["SuperAdmin:Password"] = "super-secret",
                    ["SuperAdmin:DisplayName"] = "SuperAdmin"
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
