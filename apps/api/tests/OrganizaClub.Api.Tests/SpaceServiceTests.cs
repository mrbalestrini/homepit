using OrganizaClub.Application.Common;
using OrganizaClub.Application.Spaces;
using OrganizaClub.Application.Plans;
using OrganizaClub.Application.Storage;
using OrganizaClub.Domain.Spaces;
using OrganizaClub.Domain.Notifications;
using OrganizaClub.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace OrganizaClub.Api.Tests;

public sealed class SpaceServiceTests
{
    [Fact]
    public async Task Owner_can_update_member_role()
    {
        await using var context = CreateDbContext();
        var fixture = await SeedFixtureAsync(context);
        var service = CreateService(context, fixture.OwnerUserId, fixture.SpaceId);

        var updated = await service.UpdateMemberAsync(
            fixture.MemberId,
            new UpdateSpaceMemberRequest(SpaceRole.Admin),
            CancellationToken.None);

        Assert.Equal(SpaceRole.Admin, updated.Role);
        var member = await context.SpaceMembers.SingleAsync(item => item.Id == fixture.MemberId);
        Assert.Equal(SpaceRole.Admin, member.Role);
        Assert.True(member.IsActive);
    }

    [Fact]
    public async Task Owner_can_remove_member_without_deleting_history()
    {
        await using var context = CreateDbContext();
        var fixture = await SeedFixtureAsync(context);
        var service = CreateService(context, fixture.OwnerUserId, fixture.SpaceId);

        await service.RemoveMemberAsync(fixture.MemberId, CancellationToken.None);

        var member = await context.SpaceMembers.SingleAsync(item => item.Id == fixture.MemberId);
        Assert.False(member.IsActive);
        Assert.Equal(SpaceRole.Member, member.Role);
    }

    [Fact]
    public async Task Non_owner_cannot_manage_members()
    {
        await using var context = CreateDbContext();
        var fixture = await SeedFixtureAsync(context);
        var service = CreateService(context, fixture.MemberUserId, fixture.SpaceId);

        await Assert.ThrowsAsync<ForbiddenException>(
            () => service.UpdateMemberAsync(fixture.OwnerMemberId, new UpdateSpaceMemberRequest(SpaceRole.Admin), CancellationToken.None));

        await Assert.ThrowsAsync<ForbiddenException>(
            () => service.RemoveMemberAsync(fixture.OwnerMemberId, CancellationToken.None));
    }

    [Fact]
    public async Task Owner_cannot_remove_the_last_active_owner()
    {
        await using var context = CreateDbContext();
        var fixture = await SeedFixtureAsync(context, includeSecondOwner: false);
        var service = CreateService(context, fixture.OwnerUserId, fixture.SpaceId);

        await Assert.ThrowsAsync<ValidationException>(
            () => service.RemoveMemberAsync(fixture.OwnerMemberId, CancellationToken.None));
    }

    [Fact]
    public async Task List_members_includes_profile_photo_metadata()
    {
        await using var context = CreateDbContext();
        var fixture = await SeedFixtureAsync(context);
        var service = CreateService(context, fixture.OwnerUserId, fixture.SpaceId);

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

    [Fact]
    public async Task Owner_can_create_pending_invitation_and_invitee_can_list_it()
    {
        await using var context = CreateDbContext();
        var fixture = await SeedFixtureAsync(context);
        var invitee = new AppUser
        {
            Email = "invitee@organiza.club",
            PasswordHash = "hash",
            DisplayName = "Invitee",
            SystemRole = SystemRole.User
        };
        context.Users.Add(invitee);
        await context.SaveChangesAsync();

        var ownerService = CreateService(context, fixture.OwnerUserId, fixture.SpaceId);
        var invitation = await ownerService.ShareAsync(
            new ShareSpaceRequest(invitee.Email, SpaceRole.Admin),
            CancellationToken.None);

        Assert.Equal(SpaceInvitationStatus.Pending, invitation.Status);
        Assert.False(invitation.IsIncoming);
        Assert.Equal(fixture.SpaceId, invitation.SpaceId);

        var storedInvitation = await context.SpaceInvitations.SingleAsync(item => item.Id == invitation.Id);
        Assert.Equal(invitee.Email, storedInvitation.InviteeEmail);
        Assert.Equal(SpaceInvitationStatus.Pending, storedInvitation.Status);

        var inviteeService = CreateService(context, invitee.Id, null);
        var invitations = await inviteeService.ListInvitationsAsync(CancellationToken.None);

        var listed = Assert.Single(invitations);
        Assert.True(listed.IsIncoming);
        Assert.Equal(invitation.Id, listed.Id);
        Assert.Equal(invitation.SpaceId, listed.SpaceId);
    }

    [Fact]
    public async Task Invitee_can_accept_pending_invitation_and_join_space()
    {
        await using var context = CreateDbContext();
        var fixture = await SeedFixtureAsync(context);
        var invitee = new AppUser
        {
            Email = "invitee-accept@organiza.club",
            PasswordHash = "hash",
            DisplayName = "Invitee",
            SystemRole = SystemRole.User
        };
        context.Users.Add(invitee);
        await context.SaveChangesAsync();

        var ownerService = CreateService(context, fixture.OwnerUserId, fixture.SpaceId);
        var invitation = await ownerService.ShareAsync(
            new ShareSpaceRequest(invitee.Email, SpaceRole.Member),
            CancellationToken.None);

        var inviteeService = CreateService(context, invitee.Id, null);
        var space = await inviteeService.AcceptInvitationAsync(invitation.Id, CancellationToken.None);

        Assert.Equal(fixture.SpaceId, space.Id);
        Assert.Equal(SpaceRole.Member, space.Role);

        var member = await context.SpaceMembers.SingleAsync(item => item.UserId == invitee.Id && item.SpaceId == fixture.SpaceId);
        Assert.True(member.IsActive);
        Assert.Equal(SpaceRole.Member, member.Role);

        var storedInvitation = await context.SpaceInvitations.SingleAsync(item => item.Id == invitation.Id);
        Assert.Equal(SpaceInvitationStatus.Accepted, storedInvitation.Status);
        Assert.NotNull(storedInvitation.RespondedAt);
    }

    [Fact]
    public async Task Invitee_can_decline_pending_invitation()
    {
        await using var context = CreateDbContext();
        var fixture = await SeedFixtureAsync(context);
        var invitee = new AppUser
        {
            Email = "invitee-decline@organiza.club",
            PasswordHash = "hash",
            DisplayName = "Invitee",
            SystemRole = SystemRole.User
        };
        context.Users.Add(invitee);
        await context.SaveChangesAsync();

        var ownerService = CreateService(context, fixture.OwnerUserId, fixture.SpaceId);
        var invitation = await ownerService.ShareAsync(
            new ShareSpaceRequest(invitee.Email, SpaceRole.Member),
            CancellationToken.None);

        var inviteeService = CreateService(context, invitee.Id, null);
        await inviteeService.DeclineInvitationAsync(invitation.Id, CancellationToken.None);

        var storedInvitation = await context.SpaceInvitations.SingleAsync(item => item.Id == invitation.Id);
        Assert.Equal(SpaceInvitationStatus.Declined, storedInvitation.Status);
        Assert.NotNull(storedInvitation.RespondedAt);
    }

    private static OrganizaClubDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OrganizaClubDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new OrganizaClubDbContext(options);
    }

