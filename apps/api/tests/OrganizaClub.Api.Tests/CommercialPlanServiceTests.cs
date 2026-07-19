using OrganizaClub.Application.Common;
using OrganizaClub.Application.Plans;
using OrganizaClub.Application.Storage;
using OrganizaClub.Domain.Spaces;
using OrganizaClub.Domain.Plans;
using OrganizaClub.Infrastructure.Data;
using OrganizaClub.Infrastructure.Images;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using Xunit;

namespace OrganizaClub.Api.Tests;

public sealed class CommercialPlanServiceTests
{
    [Fact]
    public async Task Resolve_effective_plan_falls_back_to_free_when_user_has_no_active_subscription()
    {
        await using var context = CreateDbContext();
        var user = await SeedUserAsync(context, "free-user@organiza.club");
        var service = CreateCommercialPlanService(context, user.Id, SystemRole.User);

        var plan = await service.ResolveEffectivePlanDefinitionAsync(user.Id, CancellationToken.None);

        Assert.Equal(PlanDefinitionCatalog.FreeSlug, plan.Slug);
    }

    [Fact]
    public async Task Public_plan_catalog_is_available_without_superadmin_context()
    {
        await using var context = CreateDbContext();
        var user = await SeedUserAsync(context, "public-plans@organiza.club");
        var service = CreateCommercialPlanService(context, user.Id, SystemRole.User);

        var plans = await service.ListPublicPlansAsync(CancellationToken.None);

        Assert.NotEmpty(plans);
        Assert.Contains(plans, plan => plan.Slug == PlanDefinitionCatalog.FreeSlug);
        Assert.Contains(plans, plan => plan.Slug == PlanDefinitionCatalog.GoldSlug && plan.IsPopular);
        Assert.Single(plans, plan => plan.IsPopular);
    }

    [Fact]
    public async Task Public_plan_catalog_keeps_the_current_plan_even_when_it_is_hidden()
    {
        await using var context = CreateDbContext();
        var user = await SeedUserAsync(context, "hidden-current@organiza.club");
        var adminService = CreateCommercialPlanService(context, Guid.NewGuid(), SystemRole.SuperAdmin);
        var userService = CreateCommercialPlanService(context, user.Id, SystemRole.User);

        await adminService.EnsurePlanCatalogAsync(CancellationToken.None);
        var standardPlan = await context.PlanDefinitions.SingleAsync(item => item.Slug == PlanDefinitionCatalog.StandardSlug);
        var bronzePlan = await context.PlanDefinitions.SingleAsync(item => item.Slug == PlanDefinitionCatalog.BronzeSlug);
        standardPlan.ShowInCatalog = false;
        bronzePlan.ShowInCatalog = false;

        await adminService.CreateSubscriptionAsync(
            new CreateUserSubscriptionRequest(
                user.Id,
                standardPlan.Id,
                BillingCycle.Monthly,
                DateTimeOffset.UtcNow.AddDays(-1),
                DateTimeOffset.UtcNow.AddDays(29),
                9.90m,
                "BRL",
                UserSubscriptionStatus.Active,
                null),
            CancellationToken.None);

        await context.SaveChangesAsync(CancellationToken.None);

        var plans = await userService.ListPublicPlansAsync(CancellationToken.None);

        Assert.Contains(plans, plan => plan.Slug == PlanDefinitionCatalog.StandardSlug && !plan.ShowInCatalog);
        Assert.DoesNotContain(plans, plan => plan.Slug == PlanDefinitionCatalog.BronzeSlug);
        Assert.Contains(plans, plan => plan.Slug == PlanDefinitionCatalog.FreeSlug);
    }

    [Fact]
    public async Task Free_plan_cannot_create_space()
    {
        await using var context = CreateDbContext();
        var user = await SeedUserAsync(context, "ownerless@organiza.club");
        var service = CreateCommercialPlanService(context, user.Id, SystemRole.User);

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            service.EnsureCanCreateSpaceAsync(user.Id, CancellationToken.None));

