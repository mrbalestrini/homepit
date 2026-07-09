using HomePit.Application.Common;
using HomePit.Application.Households;
using HomePit.Application.Storage;
using HomePit.Domain.Households;
using HomePit.Domain.Notifications;
using HomePit.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HomePit.Api.Tests;

public sealed class HouseholdServiceTests
{
    [Fact]
    public async Task Owner_can_update_member_role()
    {
        await using var context = CreateDbContext();
        var fixture = await SeedFixtureAsync(context);
        var service = CreateService(context, fixture.OwnerUserId, fixture.HouseholdId);

        var updated = await service.UpdateMemberAsync(
            fixture.MemberId,
            new UpdateHouseholdMemberRequest(HouseholdRole.Admin),
            CancellationToken.None);

        Assert.Equal(HouseholdRole.Admin, updated.Role);
        var member = await context.HouseholdMembers.SingleAsync(item => item.Id == fixture.MemberId);
        Assert.Equal(HouseholdRole.Admin, member.Role);
        Assert.True(member.IsActive);
    }

    [Fact]
    public async Task Owner_can_remove_member_without_deleting_history()
    {
        await using var context = CreateDbContext();
        var fixture = await SeedFixtureAsync(context);
        var service = CreateService(context, fixture.OwnerUserId, fixture.HouseholdId);

        await service.RemoveMemberAsync(fixture.MemberId, CancellationToken.None);

        var member = await context.HouseholdMembers.SingleAsync(item => item.Id == fixture.MemberId);
        Assert.False(member.IsActive);
        Assert.Equal(HouseholdRole.Member, member.Role);
    }

    [Fact]
    public async Task Non_owner_cannot_manage_members()
    {
        await using var context = CreateDbContext();
        var fixture = await SeedFixtureAsync(context);
        var service = CreateService(context, fixture.MemberUserId, fixture.HouseholdId);

        await Assert.ThrowsAsync<ForbiddenException>(
            () => service.UpdateMemberAsync(fixture.OwnerMemberId, new UpdateHouseholdMemberRequest(HouseholdRole.Admin), CancellationToken.None));

        await Assert.ThrowsAsync<ForbiddenException>(
            () => service.RemoveMemberAsync(fixture.OwnerMemberId, CancellationToken.None));
    }

    [Fact]
    public async Task Owner_cannot_remove_the_last_active_owner()
    {
        await using var context = CreateDbContext();
        var fixture = await SeedFixtureAsync(context, includeSecondOwner: false);
        var service = CreateService(context, fixture.OwnerUserId, fixture.HouseholdId);

        await Assert.ThrowsAsync<ValidationException>(
            () => service.RemoveMemberAsync(fixture.OwnerMemberId, CancellationToken.None));
    }

    [Fact]
    public async Task List_members_includes_profile_photo_metadata()
    {
        await using var context = CreateDbContext();
        var fixture = await SeedFixtureAsync(context);
        var service = CreateService(context, fixture.OwnerUserId, fixture.HouseholdId);

        var members = await service.ListMembersAsync(CancellationToken.None);
        var owner = Assert.Single(members, member => member.UserId == fixture.OwnerUserId);
        var member = Assert.Single(members, item => item.UserId == fixture.MemberUserId);

        Assert.False(owner.HasProfilePhoto);
        Assert.Null(owner.ProfilePhotoUpdatedAt);
        Assert.True(owner.IsCurrentUser);

        Assert.True(member.HasProfilePhoto);
        Assert.Equal(fixture.MemberProfilePhotoUpdatedAt, member.ProfilePhotoUpdatedAt);
        Assert.False(member.IsCurrentUser);
    }

    private static HomePitDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HomePitDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new HomePitDbContext(options);
    }

    private static HouseholdService CreateService(HomePitDbContext context, Guid userId, Guid householdId)
    {
        return new HouseholdService(
            context,
            new TestUserContext(userId, householdId),
            new HomePitDataPurgeService(context, new FakeObjectStorage()));
    }

    private static async Task<HouseholdFixture> SeedFixtureAsync(HomePitDbContext context, bool includeSecondOwner = true)
    {
        var ownerUser = new AppUser
        {
            Email = "owner@homepit.dev",
            PasswordHash = "hash",
            DisplayName = "Owner",
            SystemRole = SystemRole.User
        };
        var memberUser = new AppUser
        {
            Email = "member@homepit.dev",
            PasswordHash = "hash",
            DisplayName = "Member",
            ProfilePhotoObjectKey = "users/member/profile-photo",
            ProfilePhotoUpdatedAt = DateTimeOffset.Parse("2026-06-26T12:00:00Z"),
            SystemRole = SystemRole.User
        };
        var household = new Household
        {
            Name = "Casa Teste"
        };
        var ownerMember = new HouseholdMember
        {
            Household = household,
            User = ownerUser,
            Role = HouseholdRole.Owner
        };
        var member = new HouseholdMember
        {
            Household = household,
            User = memberUser,
            Role = HouseholdRole.Member
        };

        context.Users.AddRange(ownerUser, memberUser);
        context.Households.Add(household);
        context.HouseholdMembers.Add(ownerMember);
        context.HouseholdMembers.Add(member);

        if (includeSecondOwner)
        {
            var secondOwnerUser = new AppUser
            {
                Email = "co-owner@homepit.dev",
                PasswordHash = "hash",
                DisplayName = "Co Owner",
                SystemRole = SystemRole.User
            };
            var secondOwner = new HouseholdMember
            {
                Household = household,
                User = secondOwnerUser,
                Role = HouseholdRole.Owner
            };

            context.Users.Add(secondOwnerUser);
            context.HouseholdMembers.Add(secondOwner);
        }

        context.NotificationPreferences.Add(new NotificationPreference
        {
            Household = household,
            HouseholdMember = ownerMember
        });
        context.NotificationPreferences.Add(new NotificationPreference
        {
            Household = household,
            HouseholdMember = member
        });

        await context.SaveChangesAsync();

        return new HouseholdFixture(
            household.Id,
            ownerUser.Id,
            ownerMember.Id,
            memberUser.Id,
            member.Id,
            memberUser.ProfilePhotoUpdatedAt);
    }

    private sealed record HouseholdFixture(
        Guid HouseholdId,
        Guid OwnerUserId,
        Guid OwnerMemberId,
        Guid MemberUserId,
        Guid MemberId,
        DateTimeOffset? MemberProfilePhotoUpdatedAt);

    private sealed class TestUserContext(Guid userId, Guid? householdId) : IUserContext
    {
        public Guid UserId { get; } = userId;
        public SystemRole SystemRole => SystemRole.User;
        public Guid? HouseholdId { get; } = householdId;
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
