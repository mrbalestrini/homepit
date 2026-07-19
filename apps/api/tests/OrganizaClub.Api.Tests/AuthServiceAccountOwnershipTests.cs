using OrganizaClub.Application.Auth;
using OrganizaClub.Application.Common;
using OrganizaClub.Application.Plans;
using OrganizaClub.Application.Storage;
using OrganizaClub.Domain.Spaces;
using OrganizaClub.Infrastructure.Data;
using OrganizaClub.Infrastructure.Images;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace OrganizaClub.Api.Tests;

public sealed class AuthServiceAccountOwnershipTests
{
    [Fact]
    public async Task Delete_own_account_is_immediate_when_user_only_has_owner_role_in_space_created_by_another_user()
    {
        await using var db = CreateDbContext();
        var creator = new AppUser
        {
            Email = "creator@organiza.club",
            PasswordHash = "hash",
            DisplayName = "Creator",
            SystemRole = SystemRole.User
        };
        var invitedOwner = new AppUser
        {
            Email = "invited-owner@organiza.club",
            PasswordHash = "hash",
            DisplayName = "Invited Owner",
            SystemRole = SystemRole.User
        };
        var space = new Space
        {
            Name = "Espaço compartilhado",
            CreatedByUser = creator
        };

        db.Users.AddRange(creator, invitedOwner);
        db.Spaces.Add(space);
        db.SpaceMembers.AddRange(
            new SpaceMember
            {
                Space = space,
                User = creator,
                Role = SpaceRole.Owner
            },
            new SpaceMember
            {
                Space = space,
                User = invitedOwner,
                Role = SpaceRole.Owner
            });
        await db.SaveChangesAsync();

        var service = CreateService(db, invitedOwner.Id, SystemRole.User);

        var result = await service.DeleteOwnAccountAsync(CancellationToken.None);

        Assert.True(result.DeletedImmediately);
        Assert.Null(result.ScheduledDeletionAt);
        Assert.False(await db.Users.AnyAsync(user => user.Id == invitedOwner.Id));
        Assert.True(await db.Spaces.AnyAsync(item => item.Id == space.Id));
    }

    [Fact]
    public async Task Admin_user_list_counts_only_spaces_created_by_each_user()
    {
        await using var db = CreateDbContext();
        var creator = new AppUser
        {
            Email = "creator-admin@organiza.club",
            PasswordHash = "hash",
            DisplayName = "Creator",
            SystemRole = SystemRole.User
        };
        var invitedOwner = new AppUser
        {
            Email = "invited-admin@organiza.club",
            PasswordHash = "hash",
            DisplayName = "Invited Owner",
            SystemRole = SystemRole.User
        };
        var space = new Space
        {
            Name = "Espaço visível",
            CreatedByUser = creator
        };

        db.Users.AddRange(creator, invitedOwner);
        db.Spaces.Add(space);
        db.SpaceMembers.AddRange(
            new SpaceMember
            {
                Space = space,
                User = creator,
                Role = SpaceRole.Owner
            },
            new SpaceMember
            {
                Space = space,
                User = invitedOwner,
                Role = SpaceRole.Owner
            });
        await db.SaveChangesAsync();

        var service = CreateService(db, Guid.NewGuid(), SystemRole.SuperAdmin);

        var users = await service.ListAdminUsersAsync(CancellationToken.None);
        var creatorSummary = Assert.Single(users, user => user.Id == creator.Id);
        var invitedSummary = Assert.Single(users, user => user.Id == invitedOwner.Id);

        Assert.Equal(1, creatorSummary.OwnedSpaceCount);
        Assert.Equal(0, invitedSummary.OwnedSpaceCount);
    }

    private static OrganizaClubDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OrganizaClubDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new OrganizaClubDbContext(options);
    }

    private static AuthService CreateService(OrganizaClubDbContext db, Guid userId, SystemRole systemRole)
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
            new OrganizaClubDataPurgeService(db, storage),
            new SuperAdminOptions(),
            commercialPlanService);
    }

    private sealed class TestUserContext(Guid userId, SystemRole systemRole) : IUserContext
    {
        public Guid UserId { get; } = userId;
        public SystemRole SystemRole { get; } = systemRole;
        public Guid? SpaceId => null;
    }

    private sealed class StubPasswordHasher : IPasswordHasher
    {
        public string Hash(string password) => $"hash::{password}";

        public bool Verify(string password, string passwordHash) => passwordHash.Contains(password, StringComparison.Ordinal);
    }

    private sealed class StubTokenService : ITokenService
    {
        public DateTimeOffset AccessTokenExpiresAt => DateTimeOffset.Parse("2026-06-01T11:00:00+00:00");

        public string CreateAccessToken(AppUser user, IReadOnlyCollection<SpaceMember> memberships) => $"token::{user.Id:N}";

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
