using HomePit.Application.Common;
using HomePit.Application.Institutional;
using HomePit.Application.Storage;
using HomePit.Domain.Households;
using HomePit.Infrastructure.Data;
using HomePit.Infrastructure.Images;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HomePit.Api.Tests;

public sealed class InstitutionalPageServiceTests
{
    [Fact]
    public async Task Public_content_uses_defaults_without_persisting_a_page()
    {
        var context = CreateContext(SystemRole.User);

        var result = await context.Service.GetPublicAsync(CancellationToken.None);

        Assert.Equal("home", result.Slug);
        Assert.Equal("HomePit", result.BrandName);
        Assert.Equal(3, result.Benefits.Count);
        Assert.Equal(3, result.Steps.Count);
        Assert.Null(result.UpdatedAt);
        Assert.Empty(context.Db.InstitutionalPages);
    }

    [Fact]
    public async Task Superadmin_can_upsert_and_reorder_content()
    {
        var context = CreateContext(SystemRole.SuperAdmin);
        var request = CreateRequest(
            benefits:
            [
                new("Segundo", "Descrição 2"),
                new("Primeiro", "Descrição 1")
            ],
            steps:
            [
                new("Etapa B", "Descrição B"),
                new("Etapa A", "Descrição A")
            ]);

        var first = await context.Service.UpdateAsync(request, CancellationToken.None);
        var second = await context.Service.UpdateAsync(
            request with
            {
                Benefits =
                [
                    new("Primeiro", "Descrição 1"),
                    new("Segundo", "Descrição 2")
                ]
            },
            CancellationToken.None);

        Assert.Equal("Segundo", first.Benefits.First().Title);
        Assert.Equal(new[] { "Primeiro", "Segundo" }, second.Benefits.Select(item => item.Title));
        Assert.Single(context.Db.InstitutionalPages);
        Assert.Equal(2, await context.Db.InstitutionalBenefits.CountAsync());
        Assert.Equal(2, await context.Db.InstitutionalSteps.CountAsync());
    }

    [Theory]
    [InlineData(SystemRole.User)]
    [InlineData(SystemRole.Admin)]
    public async Task Non_superadmin_cannot_manage_content(SystemRole role)
    {
        var context = CreateContext(role);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            context.Service.GetAdminAsync(CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            context.Service.UpdateAsync(CreateRequest(), CancellationToken.None));
    }

    [Fact]
    public async Task Rejects_invalid_primary_cta_url_and_list_size()
    {
        var context = CreateContext(SystemRole.SuperAdmin);

        var urlException = await Assert.ThrowsAsync<ValidationException>(() =>
            context.Service.UpdateAsync(
                CreateRequest() with { PrimaryCtaUrl = "javascript:alert(1)" },
                CancellationToken.None));
        var listException = await Assert.ThrowsAsync<ValidationException>(() =>
            context.Service.UpdateAsync(
                CreateRequest() with { Benefits = [] },
                CancellationToken.None));

        Assert.Contains("HTTP ou HTTPS", urlException.Message);
        Assert.Contains("entre 1 e 6 benefícios", listException.Message);
    }

    [Fact]
    public async Task Uploading_and_deleting_image_updates_public_metadata()
    {
        var context = CreateContext(SystemRole.SuperAdmin);
        var png = TestImageFactory.CreatePng(600, 400);
        await using var stream = new MemoryStream(png);

        var uploaded = await context.Service.UploadImageAsync(
            "hero",
            stream,
            stream.Length,
            "image/png",
            CancellationToken.None);
        var image = await context.Service.GetImageAsync("hero", CancellationToken.None);
        var deleted = await context.Service.DeleteImageAsync("hero", CancellationToken.None);

        Assert.True(uploaded.HasHeroImage);
        Assert.NotNull(uploaded.HeroImageUpdatedAt);
        Assert.Equal("image/webp", image.ContentType);
        Assert.False(deleted.HasHeroImage);
        Assert.Empty(context.Storage.Objects);
    }

