using HomePit.Application.Auth;
using HomePit.Application.Common;
using HomePit.Application.Plans;
using HomePit.Application.Storage;
using HomePit.Domain.Households;
using HomePit.Infrastructure.Data;
using HomePit.Infrastructure.Images;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HomePit.Api.Tests;

public sealed class AuthServiceSuperAdminTests
{
    [Fact]
    public async Task Login_with_superadmin_credentials_creates_superadmin_user_and_lists_all_households()
    {
        await using var db = CreateDbContext();
        SeedHouseholds(db, "Casa A", "Casa B");

        var options = new SuperAdminOptions
        {
            Email = "superadmin@homepit.dev",
            Password = "super-secret",
            DisplayName = "Guardião"
        };
        var service = CreateService(db, options);

        var response = await service.LoginAsync(new LoginRequest(options.Email!, options.Password!), CancellationToken.None);
        var savedUser = await db.Users.SingleAsync();

        Assert.Equal(SystemRole.SuperAdmin, savedUser.SystemRole);
        Assert.Equal("Guardião", savedUser.DisplayName);
        Assert.Equal("superadmin@homepit.dev", savedUser.Email);
        Assert.Equal(SystemRole.SuperAdmin, response.User.SystemRole);
        Assert.Equal(2, response.Households.Count);
        Assert.All(response.Households, household => Assert.Equal(HouseholdRole.Member, household.Role));
    }

    [Fact]
    public async Task Login_reuses_same_superadmin_user_when_configuration_changes()
    {
        await using var db = CreateDbContext();
        SeedHouseholds(db, "Casa Única");

        var options = new SuperAdminOptions
        {
            Email = "superadmin@homepit.dev",
            Password = "super-secret",
            DisplayName = "Guardião"
        };
        var service = CreateService(db, options);

        await service.LoginAsync(new LoginRequest(options.Email!, options.Password!), CancellationToken.None);
        var firstUser = await db.Users.SingleAsync();
        var firstHash = firstUser.PasswordHash;

        options.Email = "suporte@homepit.dev";
        options.Password = "another-secret";
        options.DisplayName = "Suporte";

        var response = await service.LoginAsync(new LoginRequest(options.Email!, options.Password!), CancellationToken.None);
        var savedUsers = await db.Users.ToArrayAsync();
        var updatedUser = Assert.Single(savedUsers);

        Assert.Equal(firstUser.Id, updatedUser.Id);
        Assert.Equal("suporte@homepit.dev", updatedUser.Email);
        Assert.Equal("Suporte", updatedUser.DisplayName);
        Assert.NotEqual(firstHash, updatedUser.PasswordHash);
        Assert.Equal(SystemRole.SuperAdmin, response.User.SystemRole);
    }

    [Fact]
    public async Task First_registered_non_superadmin_user_remains_admin_even_after_superadmin_login()
    {
        await using var db = CreateDbContext();
        var options = new SuperAdminOptions
        {
            Email = "superadmin@homepit.dev",
            Password = "super-secret"
        };
        var service = CreateService(db, options);

        await service.LoginAsync(new LoginRequest(options.Email!, options.Password!), CancellationToken.None);
        var response = await service.RegisterAsync(
            new RegisterRequest("owner@homepit.dev", "owner-secret", "Owner", null),
            CancellationToken.None);

        Assert.Equal(SystemRole.Admin, response.User.SystemRole);
    }

    private static HomePitDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HomePitDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new HomePitDbContext(options);
    }

    private static AuthService CreateService(HomePitDbContext db, SuperAdminOptions options)
    {
        var storage = new FakeObjectStorage();
        var userContext = new TestUserContext();
        var commercialPlanService = new CommercialPlanService(db, userContext, TimeProvider.System);

        return new AuthService(
            db,
            new StubPasswordHasher(),
            new StubTokenService(),
            TimeProvider.System,
            userContext,
            storage,
            new ImageSharpImageUploadProcessor(),
            new HomePitDataPurgeService(db, storage),
            options,
            commercialPlanService);
    }

    private static void SeedHouseholds(HomePitDbContext db, params string[] names)
    {
        db.Households.AddRange(names.Select(name => new Household { Name = name }));
        db.SaveChanges();
    }

    private sealed class TestUserContext : IUserContext
    {
        public Guid UserId => Guid.Empty;
        public SystemRole SystemRole => SystemRole.User;
        public Guid? HouseholdId => null;
    }

    private sealed class StubPasswordHasher : IPasswordHasher
    {
        public string Hash(string password) => $"hash::{password}::{Guid.NewGuid():N}";

        public bool Verify(string password, string passwordHash) => passwordHash.Contains(password, StringComparison.Ordinal);
    }

    private sealed class StubTokenService : ITokenService
    {
        public DateTimeOffset AccessTokenExpiresAt => DateTimeOffset.Parse("2026-06-01T11:00:00+00:00");

        public string CreateAccessToken(AppUser user, IReadOnlyCollection<HouseholdMember> memberships) => $"token::{user.SystemRole}";

        public string CreateRefreshToken() => "refresh-token";

        public string HashRefreshToken(string refreshToken) => refreshToken;
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
