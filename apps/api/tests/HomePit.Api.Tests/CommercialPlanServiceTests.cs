using HomePit.Application.Common;
using HomePit.Application.Plans;
using HomePit.Application.Storage;
using HomePit.Domain.Households;
using HomePit.Domain.Plans;
using HomePit.Infrastructure.Data;
using HomePit.Infrastructure.Images;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using Xunit;

namespace HomePit.Api.Tests;

public sealed class CommercialPlanServiceTests
{
    [Fact]
    public async Task Resolve_effective_plan_falls_back_to_free_when_user_has_no_active_subscription()
    {
        await using var context = CreateDbContext();
        var user = await SeedUserAsync(context, "free-user@homepit.dev");
        var service = CreateCommercialPlanService(context, user.Id, SystemRole.User);

        var plan = await service.ResolveEffectivePlanDefinitionAsync(user.Id, CancellationToken.None);

        Assert.Equal(PlanDefinitionCatalog.FreeSlug, plan.Slug);
    }

    [Fact]
    public async Task Public_plan_catalog_is_available_without_superadmin_context()
    {
        await using var context = CreateDbContext();
        var user = await SeedUserAsync(context, "public-plans@homepit.dev");
        var service = CreateCommercialPlanService(context, user.Id, SystemRole.User);

        var plans = await service.ListPublicPlansAsync(CancellationToken.None);

        Assert.NotEmpty(plans);
        Assert.Contains(plans, plan => plan.Slug == PlanDefinitionCatalog.FreeSlug);
        Assert.Contains(plans, plan => plan.Slug == PlanDefinitionCatalog.GoldSlug && plan.IsPopular);
        Assert.Single(plans, plan => plan.IsPopular);
    }

    [Fact]
    public async Task Free_plan_cannot_create_household()
    {
        await using var context = CreateDbContext();
        var user = await SeedUserAsync(context, "ownerless@homepit.dev");
        var service = CreateCommercialPlanService(context, user.Id, SystemRole.User);

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            service.EnsureCanCreateHouseholdAsync(user.Id, CancellationToken.None));

