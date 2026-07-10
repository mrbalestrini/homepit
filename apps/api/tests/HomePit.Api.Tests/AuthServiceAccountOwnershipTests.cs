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

public sealed class AuthServiceAccountOwnershipTests
{
    [Fact]
    public async Task Delete_own_account_is_immediate_when_user_only_has_owner_role_in_household_created_by_another_user()
    {
        await using var db = CreateDbContext();
        var creator = new AppUser
        {
            Email = "creator@homepit.dev",
            PasswordHash = "hash",
            DisplayName = "Creator",
            SystemRole = SystemRole.User
        };
        var invitedOwner = new AppUser
        {
            Email = "invited-owner@homepit.dev",
            PasswordHash = "hash",
            DisplayName = "Invited Owner",
            SystemRole = SystemRole.User
        };
        var household = new Household
        {
            Name = "Casa compartilhada",
            CreatedByUser = creator
        };

        db.Users.AddRange(creator, invitedOwner);
        db.Households.Add(household);
        db.HouseholdMembers.AddRange(
            new HouseholdMember
            {
                Household = household,
                User = creator,
                Role = HouseholdRole.Owner
            },
            new HouseholdMember
            {
                Household = household,
                User = invitedOwner,
                Role = HouseholdRole.Owner
            });
        await db.SaveChangesAsync();

        var service = CreateService(db, invitedOwner.Id, SystemRole.User);

        var result = await service.DeleteOwnAccountAsync(CancellationToken.None);

        Assert.True(result.DeletedImmediately);
        Assert.Null(result.ScheduledDeletionAt);
        Assert.False(await db.Users.AnyAsync(user => user.Id == invitedOwner.Id));
        Assert.True(await db.Households.AnyAsync(item => item.Id == household.Id));
    }

    [Fact]
    public async Task Admin_user_list_counts_only_households_created_by_each_user()
    {
        await using var db = CreateDbContext();
        var creator = new AppUser
        {
            Email = "creator-admin@homepit.dev",
            PasswordHash = "hash",
            DisplayName = "Creator",
            SystemRole = SystemRole.User
        };
        var invitedOwner = new AppUser
        {
            Email = "invited-admin@homepit.dev",
            PasswordHash = "hash",
            DisplayName = "Invited Owner",
            SystemRole = SystemRole.User
        };
        var household = new Household
        {
            Name = "Casa visível",
            CreatedByUser = creator
        };

        db.Users.AddRange(creator, invitedOwner);
        db.Households.Add(household);
        db.HouseholdMembers.AddRange(
            new HouseholdMember
            {
                Household = household,
                User = creator,
                Role = HouseholdRole.Owner
            },
            new HouseholdMember
            {
                Household = household,
                User = invitedOwner,
                Role = HouseholdRole.Owner
            });
        await db.SaveChangesAsync();

        var service = CreateService(db, Guid.NewGuid(), SystemRole.SuperAdmin);

        var users = await service.ListAdminUsersAsync(CancellationToken.None);
        var creatorSummary = Assert.Single(users, user => user.Id == creator.Id);
        var invitedSummary = Assert.Single(users, user => user.Id == invitedOwner.Id);

        Assert.Equal(1, creatorSummary.OwnedHouseholdCount);
        Assert.Equal(0, invitedSummary.OwnedHouseholdCount);
    }

    private static HomePitDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HomePitDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new HomePitDbContext(options);
    }

    private static AuthService CreateService(HomePitDbContext db, Guid userId, SystemRole systemRole)
    {
        var storage = new FakeObjectStorage();
        var userContext = new TestUserContext(userId, systemRole);
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
            new SuperAdminOptions(),
            commercialPlanService);
    }

    private sealed class TestUserContext(Guid userId, SystemRole systemRole) : IUserContext
    {
        public Guid UserId { get; } = userId;
        public SystemRole SystemRole { get; } = systemRole;
        public Guid? HouseholdId => null;
    }

    private sealed class StubPasswordHasher : IPasswordHasher
    {
        public string Hash(string password) => $"hash::{password}";

        public bool Verify(string password, string passwordHash) => passwordHash.Contains(password, StringComparison.Ordinal);
    }

    private sealed class StubTokenService : ITokenService
    {
        public DateTimeOffset AccessTokenExpiresAt => DateTimeOffset.Parse("2026-06-01T11:00:00+00:00");

        public string CreateAccessToken(AppUser user, IReadOnlyCollection<HouseholdMember> memberships) => $"token::{user.Id:N}";

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