        Assert.Equal("O plano Free não permite criar espaços próprios.", exception.Message);
    }

    [Fact]
    public async Task Create_subscription_rejects_overlapping_ranges_for_same_user()
    {
        await using var context = CreateDbContext();
        var user = await SeedUserAsync(context, "subscriber@organiza.club");
        var service = CreateCommercialPlanService(context, Guid.NewGuid(), SystemRole.SuperAdmin);
        await service.EnsurePlanCatalogAsync(CancellationToken.None);
        var standardPlan = await context.PlanDefinitions.SingleAsync(item => item.Slug == PlanDefinitionCatalog.StandardSlug);

        await service.CreateSubscriptionAsync(
            new CreateUserSubscriptionRequest(
                user.Id,
                standardPlan.Id,
                BillingCycle.Monthly,
                DateTimeOffset.Parse("2026-07-01T00:00:00Z"),
                DateTimeOffset.Parse("2026-07-31T23:59:59Z"),
                9.90m,
                "BRL",
                UserSubscriptionStatus.Active,
                null),
            CancellationToken.None);

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.CreateSubscriptionAsync(
                new CreateUserSubscriptionRequest(
                    user.Id,
                    standardPlan.Id,
                    BillingCycle.Monthly,
                    DateTimeOffset.Parse("2026-07-15T00:00:00Z"),
                    DateTimeOffset.Parse("2026-08-14T23:59:59Z"),
                    9.90m,
                    "BRL",
                    UserSubscriptionStatus.Active,
                    "sobreposição"),
                CancellationToken.None));
    }

    [Fact]
    public async Task Superadmin_can_move_the_popular_flag_between_plans()
    {
        await using var context = CreateDbContext();
        var user = await SeedUserAsync(context, "popular@organiza.club");
        var service = CreateCommercialPlanService(context, user.Id, SystemRole.SuperAdmin);
        await service.EnsurePlanCatalogAsync(CancellationToken.None);

        var standardPlan = await context.PlanDefinitions.SingleAsync(item => item.Slug == PlanDefinitionCatalog.StandardSlug);

        var updated = await service.UpdatePlanAsync(
            standardPlan.Id,
            new UpdatePlanDefinitionRequest(
                standardPlan.MonthlyPrice,
                standardPlan.AnnualPrice,
                standardPlan.MaxOwnedSpaces,
                standardPlan.MaxCores,
                standardPlan.MaxProjects,
                standardPlan.MaxInvitedMembers,
                standardPlan.MaxOriginalImages,
                standardPlan.ShowInCatalog,
                true),
            CancellationToken.None);

        Assert.True(updated.IsPopular);

        var plans = await service.ListPublicPlansAsync(CancellationToken.None);
        Assert.True(plans.Single(item => item.Slug == PlanDefinitionCatalog.StandardSlug).IsPopular);
        Assert.False(plans.Single(item => item.Slug == PlanDefinitionCatalog.GoldSlug).IsPopular);
        Assert.Equal(1, plans.Count(item => item.IsPopular));
    }

    [Fact]
    public async Task Managed_image_quota_degrades_the_oldest_image_after_limit()
    {
        await using var context = CreateDbContext();
        var user = await SeedUserAsync(context, "images@organiza.club");
        var planService = CreateCommercialPlanService(context, user.Id, SystemRole.User);
        await planService.EnsurePlanCatalogAsync(CancellationToken.None);

        var freePlan = await context.PlanDefinitions.SingleAsync(item => item.Slug == PlanDefinitionCatalog.FreeSlug);
        freePlan.MaxOriginalImages = 1;
        await context.SaveChangesAsync();

        var storage = new InMemoryObjectStorage();
        var imageProcessor = new ImageSharpImageUploadProcessor();
        var quotaService = new ManagedImageQuotaService(context, storage, imageProcessor, planService, TimeProvider.System);

        var firstImage = TestImageFactory.CreatePng(1200, 900);
        await PutObjectAsync(storage, "activities/first/image", firstImage, "image/png");
        await quotaService.RegisterManagedImageAsync(
            user.Id,
            PlanImageAssetModule.Activity,
            Guid.NewGuid(),
            "activities/first/image",
            "image/png",
            CancellationToken.None);

        var secondImage = TestImageFactory.CreatePng(1100, 800);
        await PutObjectAsync(storage, "prompts/second/image", secondImage, "image/png");
        await quotaService.RegisterManagedImageAsync(
            user.Id,
            PlanImageAssetModule.Prompt,
            Guid.NewGuid(),
            "prompts/second/image",
            "image/png",
            CancellationToken.None);

        var assets = await context.UserPlanImageAssets
            .OrderBy(item => item.UploadedAt)
            .ToArrayAsync();

        Assert.Equal(2, assets.Length);
        Assert.True(assets[0].IsDegraded);
        Assert.False(assets[1].IsDegraded);
        Assert.Equal("image/webp", storage.Objects["activities/first/image"].ContentType);

        var degradedInfo = Image.Identify(storage.Objects["activities/first/image"].Content);
        Assert.NotNull(degradedInfo);
        Assert.True(degradedInfo.Width <= 300);
        Assert.True(degradedInfo.Height <= 300);
    }

    [Fact]
    public async Task Current_user_plan_counts_global_usage_for_the_current_user()
    {
        await using var context = CreateDbContext();
        var creator = await SeedUserAsync(context, "creator@organiza.club");
        var invited = await SeedUserAsync(context, "invited@organiza.club");
        var ownerMemberId = Guid.NewGuid();
        var invitedOwnerMemberId = Guid.NewGuid();
        var space = new Space
        {
            Name = "Espaço compartilhado",
            CreatedByUserId = creator.Id
        };

        context.Spaces.Add(space);
        context.SpaceMembers.AddRange(
            new SpaceMember
            {
                Id = ownerMemberId,
                Space = space,
                UserId = creator.Id,
                Role = SpaceRole.Owner
            },
            new SpaceMember
            {
                Id = invitedOwnerMemberId,
                Space = space,
                UserId = invited.Id,
                Role = SpaceRole.Owner
            });

        var service = CreateCommercialPlanService(context, invited.Id, SystemRole.User, space.Id);
        var invitedCore = new OrganizaClub.Domain.Projects.Core
        {
            Space = space,
            CreatedByMemberId = invitedOwnerMemberId,
            Name = "Núcleo do convidado"
        };
        context.Cores.Add(invitedCore);
        context.Projects.Add(new OrganizaClub.Domain.Projects.Project
        {
            SpaceId = space.Id,
            Core = invitedCore,
            CreatedByMemberId = invitedOwnerMemberId,
            Name = "Projeto do convidado"
        });

        await context.SaveChangesAsync();

        var summary = await service.GetCurrentUserPlanAsync(CancellationToken.None);

        Assert.Equal(PlanDefinitionCatalog.FreeSlug, summary.Plan.Slug);
        Assert.Equal(0, summary.Usage.OwnedSpaceCount);
        Assert.Equal(1, summary.Usage.CoreCount);
        Assert.Equal(1, summary.Usage.ProjectCount);
        Assert.Equal(0, summary.Usage.InvitedMemberCount);
    }

    [Fact]
    public async Task Create_core_limit_uses_the_creator_total_even_inside_another_users_space()
    {
        await using var context = CreateDbContext();
        var creator = await SeedUserAsync(context, "creator-limit@organiza.club");
        var invited = await SeedUserAsync(context, "invited-limit@organiza.club");
        var creatorMemberId = Guid.NewGuid();
        var invitedMemberId = Guid.NewGuid();
        var space = new Space
        {
            Name = "Espaço premium",
            CreatedByUserId = creator.Id
        };

        context.Spaces.Add(space);
        context.SpaceMembers.AddRange(
            new SpaceMember
            {
                Id = creatorMemberId,
                Space = space,
                UserId = creator.Id,
                Role = SpaceRole.Owner
            },
            new SpaceMember
            {
                Id = invitedMemberId,
                Space = space,
                UserId = invited.Id,
                Role = SpaceRole.Member
            });

        var service = CreateCommercialPlanService(context, invited.Id, SystemRole.User, space.Id);
        await service.EnsurePlanCatalogAsync(CancellationToken.None);
        context.Cores.AddRange(Enumerable.Range(1, 3).Select(index => new OrganizaClub.Domain.Projects.Core
        {
            Space = space,
            CreatedByMemberId = invitedMemberId,
            Name = $"Núcleo {index}"
        }));

        await context.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            service.EnsureCanCreateCoreAsync(invited.Id, space.Id, CancellationToken.None));

        Assert.Equal("O plano Free permite até 3 núcleo(s) no total.", exception.Message);
    }

    [Fact]
    public async Task Create_project_limit_uses_the_creator_total_even_inside_another_users_core()
    {
        await using var context = CreateDbContext();
        var creator = await SeedUserAsync(context, "creator-project@organiza.club");
        var invited = await SeedUserAsync(context, "invited-project@organiza.club");
        var creatorMemberId = Guid.NewGuid();
        var invitedMemberId = Guid.NewGuid();
        var space = new Space
        {
            Name = "Espaço premium",
            CreatedByUserId = creator.Id
        };
        var core = new OrganizaClub.Domain.Projects.Core
        {
            Space = space,
            CreatedByMemberId = creatorMemberId,
            Name = "Núcleo compartilhado"
        };

        context.Spaces.Add(space);
        context.Cores.Add(core);
        context.SpaceMembers.AddRange(
            new SpaceMember
            {
                Id = creatorMemberId,
                Space = space,
                UserId = creator.Id,
                Role = SpaceRole.Owner
            },
            new SpaceMember
            {
                Id = invitedMemberId,
                Space = space,
                UserId = invited.Id,
                Role = SpaceRole.Member
            });

        var service = CreateCommercialPlanService(context, invited.Id, SystemRole.User, space.Id);
        await service.EnsurePlanCatalogAsync(CancellationToken.None);
        context.Projects.AddRange(Enumerable.Range(1, 3).Select(index => new OrganizaClub.Domain.Projects.Project
        {
            SpaceId = space.Id,
            Core = core,
            CreatedByMemberId = invitedMemberId,
            Name = $"Projeto {index}"
        }));

        await context.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            service.EnsureCanCreateProjectAsync(invited.Id, core.Id, CancellationToken.None));

        Assert.Equal("O plano Free permite até 3 projeto(s) no total.", exception.Message);
    }

    [Fact]
    public async Task Invite_limit_counts_active_memberships_in_spaces_created_by_the_plan_owner()
    {
        await using var context = CreateDbContext();
        var creator = await SeedUserAsync(context, "owner-invites@organiza.club");
        var admin = await SeedUserAsync(context, "admin-invites@organiza.club");
        var guest = await SeedUserAsync(context, "guest-invites@organiza.club");
        var space = new Space
        {
            Name = "Espaço de convites",
            CreatedByUserId = creator.Id
        };

        context.Spaces.Add(space);
        context.SpaceMembers.AddRange(
            new SpaceMember
            {
                Space = space,
                UserId = creator.Id,
                Role = SpaceRole.Owner
            },
            new SpaceMember
            {
                Space = space,
                UserId = admin.Id,
                Role = SpaceRole.Admin
            });

        var service = CreateCommercialPlanService(context, admin.Id, SystemRole.User, space.Id);
        await service.EnsurePlanCatalogAsync(CancellationToken.None);
        var freePlan = await context.PlanDefinitions.SingleAsync(item => item.Slug == PlanDefinitionCatalog.FreeSlug);
        freePlan.MaxInvitedMembers = 2;
        await context.SaveChangesAsync();

        await service.EnsureCanInviteMemberToSpaceAsync(space.Id, guest.Id, CancellationToken.None);

        context.SpaceMembers.Add(new SpaceMember
        {
            Space = space,
            UserId = guest.Id,
            Role = SpaceRole.Member
        });
        await context.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            service.EnsureCanInviteMemberToSpaceAsync(space.Id, Guid.NewGuid(), CancellationToken.None));

        Assert.Equal("O plano Free permite até 2 membro(s) convidado(s) ativo(s) nos espaços próprios.", exception.Message);
    }

    [Fact]
    public async Task Invite_limit_ignores_inactive_memberships_and_null_limit_is_unlimited()
    {
        await using var context = CreateDbContext();
        var creator = await SeedUserAsync(context, "owner-limit@organiza.club");
        var guest = await SeedUserAsync(context, "guest-limit@organiza.club");
        var guestTwo = await SeedUserAsync(context, "guest-limit-two@organiza.club");
        var space = new Space
        {
            Name = "Espaço livre",
            CreatedByUserId = creator.Id
        };

        context.Spaces.Add(space);
        context.SpaceMembers.AddRange(
            new SpaceMember
            {
                Space = space,
                UserId = creator.Id,
                Role = SpaceRole.Owner
            },
            new SpaceMember
            {
                Space = space,
                UserId = guest.Id,
                Role = SpaceRole.Member,
                IsActive = false
            });

        var service = CreateCommercialPlanService(context, creator.Id, SystemRole.User, space.Id);
        await service.EnsurePlanCatalogAsync(CancellationToken.None);
        var freePlan = await context.PlanDefinitions.SingleAsync(item => item.Slug == PlanDefinitionCatalog.FreeSlug);
        freePlan.MaxInvitedMembers = 1;
        await context.SaveChangesAsync();

        await service.EnsureCanInviteMemberToSpaceAsync(space.Id, guestTwo.Id, CancellationToken.None);

        freePlan.MaxInvitedMembers = null;
        await context.SaveChangesAsync();

        await service.EnsureCanInviteMemberToSpaceAsync(space.Id, Guid.NewGuid(), CancellationToken.None);
    }

    private static async Task PutObjectAsync(InMemoryObjectStorage storage, string key, byte[] content, string contentType)
    {
        await using var stream = new MemoryStream(content, writable: false);
        await storage.PutAsync(new ObjectStoragePutRequest(key, stream, content.LongLength, contentType), CancellationToken.None);
    }

    private static CommercialPlanService CreateCommercialPlanService(
        OrganizaClubDbContext context,
        Guid userId,
        SystemRole systemRole,
        Guid? spaceId = null)
    {
        return new CommercialPlanService(context, new TestUserContext(userId, systemRole, spaceId), TimeProvider.System);
    }

    private static OrganizaClubDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OrganizaClubDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new OrganizaClubDbContext(options);
    }

    private static async Task<AppUser> SeedUserAsync(OrganizaClubDbContext context, string email)
    {
        var user = new AppUser
        {
            Email = email,
            PasswordHash = "hash",
            DisplayName = email,
            SystemRole = SystemRole.User
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    private sealed class TestUserContext(Guid userId, SystemRole systemRole, Guid? spaceId) : IUserContext
    {
        public Guid UserId { get; } = userId;
        public SystemRole SystemRole { get; } = systemRole;
        public Guid? SpaceId { get; } = spaceId;
    }

    private sealed class InMemoryObjectStorage : IObjectStorage
    {
        public Dictionary<string, StoredObject> Objects { get; } = [];

        public Task EnsureBucketExistsAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<StoredObject> GetAsync(string objectKey, CancellationToken cancellationToken) =>
            Task.FromResult(Objects.TryGetValue(objectKey, out var value)
                ? value
                : throw new NotFoundException("Arquivo não encontrado."));

        public async Task PutAsync(ObjectStoragePutRequest request, CancellationToken cancellationToken)
        {
            await using var buffer = new MemoryStream();
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