        Assert.Equal("O plano Free não permite criar casas próprias.", exception.Message);
    }

    [Fact]
    public async Task Create_subscription_rejects_overlapping_ranges_for_same_user()
    {
        await using var context = CreateDbContext();
        var user = await SeedUserAsync(context, "subscriber@homepit.dev");
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
        var user = await SeedUserAsync(context, "popular@homepit.dev");
        var service = CreateCommercialPlanService(context, user.Id, SystemRole.SuperAdmin);
        await service.EnsurePlanCatalogAsync(CancellationToken.None);

        var standardPlan = await context.PlanDefinitions.SingleAsync(item => item.Slug == PlanDefinitionCatalog.StandardSlug);

        var updated = await service.UpdatePlanAsync(
            standardPlan.Id,
            new UpdatePlanDefinitionRequest(
                standardPlan.MonthlyPrice,
                standardPlan.AnnualPrice,
                standardPlan.MaxOwnedHouseholds,
                standardPlan.MaxUniverses,
                standardPlan.MaxProjects,
                standardPlan.MaxInvitedMembers,
                standardPlan.MaxOriginalImages,
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
        var user = await SeedUserAsync(context, "images@homepit.dev");
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
        var creator = await SeedUserAsync(context, "creator@homepit.dev");
        var invited = await SeedUserAsync(context, "invited@homepit.dev");
        var ownerMemberId = Guid.NewGuid();
        var invitedOwnerMemberId = Guid.NewGuid();
        var household = new Household
        {
            Name = "Casa compartilhada",
            CreatedByUserId = creator.Id
        };

        context.Households.Add(household);
        context.HouseholdMembers.AddRange(
            new HouseholdMember
            {
                Id = ownerMemberId,
                Household = household,
                UserId = creator.Id,
                Role = HouseholdRole.Owner
            },
            new HouseholdMember
            {
                Id = invitedOwnerMemberId,
                Household = household,
                UserId = invited.Id,
                Role = HouseholdRole.Owner
            });

        var service = CreateCommercialPlanService(context, invited.Id, SystemRole.User, household.Id);
        var invitedUniverse = new HomePit.Domain.Projects.Universe
        {
            Household = household,
            CreatedByMemberId = invitedOwnerMemberId,
            Name = "Universo do convidado"
        };
        context.Universes.Add(invitedUniverse);
        context.Projects.Add(new HomePit.Domain.Projects.Project
        {
            HouseholdId = household.Id,
            Universe = invitedUniverse,
            CreatedByMemberId = invitedOwnerMemberId,
            Name = "Projeto do convidado"
        });

        await context.SaveChangesAsync();

        var summary = await service.GetCurrentUserPlanAsync(CancellationToken.None);

        Assert.Equal(PlanDefinitionCatalog.FreeSlug, summary.Plan.Slug);
        Assert.Equal(0, summary.Usage.OwnedHouseholdCount);
        Assert.Equal(1, summary.Usage.UniverseCount);
        Assert.Equal(1, summary.Usage.ProjectCount);
        Assert.Equal(0, summary.Usage.InvitedMemberCount);
    }

    [Fact]
    public async Task Create_universe_limit_uses_the_creator_total_even_inside_another_users_household()
    {
        await using var context = CreateDbContext();
        var creator = await SeedUserAsync(context, "creator-limit@homepit.dev");
        var invited = await SeedUserAsync(context, "invited-limit@homepit.dev");
        var creatorMemberId = Guid.NewGuid();
        var invitedMemberId = Guid.NewGuid();
        var household = new Household
        {
            Name = "Casa premium",
            CreatedByUserId = creator.Id
        };

        context.Households.Add(household);
        context.HouseholdMembers.AddRange(
            new HouseholdMember
            {
                Id = creatorMemberId,
                Household = household,
                UserId = creator.Id,
                Role = HouseholdRole.Owner
            },
            new HouseholdMember
            {
                Id = invitedMemberId,
                Household = household,
                UserId = invited.Id,
                Role = HouseholdRole.Member
            });

        var service = CreateCommercialPlanService(context, invited.Id, SystemRole.User, household.Id);
        await service.EnsurePlanCatalogAsync(CancellationToken.None);
        context.Universes.AddRange(Enumerable.Range(1, 3).Select(index => new HomePit.Domain.Projects.Universe
        {
            Household = household,
            CreatedByMemberId = invitedMemberId,
            Name = $"Universo {index}"
        }));

        await context.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            service.EnsureCanCreateUniverseAsync(invited.Id, household.Id, CancellationToken.None));

        Assert.Equal("O plano Free permite até 3 universo(s) no total.", exception.Message);
    }

    [Fact]
    public async Task Create_project_limit_uses_the_creator_total_even_inside_another_users_universe()
    {
        await using var context = CreateDbContext();
        var creator = await SeedUserAsync(context, "creator-project@homepit.dev");
        var invited = await SeedUserAsync(context, "invited-project@homepit.dev");
        var creatorMemberId = Guid.NewGuid();
        var invitedMemberId = Guid.NewGuid();
        var household = new Household
        {
            Name = "Casa premium",
            CreatedByUserId = creator.Id
        };
        var universe = new HomePit.Domain.Projects.Universe
        {
            Household = household,
            CreatedByMemberId = creatorMemberId,
            Name = "Universo compartilhado"
        };

        context.Households.Add(household);
        context.Universes.Add(universe);
        context.HouseholdMembers.AddRange(
            new HouseholdMember
            {
                Id = creatorMemberId,
                Household = household,
                UserId = creator.Id,
                Role = HouseholdRole.Owner
            },
            new HouseholdMember
            {
                Id = invitedMemberId,
                Household = household,
                UserId = invited.Id,
                Role = HouseholdRole.Member
            });

        var service = CreateCommercialPlanService(context, invited.Id, SystemRole.User, household.Id);
        await service.EnsurePlanCatalogAsync(CancellationToken.None);
        context.Projects.AddRange(Enumerable.Range(1, 3).Select(index => new HomePit.Domain.Projects.Project
        {
            HouseholdId = household.Id,
            Universe = universe,
            CreatedByMemberId = invitedMemberId,
            Name = $"Projeto {index}"
        }));

        await context.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            service.EnsureCanCreateProjectAsync(invited.Id, universe.Id, CancellationToken.None));

        Assert.Equal("O plano Free permite até 3 projeto(s) no total.", exception.Message);
    }

    [Fact]
    public async Task Invite_limit_counts_active_memberships_in_households_created_by_the_plan_owner()
    {
        await using var context = CreateDbContext();
        var creator = await SeedUserAsync(context, "owner-invites@homepit.dev");
        var admin = await SeedUserAsync(context, "admin-invites@homepit.dev");
        var guest = await SeedUserAsync(context, "guest-invites@homepit.dev");
        var household = new Household
        {
            Name = "Casa de convites",
            CreatedByUserId = creator.Id
        };

        context.Households.Add(household);
        context.HouseholdMembers.AddRange(
            new HouseholdMember
            {
                Household = household,
                UserId = creator.Id,
                Role = HouseholdRole.Owner
            },
            new HouseholdMember
            {
                Household = household,
                UserId = admin.Id,
                Role = HouseholdRole.Admin
            });

        var service = CreateCommercialPlanService(context, admin.Id, SystemRole.User, household.Id);
        await service.EnsurePlanCatalogAsync(CancellationToken.None);
        var freePlan = await context.PlanDefinitions.SingleAsync(item => item.Slug == PlanDefinitionCatalog.FreeSlug);
        freePlan.MaxInvitedMembers = 2;
        await context.SaveChangesAsync();

        await service.EnsureCanInviteMemberToHouseholdAsync(household.Id, guest.Id, CancellationToken.None);

        context.HouseholdMembers.Add(new HouseholdMember
        {
            Household = household,
            UserId = guest.Id,
            Role = HouseholdRole.Member
        });
        await context.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            service.EnsureCanInviteMemberToHouseholdAsync(household.Id, Guid.NewGuid(), CancellationToken.None));

        Assert.Equal("O plano Free permite até 2 membro(s) convidado(s) ativo(s) nas casas próprias.", exception.Message);
    }

    [Fact]
    public async Task Invite_limit_ignores_inactive_memberships_and_null_limit_is_unlimited()
    {
        await using var context = CreateDbContext();
        var creator = await SeedUserAsync(context, "owner-limit@homepit.dev");
        var guest = await SeedUserAsync(context, "guest-limit@homepit.dev");
        var guestTwo = await SeedUserAsync(context, "guest-limit-two@homepit.dev");
        var household = new Household
        {
            Name = "Casa livre",
            CreatedByUserId = creator.Id
        };

        context.Households.Add(household);
        context.HouseholdMembers.AddRange(
            new HouseholdMember
            {
                Household = household,
                UserId = creator.Id,
                Role = HouseholdRole.Owner
            },
            new HouseholdMember
            {
                Household = household,
                UserId = guest.Id,
                Role = HouseholdRole.Member,
                IsActive = false
            });

        var service = CreateCommercialPlanService(context, creator.Id, SystemRole.User, household.Id);
        await service.EnsurePlanCatalogAsync(CancellationToken.None);
        var freePlan = await context.PlanDefinitions.SingleAsync(item => item.Slug == PlanDefinitionCatalog.FreeSlug);
        freePlan.MaxInvitedMembers = 1;
        await context.SaveChangesAsync();

        await service.EnsureCanInviteMemberToHouseholdAsync(household.Id, guestTwo.Id, CancellationToken.None);

        freePlan.MaxInvitedMembers = null;
        await context.SaveChangesAsync();

        await service.EnsureCanInviteMemberToHouseholdAsync(household.Id, Guid.NewGuid(), CancellationToken.None);
    }

    private static async Task PutObjectAsync(InMemoryObjectStorage storage, string key, byte[] content, string contentType)
    {
        await using var stream = new MemoryStream(content, writable: false);
        await storage.PutAsync(new ObjectStoragePutRequest(key, stream, content.LongLength, contentType), CancellationToken.None);
    }

    private static CommercialPlanService CreateCommercialPlanService(
        HomePitDbContext context,
        Guid userId,
        SystemRole systemRole,
        Guid? householdId = null)
    {
        return new CommercialPlanService(context, new TestUserContext(userId, systemRole, householdId), TimeProvider.System);
    }

    private static HomePitDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HomePitDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new HomePitDbContext(options);
    }

    private static async Task<AppUser> SeedUserAsync(HomePitDbContext context, string email)
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

    private sealed class TestUserContext(Guid userId, SystemRole systemRole, Guid? householdId) : IUserContext
    {
        public Guid UserId { get; } = userId;
        public SystemRole SystemRole { get; } = systemRole;
        public Guid? HouseholdId { get; } = householdId;
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
