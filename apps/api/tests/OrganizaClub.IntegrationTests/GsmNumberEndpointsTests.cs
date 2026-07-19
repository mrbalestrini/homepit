using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using OrganizaClub.Application.Auth;
using OrganizaClub.Application.Common;
using OrganizaClub.Application.Storage;
using OrganizaClub.Domain.Spaces;
using OrganizaClub.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace OrganizaClub.IntegrationTests;

public sealed class GsmNumberEndpointsTests
{
    [Fact]
    public async Task Gsm_number_crud_normalizes_number_and_lists_records()
    {
        await using var factory = new OrganizaClubApiFactory();
        using var client = factory.CreateClient();
        var seed = await SeedAsync(factory);

        var createResponse = await SendAuthorizedAsync(
            client,
            seed.AccessToken,
            seed.SpaceId,
            HttpMethod.Post,
            "/api/gsm-numbers",
            JsonContent.Create(new
            {
                title = "Linha principal",
                number = "(11) 91234-5678",
                description = "Uso diário",
                plan = "PrePago",
                monthlyCost = 59.9m,
                daysWithoutRecharge = 30,
                acquiredOn = new DateOnly(2026, 1, 10),
                status = "Ativo"
            }));

        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<GsmNumberResponse>(JsonSerializerOptions.Web);
        Assert.NotNull(created);
        Assert.Equal("5511912345678", created.Number);
        Assert.Equal("Ativo", created.Status);
        Assert.Equal("PrePago", created.Plan);
        Assert.Equal(59.9m, created.MonthlyCost);
        Assert.Equal(30, created.DaysWithoutRecharge);

        var listResponse = await SendAuthorizedAsync(
            client,
            seed.AccessToken,
            seed.SpaceId,
            HttpMethod.Get,
            "/api/gsm-numbers");

        listResponse.EnsureSuccessStatusCode();
        var numbers = await listResponse.Content.ReadFromJsonAsync<IReadOnlyCollection<GsmNumberResponse>>(JsonSerializerOptions.Web);
        var listed = Assert.Single(numbers!);
        Assert.Equal(created.Id, listed.Id);

        var updateResponse = await SendAuthorizedAsync(
            client,
            seed.AccessToken,
            seed.SpaceId,
            HttpMethod.Put,
            $"/api/gsm-numbers/{created.Id}",
            JsonContent.Create(new
            {
                title = "Linha reserva",
                number = "+44 (11) 91234-5678",
                description = "Uso eventual",
                plan = "PosPago",
                monthlyCost = 72.5m,
                daysWithoutRecharge = 45,
                acquiredOn = new DateOnly(2026, 1, 10),
                status = "Inativo"
            }));

        updateResponse.EnsureSuccessStatusCode();
        var updated = await updateResponse.Content.ReadFromJsonAsync<GsmNumberResponse>(JsonSerializerOptions.Web);
        Assert.NotNull(updated);
        Assert.Equal("4411912345678", updated.Number);
        Assert.Equal("Inativo", updated.Status);
        Assert.Equal("PosPago", updated.Plan);
        Assert.Equal(72.5m, updated.MonthlyCost);
        Assert.Equal(45, updated.DaysWithoutRecharge);

        var deleteResponse = await SendAuthorizedAsync(
            client,
            seed.AccessToken,
            seed.SpaceId,
            HttpMethod.Delete,
            $"/api/gsm-numbers/{created.Id}");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task Gsm_number_create_validates_number_and_dates()
    {
        await using var factory = new OrganizaClubApiFactory();
        using var client = factory.CreateClient();
        var seed = await SeedAsync(factory);

        var response = await SendAuthorizedAsync(
            client,
            seed.AccessToken,
            seed.SpaceId,
            HttpMethod.Post,
            "/api/gsm-numbers",
            JsonContent.Create(new
            {
                title = "Linha inválida",
                number = "1234567890",
                description = (string?)null,
                plan = "PrePago",
                monthlyCost = (decimal?)null,
                daysWithoutRecharge = 0,
                acquiredOn = new DateOnly(2026, 6, 20),
                status = "Ativo"
            }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsResponse>(JsonSerializerOptions.Web);
        Assert.NotNull(problem);
        Assert.Equal("Informe um número GSM válido com DDI opcional e DDD obrigatório.", problem.Detail);
    }

    [Fact]
    public async Task Gsm_recharge_history_updates_the_last_recharge_summary()
    {
        await using var factory = new OrganizaClubApiFactory();
        using var client = factory.CreateClient();
        var seed = await SeedAsync(factory);

        var createResponse = await SendAuthorizedAsync(
            client,
            seed.AccessToken,
            seed.SpaceId,
            HttpMethod.Post,
            "/api/gsm-numbers",
            JsonContent.Create(new
            {
                title = "Linha principal",
                number = "11912345678",
                description = "Uso diário",
                plan = "PrePago",
                monthlyCost = 59.9m,
                daysWithoutRecharge = 30,
                acquiredOn = new DateOnly(2026, 1, 10),
                status = "Ativo"
            }));

        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<GsmNumberResponse>(JsonSerializerOptions.Web);
        Assert.NotNull(created);

        var rechargeResponse = await SendAuthorizedAsync(
            client,
            seed.AccessToken,
            seed.SpaceId,
            HttpMethod.Post,
            $"/api/gsm-numbers/{created!.Id}/recharges",
            JsonContent.Create(new
            {
                rechargedOn = new DateOnly(2026, 6, 20),
                amount = 50m,
                note = "Primeira recarga"
            }));

        rechargeResponse.EnsureSuccessStatusCode();
        var recharge = await rechargeResponse.Content.ReadFromJsonAsync<GsmRechargeResponse>(JsonSerializerOptions.Web);
        Assert.NotNull(recharge);
        Assert.Equal(new DateOnly(2026, 6, 20), recharge!.RechargedOn);
        Assert.Equal(50m, recharge.Amount);
        Assert.Equal("Primeira recarga", recharge.Note);

        var listRechargeResponse = await SendAuthorizedAsync(
            client,
            seed.AccessToken,
            seed.SpaceId,
            HttpMethod.Get,
            $"/api/gsm-numbers/{created.Id}/recharges");

        listRechargeResponse.EnsureSuccessStatusCode();
        var recharges = await listRechargeResponse.Content.ReadFromJsonAsync<IReadOnlyCollection<GsmRechargeResponse>>(JsonSerializerOptions.Web);
        Assert.Single(recharges!);

        var updateRechargeResponse = await SendAuthorizedAsync(
            client,
            seed.AccessToken,
            seed.SpaceId,
            HttpMethod.Put,
            $"/api/gsm-numbers/{created.Id}/recharges/{recharge.Id}",
            JsonContent.Create(new
            {
                rechargedOn = new DateOnly(2026, 6, 22),
                amount = 60m,
                note = "Ajustada"
            }));

        updateRechargeResponse.EnsureSuccessStatusCode();
        var updatedRecharge = await updateRechargeResponse.Content.ReadFromJsonAsync<GsmRechargeResponse>(JsonSerializerOptions.Web);
        Assert.NotNull(updatedRecharge);
        Assert.Equal(new DateOnly(2026, 6, 22), updatedRecharge!.RechargedOn);
        Assert.Equal(60m, updatedRecharge.Amount);
        Assert.Equal("Ajustada", updatedRecharge.Note);

        var gsmListResponse = await SendAuthorizedAsync(
            client,
            seed.AccessToken,
            seed.SpaceId,
            HttpMethod.Get,
            "/api/gsm-numbers");

        gsmListResponse.EnsureSuccessStatusCode();
        var listedNumbers = await gsmListResponse.Content.ReadFromJsonAsync<IReadOnlyCollection<GsmNumberResponse>>(JsonSerializerOptions.Web);
        var listedNumber = Assert.Single(listedNumbers!);
        Assert.Equal(new DateOnly(2026, 6, 22), listedNumber.LastRechargeOn);

        var deleteRechargeResponse = await SendAuthorizedAsync(
            client,
            seed.AccessToken,
            seed.SpaceId,
            HttpMethod.Delete,
            $"/api/gsm-numbers/{created.Id}/recharges/{recharge.Id}");

        Assert.Equal(HttpStatusCode.NoContent, deleteRechargeResponse.StatusCode);

        var afterDeleteResponse = await SendAuthorizedAsync(
            client,
            seed.AccessToken,
            seed.SpaceId,
            HttpMethod.Get,
            "/api/gsm-numbers");

        afterDeleteResponse.EnsureSuccessStatusCode();
        var afterDeleteNumbers = await afterDeleteResponse.Content.ReadFromJsonAsync<IReadOnlyCollection<GsmNumberResponse>>(JsonSerializerOptions.Web);
        var afterDeleteNumber = Assert.Single(afterDeleteNumbers!);
        Assert.Null(afterDeleteNumber.LastRechargeOn);
    }

    private static async Task<SeedResult> SeedAsync(OrganizaClubApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OrganizaClubDbContext>();
        var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();

        var user = new AppUser
        {
            Email = $"gsm-owner-{Guid.NewGuid():N}@organiza.club",
            PasswordHash = "hash",
            DisplayName = "Owner",
            SystemRole = SystemRole.User
        };
        var space = new Space
        {
            Name = "Espaço GSM"
        };
        var member = new SpaceMember
        {
            Space = space,
            User = user,
            Role = SpaceRole.Owner
        };

        db.Users.Add(user);
        db.Spaces.Add(space);
        db.SpaceMembers.Add(member);
        await db.SaveChangesAsync();

        return new SeedResult(tokenService.CreateAccessToken(user, [member]), space.Id);
    }

    private static HttpRequestMessage CreateAuthorizedRequest(
        string accessToken,
        Guid spaceId,
        HttpMethod method,
        string path)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Add("X-Space-Id", spaceId.ToString());
        return request;
    }

    private static async Task<HttpResponseMessage> SendAuthorizedAsync(
        HttpClient client,
        string accessToken,
        Guid spaceId,
        HttpMethod method,
        string path,
        HttpContent? content = null)
    {
        using var request = CreateAuthorizedRequest(accessToken, spaceId, method, path);
        request.Content = content;
        return await client.SendAsync(request);
    }

    private sealed record SeedResult(string AccessToken, Guid SpaceId);

    private sealed record GsmNumberResponse(
        Guid Id,
        string Number,
        string Status,
        string Plan,
        decimal? MonthlyCost,
        int? DaysWithoutRecharge,
        DateOnly? LastRechargeOn);

    private sealed record GsmRechargeResponse(Guid Id, Guid GsmNumberId, DateOnly RechargedOn, decimal? Amount, string? Note);

    private sealed record ProblemDetailsResponse(string Detail);

    private sealed class OrganizaClubApiFactory : WebApplicationFactory<Program>, IAsyncDisposable
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
                services.RemoveAll<DbContextOptions<OrganizaClubDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<OrganizaClubDbContext>>();
                services.RemoveAll<OrganizaClubDbContext>();
                services.RemoveAll<IOrganizaClubDbContext>();
                services.RemoveAll<IObjectStorage>();

                services.AddDbContext<OrganizaClubDbContext>(options => options.UseInMemoryDatabase(databaseName));
                services.AddScoped<IOrganizaClubDbContext>(provider => provider.GetRequiredService<OrganizaClubDbContext>());
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
