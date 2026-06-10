using HomePit.Application.Common;
using HomePit.Application.Projects;
using HomePit.Application.Storage;
using HomePit.Domain.Households;
using HomePit.Domain.Projects;
using HomePit.Infrastructure.Data;
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

    private static HomePitDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HomePitDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new HomePitDbContext(options);
    }

    private static ProjectService CreateService(HomePitDbContext context, Guid userId, Guid householdId)
    {
        return new ProjectService(
            context,
            new TestUserContext(userId, householdId),
            new InMemoryObjectStorage(),
            TimeProvider.System);
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
            Household = household,
            Universe = universe,
            CreatedByMember = ownerMember,
            Name = "Projeto"
        };
        var openActivity = new Activity
        {
            Household = household,
            Project = project,
            CreatedByMember = ownerMember,
            Title = "Atividade aberta",
            Status = ActivityStatus.EmAndamento
        };
        var secondOpenActivity = new Activity
        {
            Household = household,
            Project = project,
            CreatedByMember = ownerMember,
            Title = "Outra aberta",
            Status = ActivityStatus.NaoIniciada
        };
        var closedActivity = new Activity
        {
            Household = household,
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
            universe.Id,
            project.Id);
    }

    private sealed record ProjectFixture(
        Guid HouseholdId,
        Guid OwnerUserId,
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
        public Task EnsureBucketExistsAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task PutAsync(ObjectStoragePutRequest request, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<StoredObject> GetAsync(string objectKey, CancellationToken cancellationToken) =>
            Task.FromResult(new StoredObject(objectKey, Array.Empty<byte>(), "application/octet-stream"));

        public Task DeleteAsync(string objectKey, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
