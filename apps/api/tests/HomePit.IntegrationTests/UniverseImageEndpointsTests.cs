using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using HomePit.Application.Auth;
using HomePit.Application.Common;
using HomePit.Application.Storage;
using HomePit.Domain.Households;
using HomePit.Domain.Projects;
using HomePit.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace HomePit.IntegrationTests;

public sealed class UniverseImageEndpointsTests
{
    [Fact]
    public async Task Universe_image_upload_get_and_delete_work()
    {
        await using var factory = new HomePitApiFactory();
        using var client = factory.CreateClient();
        var seed = await SeedAsync(factory);
        var jpeg = TestImageFactory.CreateJpeg(900, 600);

        using var uploadRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/universes/{seed.UniverseId}/image");
        uploadRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", seed.AccessToken);
        uploadRequest.Headers.Add("X-Household-Id", seed.HouseholdId.ToString());
        using var form = new MultipartFormDataContent();
        using var file = new ByteArrayContent(jpeg);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        form.Add(file, "file", "universe.jpg");
        uploadRequest.Content = form;

        var uploadResponse = await client.SendAsync(uploadRequest);
        uploadResponse.EnsureSuccessStatusCode();

        var uploaded = await uploadResponse.Content.ReadFromJsonAsync<UniverseResponse>(JsonSerializerOptions.Web);
        Assert.NotNull(uploaded);
        Assert.True(uploaded.HasImage);
        Assert.NotNull(uploaded.ImageUpdatedAt);

        using var getRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/universes/{seed.UniverseId}/image");
        getRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", seed.AccessToken);
        getRequest.Headers.Add("X-Household-Id", seed.HouseholdId.ToString());

        var getResponse = await client.SendAsync(getRequest);
        getResponse.EnsureSuccessStatusCode();
        Assert.Equal("image/webp", getResponse.Content.Headers.ContentType?.MediaType);

        using var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/universes/{seed.UniverseId}/image");
        deleteRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", seed.AccessToken);
        deleteRequest.Headers.Add("X-Household-Id", seed.HouseholdId.ToString());

        var deleteResponse = await client.SendAsync(deleteRequest);
        deleteResponse.EnsureSuccessStatusCode();

        using var missingRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/universes/{seed.UniverseId}/image");
        missingRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", seed.AccessToken);
        missingRequest.Headers.Add("X-Household-Id", seed.HouseholdId.ToString());
        Assert.Equal(HttpStatusCode.NotFound, (await client.SendAsync(missingRequest)).StatusCode);
    }

    private static async Task<SeedResult> SeedAsync(HomePitApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HomePitDbContext>();
        var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();

        var user = new AppUser
        {
            Email = $"owner-{Guid.NewGuid():N}@homepit.dev",
            PasswordHash = "hash",
            DisplayName = "Owner",
            SystemRole = SystemRole.User
        };
        var household = new Household
        {
            Name = "Casa de teste"
        };
        var member = new HouseholdMember
        {
            Household = household,
            User = user,
            Role = HouseholdRole.Owner
        };
        var universe = new Universe
        {
            Household = household,
            CreatedByMember = member,
            Name = "Universo"
        };

        db.Users.Add(user);
        db.Households.Add(household);
        db.HouseholdMembers.Add(member);
        db.Universes.Add(universe);
        await db.SaveChangesAsync();

        return new SeedResult(
            tokenService.CreateAccessToken(user, [member]),
            household.Id,
            universe.Id);
    }

    private sealed record SeedResult(string AccessToken, Guid HouseholdId, Guid UniverseId);

    private sealed record UniverseResponse(bool HasImage, string? ImageUpdatedAt);

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
                    ["ObjectStorage:CreateBucketOnStartup"] = "true"
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
        private readonly Dictionary<string, StoredObject> objects = [];

        public Task EnsureBucketExistsAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<StoredObject> GetAsync(string objectKey, CancellationToken cancellationToken)
        {
            if (!objects.TryGetValue(objectKey, out var storedObject))
            {
                throw new NotFoundException("Arquivo não encontrado.");
            }

            return Task.FromResult(storedObject);
        }

        public async Task PutAsync(ObjectStoragePutRequest request, CancellationToken cancellationToken)
        {
            using var buffer = new MemoryStream();
            await request.Content.CopyToAsync(buffer, cancellationToken);
            objects[request.ObjectKey] = new StoredObject(request.ObjectKey, buffer.ToArray(), request.ContentType);
        }

        public Task DeleteAsync(string objectKey, CancellationToken cancellationToken)
        {
            objects.Remove(objectKey);
            return Task.CompletedTask;
        }
    }
}