    private static SpaceService CreateService(OrganizaClubDbContext context, Guid userId, Guid? spaceId)
    {
        var userContext = new TestUserContext(userId, spaceId);
        var storage = new FakeObjectStorage();
        var commercialPlanService = new CommercialPlanService(context, userContext, TimeProvider.System);

        return new SpaceService(
            context,
            userContext,
            new OrganizaClubDataPurgeService(context, storage),
            commercialPlanService,
            TimeProvider.System);
    }

    private static async Task<SpaceFixture> SeedFixtureAsync(OrganizaClubDbContext context, bool includeSecondOwner = true)
    {
        var ownerUser = new AppUser
        {
            Email = "owner@organiza.club",
            PasswordHash = "hash",
            DisplayName = "Owner",
            SystemRole = SystemRole.User
        };
        var memberUser = new AppUser
        {
            Email = "member@organiza.club",
            PasswordHash = "hash",
            DisplayName = "Member",
            ProfilePhotoObjectKey = "users/member/profile-photo",
            ProfilePhotoUpdatedAt = DateTimeOffset.Parse("2026-06-26T12:00:00Z"),
            SystemRole = SystemRole.User
        };
        var space = new Space
        {
            Name = "Espaço Teste",
            CreatedByUserId = ownerUser.Id
        };
        var ownerMember = new SpaceMember
        {
            Space = space,
            User = ownerUser,
            Role = SpaceRole.Owner
        };
        var member = new SpaceMember
        {
            Space = space,
            User = memberUser,
            Role = SpaceRole.Member
        };

        context.Users.AddRange(ownerUser, memberUser);
        context.Spaces.Add(space);
        context.SpaceMembers.Add(ownerMember);
        context.SpaceMembers.Add(member);

        if (includeSecondOwner)
        {
            var secondOwnerUser = new AppUser
            {
                Email = "co-owner@organiza.club",
                PasswordHash = "hash",
                DisplayName = "Co Owner",
                SystemRole = SystemRole.User
            };
            var secondOwner = new SpaceMember
            {
                Space = space,
                User = secondOwnerUser,
                Role = SpaceRole.Owner
            };

            context.Users.Add(secondOwnerUser);
            context.SpaceMembers.Add(secondOwner);
        }

        context.NotificationPreferences.Add(new NotificationPreference
        {
            Space = space,
            SpaceMember = ownerMember
        });
        context.NotificationPreferences.Add(new NotificationPreference
        {
            Space = space,
            SpaceMember = member
        });

        await context.SaveChangesAsync();

        return new SpaceFixture(
            space.Id,
            ownerUser.Id,
            ownerMember.Id,
            memberUser.Id,
            member.Id,
            memberUser.ProfilePhotoUpdatedAt);
    }

    private sealed record SpaceFixture(
        Guid SpaceId,
        Guid OwnerUserId,
        Guid OwnerMemberId,
        Guid MemberUserId,
        Guid MemberId,
        DateTimeOffset? MemberProfilePhotoUpdatedAt);

    private sealed class TestUserContext(Guid userId, Guid? spaceId) : IUserContext
    {
        public Guid UserId { get; } = userId;
        public SystemRole SystemRole => SystemRole.User;
        public Guid? SpaceId { get; } = spaceId;
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
