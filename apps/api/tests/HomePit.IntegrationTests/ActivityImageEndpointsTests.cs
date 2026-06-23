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

public sealed class ActivityImageEndpointsTests
{
    [Fact]
    public async Task Activity_image_upload_get_and_delete_work()
    {
        await using var factory = new HomePitApiFactory();
        using var client = factory.CreateClient();
        var seed = await SeedAsync(factory);

        using var uploadRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/activities/{seed.ActivityId}/image");
        uploadRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", seed.AccessToken);
        uploadRequest.Headers.Add("X-Household-Id", seed.HouseholdId.ToString());
        using var form = new MultipartFormDataContent();
        using var file = new ByteArrayContent([7, 8, 9, 10]);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(file, "file", "activity.png");
        uploadRequest.Content = form;

        var uploadResponse = await client.SendAsync(uploadRequest);
        uploadResponse.EnsureSuccessStatusCode();

        var uploaded = await uploadResponse.Content.ReadFromJsonAsync<ActivityResponse>(JsonSerializerOptions.Web);
        Assert.NotNull(uploaded);
        Assert.True(uploaded.HasImage);
        Assert.NotNull(uploaded.ImageUpdatedAt);

        using var getRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/activities/{seed.ActivityId}/image");
        getRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", seed.AccessToken);
        getRequest.Headers.Add("X-Household-Id", seed.HouseholdId.ToString());

        var getResponse = await client.SendAsync(getRequest);
        getResponse.EnsureSuccessStatusCode();
        Assert.Equal("image/png", getResponse.Content.Headers.ContentType?.MediaType);
        Assert.Equal([7, 8, 9, 10], await getResponse.Content.ReadAsByteArrayAsync());

        using var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/activities/{seed.ActivityId}/image");
        deleteRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", seed.AccessToken);
        deleteRequest.Headers.Add("X-Household-Id", seed.HouseholdId.ToString());

        var deleteResponse = await client.SendAsync(deleteRequest);
        deleteResponse.EnsureSuccessStatusCode();

        using var missingRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/activities/{seed.ActivityId}/image");
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
        var project = new Project
        {
            HouseholdId = household.Id,
            Universe = universe,
            CreatedByMember = member,
            Name = "Projeto"
        };
        var activity = new Activity
        {
            HouseholdId = household.Id,
            Project = project,
            CreatedByMember = member,
            Title = "Atividade com imagem"
        };

        db.Users.Add(user);
        db.Households.Add(household);
        db.HouseholdMembers.Add(member);
        db.Universes.Add(universe);
        db.Projects.Add(project);
        db.Activities.Add(activity);
        await db.SaveChangesAsync();

        return new SeedResult(
            tokenService.CreateAccessToken(user, [member]),
            household.Id,
            activity.Id);
    }

    private sealed record SeedResult(string AccessToken, Guid HouseholdId, Guid ActivityId);

    private sealed record ActivityResponse(bool HasImage, string? ImageUpdatedAt);

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
