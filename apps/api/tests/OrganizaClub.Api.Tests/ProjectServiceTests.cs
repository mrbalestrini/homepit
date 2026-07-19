using OrganizaClub.Application.Common;
using OrganizaClub.Application.Plans;
using OrganizaClub.Application.Projects;
using OrganizaClub.Application.Storage;
using OrganizaClub.Domain.Spaces;
using OrganizaClub.Domain.Plans;
using OrganizaClub.Domain.Projects;
using OrganizaClub.Infrastructure.Data;
using OrganizaClub.Infrastructure.Images;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace OrganizaClub.Api.Tests;

public sealed class ProjectServiceTests
{
    [Fact]
    public async Task List_projects_counts_only_open_activities()
    {
        await using var context = CreateDbContext();
        var fixture = await SeedFixtureAsync(context);
        var service = CreateService(context, fixture.OwnerUserId, fixture.SpaceId);

        var projects = await service.ListProjectsAsync(null, CancellationToken.None);

        var project = Assert.Single(projects);
        Assert.Equal(2, project.ActivityCount);
    }

    [Fact]
    public async Task List_cores_keeps_existing_items_editable_when_the_total_limit_is_exceeded()
    {
        await using var context = CreateDbContext();
        var fixture = await SeedFixtureAsync(context);
        await AddCoresAsync(context, fixture, 4);
        var service = CreateService(context, fixture.OwnerUserId, fixture.SpaceId);

        var cores = await service.ListCoresAsync(CancellationToken.None);

        Assert.Equal(5, cores.Count);
        Assert.All(cores, item => Assert.True(item.CanEdit));
        Assert.All(cores, item => Assert.True(item.CanDelete));
        Assert.Equal(2, cores.Count(item => item.IsOutOfPlan));
    }

    [Fact]
    public async Task List_projects_keeps_existing_items_editable_when_the_total_limit_is_exceeded()
    {
        await using var context = CreateDbContext();
        var fixture = await SeedFixtureAsync(context);
        await AddProjectsAsync(context, fixture, 4);
        var service = CreateService(context, fixture.OwnerUserId, fixture.SpaceId);

        var projects = await service.ListProjectsAsync(fixture.CoreId, CancellationToken.None);

        Assert.Equal(5, projects.Count);
        Assert.All(projects, item => Assert.True(item.CanEdit));
        Assert.All(projects, item => Assert.True(item.CanDelete));
        Assert.Equal(2, projects.Count(item => item.IsOutOfPlan));
    }

    [Fact]
    public async Task Update_project_returns_only_open_activity_count()
    {
        await using var context = CreateDbContext();
        var fixture = await SeedFixtureAsync(context);
        var service = CreateService(context, fixture.OwnerUserId, fixture.SpaceId);

        var updated = await service.UpdateProjectAsync(
            fixture.ProjectId,
            new UpdateProjectRequest(fixture.CoreId, "Projeto atualizado"),
            CancellationToken.None);

        Assert.Equal(2, updated.ActivityCount);
    }

    [Fact]
    public async Task Activity_completion_date_follows_status_transitions()
    {
        await using var context = CreateDbContext();
        var fixture = await SeedFixtureAsync(context);
        var firstCompletion = new DateTimeOffset(2026, 7, 14, 12, 0, 0, TimeSpan.Zero);
        var service = CreateService(context, fixture.OwnerUserId, fixture.SpaceId, timeProvider: new FixedTimeProvider(firstCompletion));
        var activity = await context.Activities.SingleAsync(item => item.Status == ActivityStatus.Concluido);

        var completed = await service.UpdateActivityStatusAsync(
            activity.Id,
            new UpdateActivityStatusRequest(ActivityStatus.Concluido),
            CancellationToken.None);

        Assert.Equal(firstCompletion, completed.CompletedAt);

        var reopened = await service.UpdateActivityStatusAsync(
            activity.Id,
            new UpdateActivityStatusRequest(ActivityStatus.EmAndamento),
            CancellationToken.None);

        Assert.Null(reopened.CompletedAt);

        var completedAgain = await service.UpdateActivityStatusAsync(
            activity.Id,
            new UpdateActivityStatusRequest(ActivityStatus.Concluido),
            CancellationToken.None);

        Assert.Equal(firstCompletion, completedAgain.CompletedAt);
    }

