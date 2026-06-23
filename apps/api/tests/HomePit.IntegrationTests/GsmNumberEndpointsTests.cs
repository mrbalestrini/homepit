using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using HomePit.Application.Auth;
using HomePit.Application.Common;
using HomePit.Application.Storage;
using HomePit.Domain.Households;
using HomePit.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace HomePit.IntegrationTests;

public sealed class GsmNumberEndpointsTests
{
    [Fact]
    public async Task Gsm_number_crud_normalizes_number_and_lists_records()
    {
        await using var factory = new HomePitApiFactory();
        using var client = factory.CreateClient();
        var seed = await SeedAsync(factory);

        var createResponse = await SendAuthorizedAsync(
            client,
            seed.AccessToken,
            seed.HouseholdId,
            HttpMethod.Post,
            "/api/gsm-numbers",
            JsonContent.Create(new
            {
                title = "Linha principal",
                number = "(11) 91234-5678",
                description = "Uso diário",
                acquiredOn = new DateOnly(2026, 1, 10),
                lastRechargeOn = new DateOnly(2026, 6, 20),
                status = "Ativo"
            }));

        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<GsmNumberResponse>(JsonSerializerOptions.Web);
        Assert.NotNull(created);
        Assert.Equal("5511912345678", created.Number);
        Assert.Equal("Ativo", created.Status);

        var listResponse = await SendAuthorizedAsync(
            client,
            seed.AccessToken,
            seed.HouseholdId,
            HttpMethod.Get,
            "/api/gsm-numbers");

        listResponse.EnsureSuccessStatusCode();
        var numbers = await listResponse.Content.ReadFromJsonAsync<IReadOnlyCollection<GsmNumberResponse>>(JsonSerializerOptions.Web);
        var listed = Assert.Single(numbers!);
        Assert.Equal(created.Id, listed.Id);

        var updateResponse = await SendAuthorizedAsync(
            client,
            seed.AccessToken,
            seed.HouseholdId,
            HttpMethod.Put,
            $"/api/gsm-numbers/{created.Id}",
            JsonContent.Create(new
            {
                title = "Linha reserva",
                number = "+44 (11) 91234-5678",
                description = "Uso eventual",
                acquiredOn = new DateOnly(2026, 1, 10),
                lastRechargeOn = new DateOnly(2026, 6, 22),
                status = "Inativo"
            }));

        updateResponse.EnsureSuccessStatusCode();
        var updated = await updateResponse.Content.ReadFromJsonAsync<GsmNumberResponse>(JsonSerializerOptions.Web);
        Assert.NotNull(updated);
        Assert.Equal("4411912345678", updated.Number);
        Assert.Equal("Inativo", updated.Status);

        var deleteResponse = await SendAuthorizedAsync(
            client,
            seed.AccessToken,
            seed.HouseholdId,
            HttpMethod.Delete,
            $"/api/gsm-numbers/{created.Id}");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task Gsm_number_create_validates_number_and_dates()
    {
        await using var factory = new HomePitApiFactory();
        using var client = factory.CreateClient();
        var seed = await SeedAsync(factory);

        var response = await SendAuthorizedAsync(
            client,
            seed.AccessToken,
            seed.HouseholdId,
            HttpMethod.Post,
            "/api/gsm-numbers",
            JsonContent.Create(new
            {
                title = "Linha inválida",
                number = "1234567890",
                description = (string?)null,
                acquiredOn = new DateOnly(2026, 6, 20),
                lastRechargeOn = new DateOnly(2026, 6, 10),
                status = "Ativo"
            }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsResponse>(JsonSerializerOptions.Web);
        Assert.NotNull(problem);
        Assert.Equal("Informe um número GSM válido com DDI opcional e DDD obrigatório.", problem.Detail);
    }

    private static async Task<SeedResult> SeedAsync(HomePitApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HomePitDbContext>();
        var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();

        var user = new AppUser
        {
            Email = $"gsm-owner-{Guid.NewGuid():N}@homepit.dev",
            PasswordHash = "hash",
            DisplayName = "Owner",
            SystemRole = SystemRole.User
        };
        var household = new Household
        {
            Name = "Casa GSM"
        };
        var member = new HouseholdMember
        {
            Household = household,
            User = user,
            Role = HouseholdRole.Owner
        };

        db.Users.Add(user);
        db.Households.Add(household);
        db.HouseholdMembers.Add(member);
        await db.SaveChangesAsync();

        return new SeedResult(tokenService.CreateAccessToken(user, [member]), household.Id);
    }

    private static HttpRequestMessage CreateAuthorizedRequest(
        string accessToken,
        Guid householdId,
        HttpMethod method,
        string path)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Add("X-Household-Id", householdId.ToString());
        return request;
    }

    private static async Task<HttpResponseMessage> SendAuthorizedAsync(
        HttpClient client,
        string accessToken,
        Guid householdId,
        HttpMethod method,
        string path,
        HttpContent? content = null)
    {
        using var request = CreateAuthorizedRequest(accessToken, householdId, method, path);
        request.Content = content;
        return await client.SendAsync(request);
    }

    private sealed record SeedResult(string AccessToken, Guid HouseholdId);

    private sealed record GsmNumberResponse(Guid Id, string Number, string Status);

    private sealed record ProblemDetailsResponse(string Detail);

    private sealed class HomePitApiFactory : WebApplicationFactory<Program>, IAsyncDisposable
    {
        private readonly string databaseName = Guid.NewGuid().ToString("N");

        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            builder.UseSetting(Microsoft.AspNetCore.Hosting.WebHostDefaults.EnvironmentKey, "Development");
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Database:ApplyMigrationsOnStartup"] = "false",
                    ["Notifications:DailyDigestEnabled"] = "false",
                    ["ObjectStorage:CreateBucketOnStartup"] = "false"
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
                services.AddSingleton<IObjectStorage, FakeObjectStorage>();
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

        public Task PutAsync(ObjectStoragePutRequest request, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<StoredObject> GetAsync(string objectKey, CancellationToken cancellationToken) =>
            throw new NotFoundException("Arquivo não encontrado.");

        public Task DeleteAsync(string objectKey, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
