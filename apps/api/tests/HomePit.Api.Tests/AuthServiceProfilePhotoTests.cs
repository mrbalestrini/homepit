using HomePit.Application.Auth;
using HomePit.Application.Common;
using HomePit.Application.Storage;
using HomePit.Domain.Households;
using HomePit.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HomePit.Api.Tests;

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
        await using var stream = new MemoryStream([1, 2, 3]);

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            context.Service.UploadProfilePhotoAsync(stream, stream.Length, "image/gif", CancellationToken.None));

        Assert.Equal("A foto de perfil deve estar em JPG, PNG ou WEBP.", exception.Message);
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
        await using var stream = new MemoryStream([1, 2, 3, 4]);

        var result = await context.Service.UploadProfilePhotoAsync(stream, stream.Length, "image/png", CancellationToken.None);
        var savedUser = await context.Db.Users.SingleAsync();

        Assert.True(result.HasProfilePhoto);
        Assert.NotNull(result.ProfilePhotoUpdatedAt);
        Assert.Equal(ObjectStorageKeys.UserProfilePhoto(savedUser.Id), savedUser.ProfilePhotoObjectKey);
        Assert.NotNull(savedUser.ProfilePhotoUpdatedAt);
        Assert.Single(context.Storage.Objects);
    }

    [Fact]
    public async Task Replacing_profile_photo_reuses_same_object_key()
    {
        var context = await CreateContextAsync();
        await using var firstStream = new MemoryStream([1, 2, 3]);
        await using var secondStream = new MemoryStream([9, 8, 7, 6]);

        var first = await context.Service.UploadProfilePhotoAsync(firstStream, firstStream.Length, "image/png", CancellationToken.None);
        context.TimeProvider.UtcNow = context.TimeProvider.UtcNow.AddMinutes(5);
        var second = await context.Service.UploadProfilePhotoAsync(secondStream, secondStream.Length, "image/png", CancellationToken.None);

        Assert.True(second.HasProfilePhoto);
        Assert.NotNull(first.ProfilePhotoUpdatedAt);
        Assert.NotNull(second.ProfilePhotoUpdatedAt);
        Assert.NotEqual(first.ProfilePhotoUpdatedAt, second.ProfilePhotoUpdatedAt);
        Assert.Single(context.Storage.Objects);
        Assert.Equal(ObjectStorageKeys.UserProfilePhoto(context.UserId), context.Storage.Objects.Single().Key);
        Assert.Equal([9, 8, 7, 6], context.Storage.Objects.Single().Value.Content);
    }

    private static async Task<TestContext> CreateContextAsync()
    {
        var db = new HomePitDbContext(
            new DbContextOptionsBuilder<HomePitDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options);

        var user = new AppUser
        {
            Email = "ana@homepit.dev",
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

        var service = new AuthService(
            db,
            new StubPasswordHasher(),
            new StubTokenService(),
            timeProvider,
            userContext,
            storage);

        return new TestContext(db, service, storage, timeProvider, user.Id);
    }

    private sealed record TestContext(
        HomePitDbContext Db,
        AuthService Service,
        FakeObjectStorage Storage,
        FakeTimeProvider TimeProvider,
        Guid UserId);

    private sealed class FakeUserContext(Guid userId) : IUserContext
    {
        public Guid UserId { get; } = userId;
        public Guid? HouseholdId => null;
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

        public string CreateAccessToken(AppUser user, IReadOnlyCollection<HouseholdMember> memberships) => "access-token";
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
    }
}
