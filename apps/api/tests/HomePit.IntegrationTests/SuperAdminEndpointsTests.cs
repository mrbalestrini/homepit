using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using HomePit.Application.Common;
using HomePit.Application.Storage;
using HomePit.Domain.Households;
using HomePit.Domain.Prompts;
using HomePit.Domain.Projects;
using HomePit.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace HomePit.IntegrationTests;

public sealed class SuperAdminEndpointsTests
{
    [Fact]
    public async Task Superadmin_can_read_any_household_but_cannot_write()
    {
        await using var factory = new HomePitApiFactory();
        using var client = factory.CreateClient();
        var seed = await SeedDataAsync(factory);

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "superadmin@homepit.dev",
            password = "super-secret"
        });

        loginResponse.EnsureSuccessStatusCode();
        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>(JsonSerializerOptions.Web);
        Assert.NotNull(auth);
        Assert.Equal("SuperAdmin", auth.User.SystemRole);
        Assert.Equal(2, auth.Households.Count);
        Assert.All(auth.Households, household => Assert.Equal("Member", household.Role));

        var householdsResponse = await SendAuthorizedAsync(client, auth.AccessToken, seed.PrimaryHouseholdId, HttpMethod.Get, "/api/households/members");
        householdsResponse.EnsureSuccessStatusCode();
        var members = await householdsResponse.Content.ReadFromJsonAsync<IReadOnlyCollection<HouseholdMemberResponse>>(JsonSerializerOptions.Web);
        Assert.NotNull(members);
        Assert.All(members, member => Assert.False(member.IsCurrentUser));

        var universesResponse = await SendAuthorizedAsync(client, auth.AccessToken, seed.PrimaryHouseholdId, HttpMethod.Get, "/api/universes");
        universesResponse.EnsureSuccessStatusCode();
        var universes = await universesResponse.Content.ReadFromJsonAsync<IReadOnlyCollection<UniverseResponse>>(JsonSerializerOptions.Web);
        var universe = Assert.Single(universes!);
        Assert.False(universe.CanEdit);
        Assert.False(universe.CanDelete);

        var projectsResponse = await SendAuthorizedAsync(client, auth.AccessToken, seed.PrimaryHouseholdId, HttpMethod.Get, "/api/projects");
        projectsResponse.EnsureSuccessStatusCode();
        var projects = await projectsResponse.Content.ReadFromJsonAsync<IReadOnlyCollection<ProjectResponse>>(JsonSerializerOptions.Web);
        var project = Assert.Single(projects!);
        Assert.False(project.CanEdit);
        Assert.False(project.CanDelete);

        var activitiesResponse = await SendAuthorizedAsync(client, auth.AccessToken, seed.PrimaryHouseholdId, HttpMethod.Get, "/api/activities");
        activitiesResponse.EnsureSuccessStatusCode();
        var activities = await activitiesResponse.Content.ReadFromJsonAsync<IReadOnlyCollection<ActivityResponse>>(JsonSerializerOptions.Web);
        var activity = Assert.Single(activities!);
        Assert.False(activity.CanEdit);
        Assert.False(activity.CanDelete);

        var promptsResponse = await SendAuthorizedAsync(client, auth.AccessToken, seed.PrimaryHouseholdId, HttpMethod.Get, "/api/prompts");
        promptsResponse.EnsureSuccessStatusCode();
        var prompts = await promptsResponse.Content.ReadFromJsonAsync<PromptListResponse>(JsonSerializerOptions.Web);
        Assert.NotNull(prompts);
        var prompt = Assert.Single(prompts.Items);
        Assert.False(prompt.CanEdit);
        Assert.False(prompt.CanDelete);

        var categoriesResponse = await SendAuthorizedAsync(client, auth.AccessToken, seed.PrimaryHouseholdId, HttpMethod.Get, "/api/prompt-categories");
        categoriesResponse.EnsureSuccessStatusCode();
        var categories = await categoriesResponse.Content.ReadFromJsonAsync<IReadOnlyCollection<PromptCategoryResponse>>(JsonSerializerOptions.Web);
        var category = Assert.Single(categories!);
        Assert.False(category.CanEdit);
        Assert.False(category.CanDelete);

        Assert.Equal(HttpStatusCode.Forbidden, (await SendAuthorizedAsync(
            client,
            auth.AccessToken,
            seed.PrimaryHouseholdId,
            HttpMethod.Post,
            "/api/households",
            JsonContent.Create(new { name = "Nova casa" }))).StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, (await SendAuthorizedAsync(
            client,
            auth.AccessToken,
            seed.PrimaryHouseholdId,
            HttpMethod.Post,
            "/api/households/share",
            JsonContent.Create(new { email = "member@homepit.dev", role = "Member" }))).StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, (await SendAuthorizedAsync(
            client,
            auth.AccessToken,
            seed.PrimaryHouseholdId,
            HttpMethod.Post,
            "/api/universes",
            JsonContent.Create(new { name = "Novo universo", imageUrl = (string?)null }))).StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, (await SendAuthorizedAsync(
            client,
            auth.AccessToken,
            seed.PrimaryHouseholdId,
            HttpMethod.Post,
            "/api/activities",
            JsonContent.Create(new
            {
                projectId = seed.ProjectId,
                title = "Nova atividade",
                description = (string?)null,
                status = "NaoIniciada",
                priority = "Media",
                size = (decimal?)null,
                responsibleMemberId = (Guid?)null
            }))).StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, (await SendAuthorizedAsync(
            client,
            auth.AccessToken,
            seed.PrimaryHouseholdId,
            HttpMethod.Post,
            "/api/prompts",
            JsonContent.Create(new
            {
                universeId = seed.UniverseId,
                title = "Novo prompt",
                description = (string?)null,
                promptText = "Conteúdo",
                categoryIds = new[] { seed.CategoryId },
                linkUrl = (string?)null,
                linkTitle = (string?)null
            }))).StatusCode);
    }

    private static async Task<SeedResult> SeedDataAsync(HomePitApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HomePitDbContext>();

        var owner = new AppUser
        {
            Email = "owner@homepit.dev",
            PasswordHash = "hash",
            DisplayName = "Owner",
            SystemRole = SystemRole.User
        };
        var member = new AppUser
        {
            Email = "member@homepit.dev",
            PasswordHash = "hash",
            DisplayName = "Member",
            SystemRole = SystemRole.User
        };

        var primaryHousehold = new Household { Name = "Casa Principal" };
        var secondaryHousehold = new Household { Name = "Casa Secundária" };
        var ownerMember = new HouseholdMember { Household = primaryHousehold, User = owner, Role = HouseholdRole.Owner };
        var regularMember = new HouseholdMember { Household = primaryHousehold, User = member, Role = HouseholdRole.Member };

        var universe = new Universe
        {
            Household = primaryHousehold,
            CreatedByMember = ownerMember,
            Name = "Universo"
        };
        var project = new Project
        {
            Household = primaryHousehold,
            Universe = universe,
            CreatedByMember = ownerMember,
            Name = "Projeto"
        };
        var activity = new Activity
        {
            Household = primaryHousehold,
            Project = project,
            CreatedByMember = ownerMember,
            Title = "Atividade",
            Status = ActivityStatus.NaoIniciada,
            Priority = Priority.Media
        };
        var category = new PromptCategory
        {
            Household = primaryHousehold,
            CreatedByMember = ownerMember,
            Name = "Categoria"
        };
        var prompt = new Prompt
        {
            Household = primaryHousehold,
            Universe = universe,
            CreatedByMember = ownerMember,
            Title = "Prompt",
            PromptText = "Conteúdo"
        };
        prompt.CategoryAssignments.Add(new PromptCategoryAssignment
        {
            Prompt = prompt,
            Category = category
        });

        db.Users.AddRange(owner, member);
        db.Households.AddRange(primaryHousehold, secondaryHousehold);
        db.HouseholdMembers.AddRange(ownerMember, regularMember);
        db.Universes.Add(universe);
        db.Projects.Add(project);
        db.Activities.Add(activity);
        db.PromptCategories.Add(category);
        db.Prompts.Add(prompt);
        await db.SaveChangesAsync();

        return new SeedResult(primaryHousehold.Id, universe.Id, project.Id, category.Id.ToString());
    }

    private static HttpRequestMessage CreateAuthorizedRequest(
        string accessToken,
        Guid householdId,
        HttpMethod method,
        string path)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Add("X-Household-Id", householdId.ToString());
        return request;
    }

    private static async Task<HttpResponseMessage> SendAuthorizedAsync(
        HttpClient client,
        string accessToken,
        Guid householdId,
        HttpMethod method,
        string path,
        HttpContent? content = null)
    {
        using var request = CreateAuthorizedRequest(accessToken, householdId, method, path);
        request.Content = content;
        return await client.SendAsync(request);
    }

    private sealed record SeedResult(Guid PrimaryHouseholdId, Guid UniverseId, Guid ProjectId, string CategoryId);

    private sealed record AuthResponse(string AccessToken, string RefreshToken, string ExpiresAt, AuthUserResponse User, IReadOnlyCollection<AuthHouseholdResponse> Households);

    private sealed record AuthUserResponse(string Id, string Email, string DisplayName, string SystemRole);

    private sealed record AuthHouseholdResponse(string Id, string Name, string Role);

    private sealed record HouseholdMemberResponse(Guid Id, Guid UserId, string DisplayName, string Email, string? PhoneNumber, string Role, bool IsCurrentUser);

    private sealed record UniverseResponse(Guid Id, string Name, bool CanEdit, bool CanDelete);

    private sealed record ProjectResponse(Guid Id, Guid UniverseId, string Name, bool CanEdit, bool CanDelete);

    private sealed record ActivityResponse(Guid Id, Guid ProjectId, string Title, string Status, bool CanEdit, bool CanDelete);

    private sealed record PromptCategoryResponse(Guid Id, string Name, bool CanEdit, bool CanDelete);

    private sealed record PromptListResponse(IReadOnlyCollection<PromptListItemResponse> Items, int Page, int PageSize, int TotalCount);

    private sealed record PromptListItemResponse(Guid Id, string Title, bool CanEdit, bool CanDelete);

    private sealed class HomePitApiFactory : WebApplicationFactory<Program>, IAsyncDisposable
    {
        private readonly string databaseName = Guid.NewGuid().ToString("N");
        private readonly FakeObjectStorage storage = new();

        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            builder.UseSetting(Microsoft.AspNetCore.Hosting.WebHostDefaults.EnvironmentKey, "Development");
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Database:ApplyMigrationsOnStartup"] = "false",
                    ["Notifications:DailyDigestEnabled"] = "false",
                    ["ObjectStorage:CreateBucketOnStartup"] = "true",
                    ["SuperAdmin:Email"] = "superadmin@homepit.dev",
                    ["SuperAdmin:Password"] = "super-secret",
                    ["SuperAdmin:DisplayName"] = "SuperAdmin"
                });
            });

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<HomePitDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<HomePitDbContext>>();
                services.RemoveAll<HomePitDbContext>();
                services.RemoveAll<IHomePitDbContext>();
                services.RemoveAll<IObjectStorage>();

                services.AddDbContext<HomePitDbContext>(options => options.UseInMemoryDatabase(databaseName));
                services.AddScoped<IHomePitDbContext>(provider => provider.GetRequiredService<HomePitDbContext>());
                services.AddSingleton<IObjectStorage>(storage);
            });
        }

        public new async ValueTask DisposeAsync()
        {
            await base.DisposeAsync();
        }
    }

    private sealed class FakeObjectStorage : IObjectStorage
    {
        public Task EnsureBucketExistsAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<StoredObject> GetAsync(string objectKey, CancellationToken cancellationToken) =>
            throw new NotFoundException("Arquivo não encontrado.");

        public Task PutAsync(ObjectStoragePutRequest request, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task DeleteAsync(string objectKey, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