    [Fact]
    public async Task Uploading_activity_image_sets_metadata_and_storage_key()
    {
        await using var context = CreateDbContext();
        var fixture = await SeedFixtureAsync(context);
        var storage = new InMemoryObjectStorage();
        var service = CreateService(context, fixture.OwnerUserId, fixture.SpaceId, storage);

        var activity = new Activity
        {
            SpaceId = fixture.SpaceId,
            ProjectId = fixture.ProjectId,
            CreatedByMemberId = fixture.OwnerMemberId,
            Title = "Atividade com imagem"
        };
        context.Activities.Add(activity);
        await context.SaveChangesAsync();

        var png = TestImageFactory.CreatePng(400, 300);
        await using var stream = new MemoryStream(png);
        var result = await service.UploadActivityImageAsync(activity.Id, stream, stream.Length, "image/png", CancellationToken.None);

        Assert.True(result.HasImage);
        Assert.NotNull(result.ImageUpdatedAt);
        Assert.Equal(ObjectStorageKeys.ActivityImage(activity.Id), activity.ImageObjectKey);
        Assert.Single(storage.Objects);
        Assert.Equal("image/webp", storage.Objects.Single().Value.ContentType);
    }

    [Fact]
    public async Task Uploading_core_image_sets_metadata_and_storage_key()
    {
        await using var context = CreateDbContext();
        var fixture = await SeedFixtureAsync(context);
        var storage = new InMemoryObjectStorage();
        var service = CreateService(context, fixture.OwnerUserId, fixture.SpaceId, storage);

        var jpeg = TestImageFactory.CreateJpeg(640, 360);
        await using var stream = new MemoryStream(jpeg);
        var result = await service.UploadCoreImageAsync(fixture.CoreId, stream, stream.Length, "image/jpeg", CancellationToken.None);

        Assert.True(result.HasImage);
        Assert.NotNull(result.ImageUpdatedAt);
        Assert.Equal(ObjectStorageKeys.CoreImage(fixture.CoreId), storage.Objects.Single().Key);
        Assert.Equal("image/webp", storage.Objects.Single().Value.ContentType);
    }

    [Fact]
    public async Task Deleting_activity_image_clears_storage_and_metadata()
    {
        await using var context = CreateDbContext();
        var fixture = await SeedFixtureAsync(context);
        var storage = new InMemoryObjectStorage();
        var service = CreateService(context, fixture.OwnerUserId, fixture.SpaceId, storage);

        var activity = new Activity
        {
            SpaceId = fixture.SpaceId,
            ProjectId = fixture.ProjectId,
            CreatedByMemberId = fixture.OwnerMemberId,
            Title = "Atividade com imagem"
        };
        context.Activities.Add(activity);
        await context.SaveChangesAsync();

        var png = TestImageFactory.CreatePng(400, 300);
        await using var stream = new MemoryStream(png);
        await service.UploadActivityImageAsync(activity.Id, stream, stream.Length, "image/png", CancellationToken.None);

        var deleted = await service.DeleteActivityImageAsync(activity.Id, CancellationToken.None);

        Assert.False(deleted.HasImage);
        Assert.Null(deleted.ImageUpdatedAt);
        Assert.Empty(storage.Objects);
    }

    [Fact]
    public async Task Deleting_project_removes_activity_images_from_storage()
    {
        await using var context = CreateDbContext();
        var fixture = await SeedFixtureAsync(context);
        var storage = new InMemoryObjectStorage();
        var service = CreateService(context, fixture.OwnerUserId, fixture.SpaceId, storage);

        var activity = new Activity
        {
            SpaceId = fixture.SpaceId,
            ProjectId = fixture.ProjectId,
            CreatedByMemberId = fixture.OwnerMemberId,
            Title = "Atividade do projeto"
        };
        context.Activities.Add(activity);
        await context.SaveChangesAsync();

        var png = TestImageFactory.CreatePng(400, 300);
        await using var stream = new MemoryStream(png);
        await service.UploadActivityImageAsync(activity.Id, stream, stream.Length, "image/png", CancellationToken.None);

        await service.DeleteProjectAsync(fixture.ProjectId, CancellationToken.None);

        Assert.Empty(storage.Objects);
    }

    [Fact]
    public async Task Deleting_core_removes_activity_images_from_storage()
    {
        await using var context = CreateDbContext();
        var fixture = await SeedFixtureAsync(context);
        var storage = new InMemoryObjectStorage();
        var service = CreateService(context, fixture.OwnerUserId, fixture.SpaceId, storage);

        var activity = new Activity
        {
            SpaceId = fixture.SpaceId,
            ProjectId = fixture.ProjectId,
            CreatedByMemberId = fixture.OwnerMemberId,
            Title = "Atividade do núcleo"
        };
        context.Activities.Add(activity);
        await context.SaveChangesAsync();

        var png = TestImageFactory.CreatePng(400, 300);
        await using var stream = new MemoryStream(png);
        await service.UploadActivityImageAsync(activity.Id, stream, stream.Length, "image/png", CancellationToken.None);

        await service.DeleteCoreAsync(fixture.CoreId, CancellationToken.None);

        Assert.Empty(storage.Objects);
    }

