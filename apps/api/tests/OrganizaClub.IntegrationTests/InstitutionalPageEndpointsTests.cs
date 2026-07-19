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

public sealed class InstitutionalPageEndpointsTests
{
    [Fact]
    public async Task Public_content_is_available_without_authentication()
    {
        await using var factory = new OrganizaClubApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/institutional-page");

        response.EnsureSuccessStatusCode();
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        var content = await response.Content.ReadFromJsonAsync<InstitutionalPageResponse>(JsonSerializerOptions.Web);
        Assert.NotNull(content);
        Assert.Equal("Organiza Club", content.BrandName);
        Assert.False(content.HasHeroImage);
    }

    [Fact]
    public async Task Admin_endpoints_require_superadmin()
    {
        await using var factory = new OrganizaClubApiFactory();
        using var client = factory.CreateClient();
        var userToken = await SeedAccessTokenAsync(factory, SystemRole.User);
        var adminToken = await SeedAccessTokenAsync(factory, SystemRole.Admin);
        var superAdminToken = await SeedAccessTokenAsync(factory, SystemRole.SuperAdmin);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/admin/institutional-page")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAuthorizedAsync(client, userToken, HttpMethod.Get, "/api/admin/institutional-page")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendAuthorizedAsync(client, adminToken, HttpMethod.Get, "/api/admin/institutional-page")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SendAuthorizedAsync(client, superAdminToken, HttpMethod.Get, "/api/admin/institutional-page")).StatusCode);
    }

    [Fact]
    public async Task Superadmin_updates_content_and_it_becomes_public_immediately()
    {
        await using var factory = new OrganizaClubApiFactory();
        using var client = factory.CreateClient();
        var token = await SeedAccessTokenAsync(factory, SystemRole.SuperAdmin);

        var updateResponse = await SendAuthorizedAsync(
            client,
            token,
            HttpMethod.Put,
            "/api/admin/institutional-page",
            JsonContent.Create(CreateRequest("Título publicada")));
        updateResponse.EnsureSuccessStatusCode();

        var publicResponse = await client.GetFromJsonAsync<InstitutionalPageResponse>(
            "/api/institutional-page",
            JsonSerializerOptions.Web);

        Assert.NotNull(publicResponse);
        Assert.Equal("Título publicada", publicResponse.HeroTitle);
        Assert.Equal(new[] { "Primeiro", "Segundo" }, publicResponse.Benefits.Select(item => item.Title));
    }

    [Fact]
    public async Task Superadmin_uploads_reads_and_deletes_public_image()
    {
        await using var factory = new OrganizaClubApiFactory();
        using var client = factory.CreateClient();
        var token = await SeedAccessTokenAsync(factory, SystemRole.SuperAdmin);
        var png = TestImageFactory.CreatePng(1800, 900);

        using var uploadRequest = new HttpRequestMessage(HttpMethod.Post, "/api/admin/institutional-page/images/hero");
        uploadRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var form = new MultipartFormDataContent();
        using var file = new ByteArrayContent(png);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(file, "file", "hero.png");
        uploadRequest.Content = form;

        var uploadResponse = await client.SendAsync(uploadRequest);
        uploadResponse.EnsureSuccessStatusCode();

        var publicImage = await client.GetAsync("/api/institutional-page/images/hero?v=1");
        publicImage.EnsureSuccessStatusCode();
        Assert.Equal("image/webp", publicImage.Content.Headers.ContentType?.MediaType);
        Assert.Equal("public, max-age=31536000, immutable", publicImage.Headers.CacheControl?.ToString());

        var deleteResponse = await SendAuthorizedAsync(
            client,
            token,
            HttpMethod.Delete,
            "/api/admin/institutional-page/images/hero");
        deleteResponse.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/institutional-page/images/hero?v=2")).StatusCode);
    }

    [Fact]
    public async Task Superadmin_uploads_reads_and_deletes_public_seo_image()
    {
        await using var factory = new OrganizaClubApiFactory();
        using var client = factory.CreateClient();
        var token = await SeedAccessTokenAsync(factory, SystemRole.SuperAdmin);
        var seoBytes = TestImageFactory.CreateWebp(1200, 630);

        using var uploadRequest = new HttpRequestMessage(HttpMethod.Post, "/api/admin/institutional-page/images/seo");
        uploadRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var form = new MultipartFormDataContent();
        using var file = new ByteArrayContent(seoBytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/webp");
        form.Add(file, "file", "seo.webp");
        uploadRequest.Content = form;

        var uploadResponse = await client.SendAsync(uploadRequest);
        uploadResponse.EnsureSuccessStatusCode();

        var publicImage = await client.GetAsync("/api/institutional-page/images/seo?v=1");
        publicImage.EnsureSuccessStatusCode();
        Assert.Equal("image/webp", publicImage.Content.Headers.ContentType?.MediaType);
        Assert.Equal(seoBytes, await publicImage.Content.ReadAsByteArrayAsync());

        var deleteResponse = await SendAuthorizedAsync(
            client,
            token,
            HttpMethod.Delete,
            "/api/admin/institutional-page/images/seo");
        deleteResponse.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/institutional-page/images/seo?v=2")).StatusCode);
    }

    private static object CreateRequest(string heroTitle) => new
    {
        seoTitle = "OrganizaClub institucional",
        seoDescription = "Descrição de busca",
        brandName = "Organiza Club",
        brandTagline = "Espaço organizado",
        heroEyebrow = "Destaque",
        heroTitle,
        heroDescription = "Descrição principal",
        primaryCtaLabel = "Falar conosco",
        primaryCtaUrl = "https://example.com/contact",
        benefitsTitle = "Benefícios",
        benefitsDescription = "Descrição dos benefícios",
        benefits = new[]
        {
            new { title = "Primeiro", description = "Descrição 1" },
            new { title = "Segundo", description = "Descrição 2" }
        },
        stepsTitle = "Como funciona",
        stepsDescription = "Descrição das etapas",
        steps = new[]
        {
            new { title = "Etapa 1", description = "Descrição 1" }
        },
        highlightEyebrow = "Produto",
        highlightTitle = "Destaque do produto",
        highlightDescription = "Descrição do produto",
        finalCtaTitle = "Chamada final",
        finalCtaDescription = "Descrição final",
        footerText = "Texto do rodapé",
        heroImageAlt = "Imagem principal",
        highlightImageAlt = "Imagem de destaque"
    };

    private static async Task<string> SeedAccessTokenAsync(OrganizaClubApiFactory factory, SystemRole role)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OrganizaClubDbContext>();
        var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();
        var user = new AppUser
        {
            Email = $"{role.ToString().ToLowerInvariant()}-{Guid.NewGuid():N}@organiza.club",
            PasswordHash = "hash",
            DisplayName = role.ToString(),
            SystemRole = role
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return tokenService.CreateAccessToken(user, Array.Empty<SpaceMember>());
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

    private sealed record InstitutionalPageResponse(
        string BrandName,
        string HeroTitle,
        bool HasHeroImage,
        IReadOnlyCollection<InstitutionalItemResponse> Benefits);

    private sealed record InstitutionalItemResponse(int Position, string Title, string Description);

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
