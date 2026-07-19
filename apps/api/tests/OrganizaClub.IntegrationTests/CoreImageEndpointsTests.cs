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

public sealed class CoreImageEndpointsTests
{
    [Fact]
    public async Task Core_image_upload_get_and_delete_work()
    {
        await using var factory = new OrganizaClubApiFactory();
        using var client = factory.CreateClient();
        var seed = await SeedAsync(factory);
        var jpeg = TestImageFactory.CreateJpeg(900, 600);

        using var uploadRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/cores/{seed.CoreId}/image");
        uploadRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", seed.AccessToken);
        uploadRequest.Headers.Add("X-Space-Id", seed.SpaceId.ToString());
        using var form = new MultipartFormDataContent();
        using var file = new ByteArrayContent(jpeg);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        form.Add(file, "file", "core.jpg");
        uploadRequest.Content = form;

        var uploadResponse = await client.SendAsync(uploadRequest);
        uploadResponse.EnsureSuccessStatusCode();

        var uploaded = await uploadResponse.Content.ReadFromJsonAsync<CoreResponse>(JsonSerializerOptions.Web);
        Assert.NotNull(uploaded);
        Assert.True(uploaded.HasImage);
        Assert.NotNull(uploaded.ImageUpdatedAt);

        using var getRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/cores/{seed.CoreId}/image");
        getRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", seed.AccessToken);
        getRequest.Headers.Add("X-Space-Id", seed.SpaceId.ToString());

        var getResponse = await client.SendAsync(getRequest);
        getResponse.EnsureSuccessStatusCode();
        Assert.Equal("image/webp", getResponse.Content.Headers.ContentType?.MediaType);

        using var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/cores/{seed.CoreId}/image");
        deleteRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", seed.AccessToken);
        deleteRequest.Headers.Add("X-Space-Id", seed.SpaceId.ToString());

        var deleteResponse = await client.SendAsync(deleteRequest);
        deleteResponse.EnsureSuccessStatusCode();

        using var missingRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/cores/{seed.CoreId}/image");
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

        db.Users.Add(user);
        db.Spaces.Add(space);
        db.SpaceMembers.Add(member);
        db.Cores.Add(core);
        await db.SaveChangesAsync();

        return new SeedResult(
            tokenService.CreateAccessToken(user, [member]),
            space.Id,
            core.Id);
    }

    private sealed record SeedResult(string AccessToken, Guid SpaceId, Guid CoreId);

    private sealed record CoreResponse(bool HasImage, string? ImageUpdatedAt);

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
