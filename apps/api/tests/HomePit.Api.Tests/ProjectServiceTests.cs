using HomePit.Application.Common;
using HomePit.Application.Plans;
using HomePit.Application.Projects;
using HomePit.Application.Storage;
using HomePit.Domain.Households;
using HomePit.Domain.Projects;
using HomePit.Infrastructure.Data;
using HomePit.Infrastructure.Images;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HomePit.Api.Tests;

public sealed class ProjectServiceTests
{
    [Fact]
    public async Task List_projects_counts_only_open_activities()
    {
        await using var context = CreateDbContext();
        var fixture = await SeedFixtureAsync(context);
        var service = CreateService(context, fixture.OwnerUserId, fixture.HouseholdId);

        var projects = await service.ListProjectsAsync(null, CancellationToken.None);

        var project = Assert.Single(projects);
        Assert.Equal(2, project.ActivityCount);
    }

    [Fact]
    public async Task List_universes_marks_newer_items_as_read_only_when_the_plan_is_exceeded()
    {
        await using var context = CreateDbContext();
        var fixture = await SeedFixtureAsync(context);
        await AddUniversesAsync(context, fixture, 4);
        var service = CreateService(context, fixture.OwnerUserId, fixture.HouseholdId);

        var universes = await service.ListUniversesAsync(CancellationToken.None);

        Assert.Equal(5, universes.Count);
        Assert.Equal(3, universes.Count(item => item.CanEdit));
        Assert.Equal(2, universes.Count(item => !item.CanEdit));
        Assert.All(universes, item => Assert.True(item.CanDelete));
    }

    [Fact]
    public async Task List_projects_marks_newer_items_as_read_only_when_the_plan_is_exceeded()
    {
        await using var context = CreateDbContext();
        var fixture = await SeedFixtureAsync(context);
        await AddProjectsAsync(context, fixture, 4);
        var service = CreateService(context, fixture.OwnerUserId, fixture.HouseholdId);

        var projects = await service.ListProjectsAsync(fixture.UniverseId, CancellationToken.None);

        Assert.Equal(5, projects.Count);
        Assert.Equal(3, projects.Count(item => item.CanEdit));
        Assert.Equal(2, projects.Count(item => !item.CanEdit));
        Assert.All(projects, item => Assert.True(item.CanDelete));
    }

    [Fact]
    public async Task Update_project_returns_only_open_activity_count()
    {
        await using var context = CreateDbContext();
        var fixture = await SeedFixtureAsync(context);
        var service = CreateService(context, fixture.OwnerUserId, fixture.HouseholdId);

        var updated = await service.UpdateProjectAsync(
            fixture.ProjectId,
            new UpdateProjectRequest(fixture.UniverseId, "Projeto atualizado"),
            CancellationToken.None);

        Assert.Equal(2, updated.ActivityCount);
    }

