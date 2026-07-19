using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using OrganizaClub.Application.Auth;
using OrganizaClub.Application.Common;
using OrganizaClub.Application.Storage;
using OrganizaClub.Domain.Spaces;
using OrganizaClub.Domain.Projects;
using OrganizaClub.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace OrganizaClub.IntegrationTests;

public sealed class ActivityImageEndpointsTests
{
    [Fact]
    public async Task Activity_image_upload_get_and_delete_work()
    {
        await using var factory = new OrganizaClubApiFactory();
        using var client = factory.CreateClient();
        var seed = await SeedAsync(factory);
        var png = TestImageFactory.CreatePng(640, 360);

        using var uploadRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/activities/{seed.ActivityId}/image");
        uploadRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", seed.AccessToken);
        uploadRequest.Headers.Add("X-Space-Id", seed.SpaceId.ToString());
        using var form = new MultipartFormDataContent();
        using var file = new ByteArrayContent(png);
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
        getRequest.Headers.Add("X-Space-Id", seed.SpaceId.ToString());

        var getResponse = await client.SendAsync(getRequest);
        getResponse.EnsureSuccessStatusCode();
        Assert.Equal("image/webp", getResponse.Content.Headers.ContentType?.MediaType);

        using var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/activities/{seed.ActivityId}/image");
        deleteRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", seed.AccessToken);
        deleteRequest.Headers.Add("X-Space-Id", seed.SpaceId.ToString());

        var deleteResponse = await client.SendAsync(deleteRequest);
        deleteResponse.EnsureSuccessStatusCode();

        using var missingRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/activities/{seed.ActivityId}/image");
        missingRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", seed.AccessToken);
        missingRequest.Headers.Add("X-Space-Id", seed.SpaceId.ToString());
        Assert.Equal(HttpStatusCode.NotFound, (await client.SendAsync(missingRequest)).StatusCode);
    }

    private static async Task<SeedResult> SeedAsync(OrganizaClubApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OrganizaClubDbContext>();
        var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();

        var user = new AppUser
        {
            Email = $"owner-{Guid.NewGuid():N}@organiza.club",
            PasswordHash = "hash",
            DisplayName = "Owner",
            SystemRole = SystemRole.User
        };
        var space = new Space
        {
            Name = "Espaço de teste"
        };
        var member = new SpaceMember
        {
            Space = space,
            User = user,
            Role = SpaceRole.Owner
        };
        var core = new Core
        {
            Space = space,
            CreatedByMember = member,
            Name = "Núcleo"
        };
        var project = new Project
        {
            SpaceId = space.Id,
            Core = core,
            CreatedByMember = member,
            Name = "Projeto"
        };
        var activity = new Activity
        {
            SpaceId = space.Id,
            Project = project,
            CreatedByMember = member,
            Title = "Atividade com imagem"
        };

        db.Users.Add(user);
        db.Spaces.Add(space);
        db.SpaceMembers.Add(member);
        db.Cores.Add(core);
        db.Projects.Add(project);
        db.Activities.Add(activity);
        await db.SaveChangesAsync();

        return new SeedResult(
            tokenService.CreateAccessToken(user, [member]),
            space.Id,
            activity.Id);
    }

    private sealed record SeedResult(string AccessToken, Guid SpaceId, Guid ActivityId);

    private sealed record ActivityResponse(bool HasImage, string? ImageUpdatedAt);

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
                    ["ObjectStorage:CreateBucketOnStartup"] = "true"
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
