using HomePit.Application.Common;
using HomePit.Application.Households;
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

    private static HomePitDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HomePitDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new HomePitDbContext(options);
    }

    private static HouseholdService CreateService(HomePitDbContext context, Guid userId, Guid householdId)
    {
        return new HouseholdService(context, new TestUserContext(userId, householdId));
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
            member.Id);
    }

    private sealed record HouseholdFixture(
        Guid HouseholdId,
        Guid OwnerUserId,
        Guid OwnerMemberId,
        Guid MemberUserId,
        Guid MemberId);

    private sealed class TestUserContext(Guid userId, Guid? householdId) : IUserContext
    {
        public Guid UserId { get; } = userId;
        public SystemRole SystemRole => SystemRole.User;
        public Guid? HouseholdId { get; } = householdId;
    }
}