    [Fact]
    public async Task Uploading_seo_image_requires_webp_1200x630_and_600_kb()
    {
        var context = CreateContext(SystemRole.SuperAdmin);
        var validSeo = TestImageFactory.CreateWebp(1200, 630);
        var invalidSeo = TestImageFactory.CreateWebp(1000, 630);

        var invalidTypeException = await Assert.ThrowsAsync<ValidationException>(() =>
            context.Service.UploadImageAsync(
                "seo",
                new MemoryStream(validSeo),
                validSeo.Length,
                "image/png",
                CancellationToken.None));

        var invalidSizeException = await Assert.ThrowsAsync<ValidationException>(() =>
            context.Service.UploadImageAsync(
                "seo",
                new MemoryStream(new byte[SEOImageMaxBytes + 1]),
                SEOImageMaxBytes + 1,
                "image/webp",
                CancellationToken.None));

        var invalidDimensionsException = await Assert.ThrowsAsync<ValidationException>(() =>
            context.Service.UploadImageAsync(
                "seo",
                new MemoryStream(invalidSeo),
                invalidSeo.Length,
                "image/webp",
                CancellationToken.None));

        Assert.Equal("A imagem de SEO deve estar em WEBP.", invalidTypeException.Message);
        Assert.Equal("A imagem de SEO deve ter no máximo 600 KB.", invalidSizeException.Message);
        Assert.Contains("1200 x 630", invalidDimensionsException.Message);
    }

    [Fact]
    public async Task Uploading_seo_image_updates_public_metadata()
    {
        var context = CreateContext(SystemRole.SuperAdmin);
        var bytes = TestImageFactory.CreateWebp(1200, 630);

        var uploaded = await context.Service.UploadImageAsync(
            "seo",
            new MemoryStream(bytes),
            bytes.Length,
            "image/webp",
            CancellationToken.None);
        var image = await context.Service.GetImageAsync("seo", CancellationToken.None);
        var deleted = await context.Service.DeleteImageAsync("seo", CancellationToken.None);

        Assert.True(uploaded.HasSeoImage);
        Assert.NotNull(uploaded.SeoImageUpdatedAt);
        Assert.Equal("image/webp", image.ContentType);
        Assert.Equal(bytes, image.Content);
        Assert.False(deleted.HasSeoImage);
    }

    private static TestContext CreateContext(SystemRole role)
    {
        var db = new HomePitDbContext(
            new DbContextOptionsBuilder<HomePitDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options);
        var storage = new FakeObjectStorage();
        var service = new InstitutionalPageService(
            db,
            new FakeUserContext(role),
            storage,
            new ImageSharpImageUploadProcessor(),
            new FakeTimeProvider(DateTimeOffset.Parse("2026-06-15T12:00:00+00:00")));

        return new TestContext(db, service, storage);
    }

    private static UpdateInstitutionalPageRequest CreateRequest(
        IReadOnlyCollection<InstitutionalContentItemRequest>? benefits = null,
        IReadOnlyCollection<InstitutionalContentItemRequest>? steps = null)
    {
        return new UpdateInstitutionalPageRequest(
            "HomePit institucional",
            "Descrição de busca",
            "HomePit",
            "Casa organizada",
            "Destaque",
            "Título principal",
            "Descrição principal",
            "Falar conosco",
            "https://example.com/contact",
            "Benefícios",
            "Descrição dos benefícios",
            benefits ?? [new("Benefício", "Descrição")],
            "Como funciona",
            "Descrição das etapas",
            steps ?? [new("Etapa", "Descrição")],
            "Produto",
            "Destaque do produto",
            "Descrição do produto",
            "Chamada final",
            "Descrição final",
            "Texto do rodapé",
            "Imagem principal",
            "Imagem de destaque");
    }

    private sealed record TestContext(
        HomePitDbContext Db,
        InstitutionalPageService Service,
        FakeObjectStorage Storage);

    private sealed class FakeUserContext(SystemRole role) : IUserContext
    {
        public Guid UserId => Guid.NewGuid();
        public SystemRole SystemRole { get; } = role;
        public Guid? HouseholdId => null;
    }

    private sealed class FakeTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class FakeObjectStorage : IObjectStorage
    {
        public Dictionary<string, StoredObject> Objects { get; } = [];

        public Task EnsureBucketExistsAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<StoredObject> GetAsync(string objectKey, CancellationToken cancellationToken)
        {
            if (!Objects.TryGetValue(objectKey, out var value))
            {
                throw new NotFoundException("Arquivo não encontrado.");
            }

            return Task.FromResult(value);
        }

        public async Task PutAsync(ObjectStoragePutRequest request, CancellationToken cancellationToken)
        {
            using var buffer = new MemoryStream();
            await request.Content.CopyToAsync(buffer, cancellationToken);
            Objects[request.ObjectKey] = new StoredObject(request.ObjectKey, buffer.ToArray(), request.ContentType);
        }

        public Task DeleteAsync(string objectKey, CancellationToken cancellationToken)
        {
            Objects.Remove(objectKey);
            return Task.CompletedTask;
        }
    }

    private const int SEOImageMaxBytes = 600 * 1024;
}
