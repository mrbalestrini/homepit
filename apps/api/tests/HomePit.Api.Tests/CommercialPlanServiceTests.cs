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

    private static async Task PutObjectAsync(InMemoryObjectStorage storage, string key, byte[] content, string contentType)
    {
        await using var stream = new MemoryStream(content, writable: false);
        await storage.PutAsync(new ObjectStoragePutRequest(key, stream, content.LongLength, contentType), CancellationToken.None);
    }

    private static CommercialPlanService CreateCommercialPlanService(HomePitDbContext context, Guid userId, SystemRole systemRole)
    {
        return new CommercialPlanService(context, new TestUserContext(userId, systemRole), TimeProvider.System);
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

    private sealed class TestUserContext(Guid userId, SystemRole systemRole) : IUserContext
    {
        public Guid UserId { get; } = userId;
        public SystemRole SystemRole { get; } = systemRole;
        public Guid? HouseholdId => null;
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