    [Fact]
    public async Task List_cores_still_respects_authorship_rules_for_invited_member()
    {
        await using var context = CreateDbContext();
        var fixture = await SeedFixtureAsync(context, includeInvitedMember: true, invitedRole: SpaceRole.Owner);
        await AddCoresAsync(context, fixture, 4);
        await UpgradeUserToBronzeAsync(context, fixture.OwnerUserId);
        var service = CreateService(context, fixture.MemberUserId!.Value, fixture.SpaceId);

        var cores = await service.ListCoresAsync(CancellationToken.None);

        Assert.Equal(5, cores.Count);
        Assert.All(cores, item => Assert.True(item.CanEdit));
        Assert.Equal(0, cores.Count(item => item.IsOutOfPlan));
    }

    private static OrganizaClubDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OrganizaClubDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new OrganizaClubDbContext(options);
    }

    private static ProjectService CreateService(
        OrganizaClubDbContext context,
        Guid userId,
        Guid spaceId,
        InMemoryObjectStorage? storage = null,
        TimeProvider? timeProvider = null)
    {
        var userContext = new TestUserContext(userId, spaceId);
        var resolvedStorage = storage ?? new InMemoryObjectStorage();
        var imageUploadProcessor = new ImageSharpImageUploadProcessor();
        var resolvedTimeProvider = timeProvider ?? TimeProvider.System;
        var commercialPlanService = new CommercialPlanService(context, userContext, resolvedTimeProvider);
        var managedImageQuotaService = new ManagedImageQuotaService(
            context,
            resolvedStorage,
            imageUploadProcessor,
            commercialPlanService,
            resolvedTimeProvider);

        return new ProjectService(
            context,
            userContext,
            resolvedStorage,
            imageUploadProcessor,
            resolvedTimeProvider,
            commercialPlanService,
            managedImageQuotaService);
    }

    private static async Task<ProjectFixture> SeedFixtureAsync(
        OrganizaClubDbContext context,
        bool includeInvitedMember = false,
        SpaceRole invitedRole = SpaceRole.Member)
    {
        var ownerUser = new AppUser
        {
            Email = "owner@organiza.club",
            PasswordHash = "hash",
            DisplayName = "Owner",
            SystemRole = SystemRole.User
        };
        var space = new Space
        {
            Name = "Espaço Teste",
            CreatedByUser = ownerUser
        };
        var ownerMember = new SpaceMember
        {
            Space = space,
            User = ownerUser,
            Role = SpaceRole.Owner
        };
        var core = new Core
        {
            Space = space,
            CreatedByMember = ownerMember,
            Name = "Núcleo"
        };
        var project = new Project
        {
            SpaceId = space.Id,
            Core = core,
            CreatedByMember = ownerMember,
            Name = "Projeto"
        };
        var openActivity = new Activity
        {
            SpaceId = space.Id,
            Project = project,
            CreatedByMember = ownerMember,
            Title = "Atividade aberta",
            Status = ActivityStatus.EmAndamento
        };
        var secondOpenActivity = new Activity
        {
            SpaceId = space.Id,
            Project = project,
            CreatedByMember = ownerMember,
            Title = "Outra aberta",
            Status = ActivityStatus.NaoIniciada
        };
        var closedActivity = new Activity
        {
            SpaceId = space.Id,
            Project = project,
            CreatedByMember = ownerMember,
            Title = "Atividade concluída",
            Status = ActivityStatus.Concluido
        };

        context.Users.Add(ownerUser);
        AppUser? invitedUser = null;

        if (includeInvitedMember)
        {
            invitedUser = new AppUser
            {
                Email = "invited@organiza.club",
                PasswordHash = "hash",
                DisplayName = "Invited",
                SystemRole = SystemRole.User
            };

            context.Users.Add(invitedUser);
            context.SpaceMembers.Add(new SpaceMember
            {
                Space = space,
                User = invitedUser,
                Role = invitedRole
            });
        }

        context.Spaces.Add(space);
        context.SpaceMembers.Add(ownerMember);
        context.Cores.Add(core);
        context.Projects.Add(project);
        context.Activities.AddRange(openActivity, secondOpenActivity, closedActivity);
        await context.SaveChangesAsync();

        return new ProjectFixture(
            space.Id,
            ownerUser.Id,
            ownerMember.Id,
            core.Id,
            project.Id,
            invitedUser?.Id);
    }

    private static async Task AddCoresAsync(
        OrganizaClubDbContext context,
        ProjectFixture fixture,
        int additionalCount)
    {
        var cores = Enumerable.Range(1, additionalCount)
            .Select(index => new Core
            {
                SpaceId = fixture.SpaceId,
                CreatedByMemberId = fixture.OwnerMemberId,
                Name = $"Núcleo {index}"
            })
            .ToArray();

        context.Cores.AddRange(cores);
        await context.SaveChangesAsync();

        var baseTime = new DateTimeOffset(2026, 1, 1, 0, 1, 0, TimeSpan.Zero);
        for (var index = 0; index < cores.Length; index++)
        {
            cores[index].CreatedAt = baseTime.AddMinutes(index);
            cores[index].UpdatedAt = cores[index].CreatedAt;
        }

        await context.SaveChangesAsync();
    }

    private static async Task AddProjectsAsync(
        OrganizaClubDbContext context,
        ProjectFixture fixture,
        int additionalCount)
    {
        var projects = Enumerable.Range(1, additionalCount)
            .Select(index => new Project
            {
                SpaceId = fixture.SpaceId,
                CoreId = fixture.CoreId,
                CreatedByMemberId = fixture.OwnerMemberId,
                Name = $"Projeto {index}"
            })
            .ToArray();

        context.Projects.AddRange(projects);
        await context.SaveChangesAsync();

        var baseTime = new DateTimeOffset(2026, 1, 1, 0, 1, 0, TimeSpan.Zero);
        for (var index = 0; index < projects.Length; index++)
        {
            projects[index].CreatedAt = baseTime.AddMinutes(index);
            projects[index].UpdatedAt = projects[index].CreatedAt;
        }

        await context.SaveChangesAsync();
    }

    private sealed record ProjectFixture(
        Guid SpaceId,
        Guid OwnerUserId,
        Guid OwnerMemberId,
        Guid CoreId,
        Guid ProjectId,
        Guid? MemberUserId);

    private static async Task UpgradeUserToBronzeAsync(OrganizaClubDbContext context, Guid userId)
    {
        var planService = new CommercialPlanService(context, new TestUserContext(userId, null), TimeProvider.System);
        await planService.EnsurePlanCatalogAsync(CancellationToken.None);
        var bronzePlan = await context.PlanDefinitions.SingleAsync(item => item.Slug == PlanDefinitionCatalog.BronzeSlug);

        context.UserSubscriptions.Add(new OrganizaClub.Domain.Plans.UserSubscription
        {
            UserId = userId,
            PlanDefinitionId = bronzePlan.Id,
            BillingCycle = OrganizaClub.Domain.Plans.BillingCycle.Monthly,
            StartsAt = DateTimeOffset.Parse("2026-07-01T00:00:00Z"),
            EndsAt = DateTimeOffset.Parse("2026-07-31T23:59:59Z"),
            AmountPaid = 19.90m,
            CurrencyCode = "BRL",
            Status = OrganizaClub.Domain.Plans.UserSubscriptionStatus.Active
        });

        await context.SaveChangesAsync();
    }

    private sealed class TestUserContext(Guid userId, Guid? spaceId) : IUserContext
    {
        public Guid UserId { get; } = userId;
        public SystemRole SystemRole => SystemRole.User;
        public Guid? SpaceId { get; } = spaceId;
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class InMemoryObjectStorage : IObjectStorage
    {
        public Dictionary<string, StoredObject> Objects { get; } = [];

        public Task EnsureBucketExistsAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public async Task PutAsync(ObjectStoragePutRequest request, CancellationToken cancellationToken)
        {
            using var buffer = new MemoryStream();
            await request.Content.CopyToAsync(buffer, cancellationToken);
            Objects[request.ObjectKey] = new StoredObject(request.ObjectKey, buffer.ToArray(), request.ContentType);
        }

        public Task<StoredObject> GetAsync(string objectKey, CancellationToken cancellationToken) =>
            Task.FromResult(Objects.TryGetValue(objectKey, out var objectValue)
                ? objectValue
                : throw new NotFoundException("Arquivo não encontrado."));

        public Task DeleteAsync(string objectKey, CancellationToken cancellationToken)
        {
            Objects.Remove(objectKey);
            return Task.CompletedTask;
        }
    }
}
