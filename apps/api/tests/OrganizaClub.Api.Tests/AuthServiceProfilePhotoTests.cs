using OrganizaClub.Application.Auth;
using OrganizaClub.Application.Common;
using OrganizaClub.Application.Plans;
using OrganizaClub.Application.Storage;
using OrganizaClub.Domain.Spaces;
using OrganizaClub.Infrastructure.Images;
using OrganizaClub.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace OrganizaClub.Api.Tests;

public sealed class AuthServiceProfilePhotoTests
{
    [Fact]
    public async Task Rejects_empty_profile_photo()
    {
        var context = await CreateContextAsync();
        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            context.Service.UploadProfilePhotoAsync(new MemoryStream(), 0, "image/png", CancellationToken.None));

        Assert.Equal("Envie uma imagem com conteúdo para a foto de perfil.", exception.Message);
    }

    [Fact]
    public async Task Rejects_invalid_profile_photo_content_type()
    {
        var context = await CreateContextAsync();
        var png = TestImageFactory.CreatePng();
        await using var stream = new MemoryStream(png);

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            context.Service.UploadProfilePhotoAsync(stream, stream.Length, "image/tiff", CancellationToken.None));

        Assert.Equal("A foto de perfil deve estar em JPG, PNG, WEBP, GIF ou BMP.", exception.Message);
    }

    [Fact]
    public async Task Rejects_profile_photo_larger_than_limit()
    {
        var context = await CreateContextAsync();
        await using var stream = new MemoryStream(new byte[(5 * 1024 * 1024) + 1]);

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            context.Service.UploadProfilePhotoAsync(stream, stream.Length, "image/png", CancellationToken.None));

        Assert.Equal("A foto de perfil deve ter no máximo 5 MB.", exception.Message);
    }

    [Fact]
    public async Task Uploading_profile_photo_sets_storage_key_and_flags()
    {
        var context = await CreateContextAsync();
        var png = TestImageFactory.CreatePng(120, 120);
        await using var stream = new MemoryStream(png);

        var result = await context.Service.UploadProfilePhotoAsync(stream, stream.Length, "image/png", CancellationToken.None);
        var savedUser = await context.Db.Users.SingleAsync();

        Assert.True(result.HasProfilePhoto);
        Assert.NotNull(result.ProfilePhotoUpdatedAt);
        Assert.Equal(ObjectStorageKeys.UserProfilePhoto(savedUser.Id), savedUser.ProfilePhotoObjectKey);
        Assert.NotNull(savedUser.ProfilePhotoUpdatedAt);
        Assert.Single(context.Storage.Objects);
        Assert.Equal("image/webp", context.Storage.Objects.Single().Value.ContentType);
    }

    [Fact]
    public async Task Replacing_profile_photo_reuses_same_object_key()
    {
        var context = await CreateContextAsync();
        var firstPng = TestImageFactory.CreatePng(64, 64);
        var secondPng = TestImageFactory.CreatePng(96, 96);
        await using var firstStream = new MemoryStream(firstPng);
        await using var secondStream = new MemoryStream(secondPng);

        var first = await context.Service.UploadProfilePhotoAsync(firstStream, firstStream.Length, "image/png", CancellationToken.None);
        context.TimeProvider.UtcNow = context.TimeProvider.UtcNow.AddMinutes(5);
        var second = await context.Service.UploadProfilePhotoAsync(secondStream, secondStream.Length, "image/png", CancellationToken.None);

        Assert.True(second.HasProfilePhoto);
        Assert.NotNull(first.ProfilePhotoUpdatedAt);
        Assert.NotNull(second.ProfilePhotoUpdatedAt);
        Assert.NotEqual(first.ProfilePhotoUpdatedAt, second.ProfilePhotoUpdatedAt);
        Assert.Single(context.Storage.Objects);
        Assert.Equal(ObjectStorageKeys.UserProfilePhoto(context.UserId), context.Storage.Objects.Single().Key);
        Assert.Equal("image/webp", context.Storage.Objects.Single().Value.ContentType);
    }

    private static async Task<TestContext> CreateContextAsync()
    {
        var db = new OrganizaClubDbContext(
            new DbContextOptionsBuilder<OrganizaClubDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options);

        var user = new AppUser
        {
            Email = "ana@organiza.club",
            PasswordHash = "hash",
            DisplayName = "Ana",
            PhoneNumber = "+55 11 99999-0000",
            SystemRole = SystemRole.User
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        var timeProvider = new FakeTimeProvider(DateTimeOffset.Parse("2026-06-01T10:00:00+00:00"));
        var userContext = new FakeUserContext(user.Id);
        var storage = new FakeObjectStorage();
        var commercialPlanService = new CommercialPlanService(db, userContext, timeProvider);

        var service = new AuthService(
            db,
            new StubPasswordHasher(),
            new StubTokenService(),
            timeProvider,
            userContext,
            storage,
            new ImageSharpImageUploadProcessor(),
            new OrganizaClubDataPurgeService(db, storage),
            new SuperAdminOptions(),
            commercialPlanService);

        return new TestContext(db, service, storage, timeProvider, user.Id);
    }

    private sealed record TestContext(
        OrganizaClubDbContext Db,
        AuthService Service,
        FakeObjectStorage Storage,
        FakeTimeProvider TimeProvider,
        Guid UserId);

    private sealed class FakeUserContext(Guid userId) : IUserContext
    {
        public Guid UserId { get; } = userId;
        public SystemRole SystemRole => SystemRole.User;
        public Guid? SpaceId => null;
    }

    private sealed class FakeTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;

        public override DateTimeOffset GetUtcNow() => UtcNow;
    }

    private sealed class StubPasswordHasher : IPasswordHasher
    {
        public string Hash(string password) => password;
        public bool Verify(string password, string passwordHash) => password == passwordHash;
    }

    private sealed class StubTokenService : ITokenService
    {
        public DateTimeOffset AccessTokenExpiresAt => DateTimeOffset.Parse("2026-06-01T11:00:00+00:00");

        public string CreateAccessToken(AppUser user, IReadOnlyCollection<SpaceMember> memberships) => "access-token";
        public string CreateRefreshToken() => "refresh-token";
        public string HashRefreshToken(string refreshToken) => refreshToken;
    }

    private sealed class FakeObjectStorage : IObjectStorage
    {
        public Dictionary<string, StoredObject> Objects { get; } = [];

        public Task EnsureBucketExistsAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<StoredObject> GetAsync(string objectKey, CancellationToken cancellationToken)
        {
            if (!Objects.TryGetValue(objectKey, out var storedObject))
            {
                throw new NotFoundException("Arquivo não encontrado.");
            }

            return Task.FromResult(storedObject);
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
}