    [Fact]
    public async Task Uploading_activity_image_sets_metadata_and_storage_key()
    {
        await using var context = CreateDbContext();
        var fixture = await SeedFixtureAsync(context);
        var storage = new InMemoryObjectStorage();
        var service = CreateService(context, fixture.OwnerUserId, fixture.HouseholdId, storage);

        var activity = new Activity
        {
            HouseholdId = fixture.HouseholdId,
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
    public async Task Uploading_universe_image_sets_metadata_and_storage_key()
    {
        await using var context = CreateDbContext();
        var fixture = await SeedFixtureAsync(context);
        var storage = new InMemoryObjectStorage();
        var service = CreateService(context, fixture.OwnerUserId, fixture.HouseholdId, storage);

        var jpeg = TestImageFactory.CreateJpeg(640, 360);
        await using var stream = new MemoryStream(jpeg);
        var result = await service.UploadUniverseImageAsync(fixture.UniverseId, stream, stream.Length, "image/jpeg", CancellationToken.None);

        Assert.True(result.HasImage);
        Assert.NotNull(result.ImageUpdatedAt);
        Assert.Equal(ObjectStorageKeys.UniverseImage(fixture.UniverseId), storage.Objects.Single().Key);
        Assert.Equal("image/webp", storage.Objects.Single().Value.ContentType);
    }

    [Fact]
    public async Task Deleting_activity_image_clears_storage_and_metadata()
    {
        await using var context = CreateDbContext();
        var fixture = await SeedFixtureAsync(context);
        var storage = new InMemoryObjectStorage();
        var service = CreateService(context, fixture.OwnerUserId, fixture.HouseholdId, storage);

        var activity = new Activity
        {
            HouseholdId = fixture.HouseholdId,
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
        var service = CreateService(context, fixture.OwnerUserId, fixture.HouseholdId, storage);

        var activity = new Activity
        {
            HouseholdId = fixture.HouseholdId,
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
    public async Task Deleting_universe_removes_activity_images_from_storage()
    {
        await using var context = CreateDbContext();
        var fixture = await SeedFixtureAsync(context);
        var storage = new InMemoryObjectStorage();
        var service = CreateService(context, fixture.OwnerUserId, fixture.HouseholdId, storage);

        var activity = new Activity
        {
            HouseholdId = fixture.HouseholdId,
            ProjectId = fixture.ProjectId,
            CreatedByMemberId = fixture.OwnerMemberId,
            Title = "Atividade do universo"
        };
        context.Activities.Add(activity);
        await context.SaveChangesAsync();

        var png = TestImageFactory.CreatePng(400, 300);
        await using var stream = new MemoryStream(png);
        await service.UploadActivityImageAsync(activity.Id, stream, stream.Length, "image/png", CancellationToken.None);

        await service.DeleteUniverseAsync(fixture.UniverseId, CancellationToken.None);

        Assert.Empty(storage.Objects);
    }

    private static HomePitDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HomePitDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new HomePitDbContext(options);
    }

    private static ProjectService CreateService(
        HomePitDbContext context,
        Guid userId,
        Guid householdId,
        InMemoryObjectStorage? storage = null)
    {
        var userContext = new TestUserContext(userId, householdId);
        var resolvedStorage = storage ?? new InMemoryObjectStorage();
        var imageUploadProcessor = new ImageSharpImageUploadProcessor();
        var commercialPlanService = new CommercialPlanService(context, userContext, TimeProvider.System);
        var managedImageQuotaService = new ManagedImageQuotaService(
            context,
            resolvedStorage,
            imageUploadProcessor,
            commercialPlanService,
            TimeProvider.System);

        return new ProjectService(
            context,
            userContext,
            resolvedStorage,
            imageUploadProcessor,
            TimeProvider.System,
            commercialPlanService,
            managedImageQuotaService);
    }

    private static async Task<ProjectFixture> SeedFixtureAsync(HomePitDbContext context)
    {
        var ownerUser = new AppUser
        {
            Email = "owner@homepit.dev",
            PasswordHash = "hash",
            DisplayName = "Owner",
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
        var universe = new Universe
        {
            Household = household,
            CreatedByMember = ownerMember,
            Name = "Universo"
        };
        var project = new Project
        {
            HouseholdId = household.Id,
            Universe = universe,
            CreatedByMember = ownerMember,
            Name = "Projeto"
        };
        var openActivity = new Activity
        {
            HouseholdId = household.Id,
            Project = project,
            CreatedByMember = ownerMember,
            Title = "Atividade aberta",
            Status = ActivityStatus.EmAndamento
        };
        var secondOpenActivity = new Activity
        {
            HouseholdId = household.Id,
            Project = project,
            CreatedByMember = ownerMember,
            Title = "Outra aberta",
            Status = ActivityStatus.NaoIniciada
        };
        var closedActivity = new Activity
        {
            HouseholdId = household.Id,
            Project = project,
            CreatedByMember = ownerMember,
            Title = "Atividade concluída",
            Status = ActivityStatus.Concluido
        };

        context.Users.Add(ownerUser);
        context.Households.Add(household);
        context.HouseholdMembers.Add(ownerMember);
        context.Universes.Add(universe);
        context.Projects.Add(project);
        context.Activities.AddRange(openActivity, secondOpenActivity, closedActivity);
        await context.SaveChangesAsync();

        return new ProjectFixture(
            household.Id,
            ownerUser.Id,
            ownerMember.Id,
            universe.Id,
            project.Id);
    }

    private static async Task AddUniversesAsync(
        HomePitDbContext context,
        ProjectFixture fixture,
        int additionalCount)
    {
        var universes = Enumerable.Range(1, additionalCount)
            .Select(index => new Universe
            {
                HouseholdId = fixture.HouseholdId,
                CreatedByMemberId = fixture.OwnerMemberId,
                Name = $"Universo {index}"
            })
            .ToArray();

        context.Universes.AddRange(universes);
        await context.SaveChangesAsync();

        var baseTime = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        for (var index = 0; index < universes.Length; index++)
        {
            universes[index].CreatedAt = baseTime.AddMinutes(index);
            universes[index].UpdatedAt = universes[index].CreatedAt;
        }

        await context.SaveChangesAsync();
    }

    private static async Task AddProjectsAsync(
        HomePitDbContext context,
        ProjectFixture fixture,
        int additionalCount)
    {
        var projects = Enumerable.Range(1, additionalCount)
            .Select(index => new Project
            {
                HouseholdId = fixture.HouseholdId,
                UniverseId = fixture.UniverseId,
                CreatedByMemberId = fixture.OwnerMemberId,
                Name = $"Projeto {index}"
            })
            .ToArray();

        context.Projects.AddRange(projects);
        await context.SaveChangesAsync();

        var baseTime = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        for (var index = 0; index < projects.Length; index++)
        {
            projects[index].CreatedAt = baseTime.AddMinutes(index);
            projects[index].UpdatedAt = projects[index].CreatedAt;
        }

        await context.SaveChangesAsync();
    }

    private sealed record ProjectFixture(
        Guid HouseholdId,
        Guid OwnerUserId,
        Guid OwnerMemberId,
        Guid UniverseId,
        Guid ProjectId);

    private sealed class TestUserContext(Guid userId, Guid? householdId) : IUserContext
    {
        public Guid UserId { get; } = userId;
        public SystemRole SystemRole => SystemRole.User;
        public Guid? HouseholdId { get; } = householdId;
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
