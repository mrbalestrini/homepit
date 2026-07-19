using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using OrganizaClub.Application.Common;
using OrganizaClub.Application.Storage;
using OrganizaClub.Domain.Gsm;
using OrganizaClub.Domain.Spaces;
using OrganizaClub.Domain.Prompts;
using OrganizaClub.Domain.Projects;
using OrganizaClub.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace OrganizaClub.IntegrationTests;

public sealed class SuperAdminEndpointsTests
{
    [Fact]
    public async Task Superadmin_can_read_any_space_but_cannot_write()
    {
        await using var factory = new OrganizaClubApiFactory();
        using var client = factory.CreateClient();
        var seed = await SeedDataAsync(factory);

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "superadmin@organiza.club",
            password = "super-secret"
        });

        loginResponse.EnsureSuccessStatusCode();
        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>(JsonSerializerOptions.Web);
        Assert.NotNull(auth);
        Assert.Equal("SuperAdmin", auth.User.SystemRole);
        Assert.Equal(2, auth.Spaces.Count);
        Assert.All(auth.Spaces, space => Assert.Equal("Member", space.Role));

        var spacesResponse = await SendAuthorizedAsync(client, auth.AccessToken, seed.PrimarySpaceId, HttpMethod.Get, "/api/spaces/members");
        spacesResponse.EnsureSuccessStatusCode();
        var members = await spacesResponse.Content.ReadFromJsonAsync<IReadOnlyCollection<SpaceMemberResponse>>(JsonSerializerOptions.Web);
        Assert.NotNull(members);
        Assert.All(members, member => Assert.False(member.IsCurrentUser));

        var coresResponse = await SendAuthorizedAsync(client, auth.AccessToken, seed.PrimarySpaceId, HttpMethod.Get, "/api/cores");
        coresResponse.EnsureSuccessStatusCode();
        var cores = await coresResponse.Content.ReadFromJsonAsync<IReadOnlyCollection<CoreResponse>>(JsonSerializerOptions.Web);
        var core = Assert.Single(cores!);
        Assert.False(core.CanEdit);
        Assert.False(core.CanDelete);

        var projectsResponse = await SendAuthorizedAsync(client, auth.AccessToken, seed.PrimarySpaceId, HttpMethod.Get, "/api/projects");
        projectsResponse.EnsureSuccessStatusCode();
        var projects = await projectsResponse.Content.ReadFromJsonAsync<IReadOnlyCollection<ProjectResponse>>(JsonSerializerOptions.Web);
        var project = Assert.Single(projects!);
        Assert.False(project.CanEdit);
        Assert.False(project.CanDelete);

        var activitiesResponse = await SendAuthorizedAsync(client, auth.AccessToken, seed.PrimarySpaceId, HttpMethod.Get, "/api/activities");
        activitiesResponse.EnsureSuccessStatusCode();
        var activities = await activitiesResponse.Content.ReadFromJsonAsync<IReadOnlyCollection<ActivityResponse>>(JsonSerializerOptions.Web);
        var activity = Assert.Single(activities!);
        Assert.NotEqual(default, activity.CreatedAt);
        Assert.Null(activity.DueDate);
        Assert.False(activity.CanEdit);
        Assert.False(activity.CanDelete);

        var promptsResponse = await SendAuthorizedAsync(client, auth.AccessToken, seed.PrimarySpaceId, HttpMethod.Get, "/api/prompts");
        promptsResponse.EnsureSuccessStatusCode();
        var prompts = await promptsResponse.Content.ReadFromJsonAsync<PromptListResponse>(JsonSerializerOptions.Web);
        Assert.NotNull(prompts);
        var prompt = Assert.Single(prompts.Items);
        Assert.False(prompt.CanEdit);
        Assert.False(prompt.CanDelete);

        var categoriesResponse = await SendAuthorizedAsync(client, auth.AccessToken, seed.PrimarySpaceId, HttpMethod.Get, "/api/prompt-categories");
        categoriesResponse.EnsureSuccessStatusCode();
        var categories = await categoriesResponse.Content.ReadFromJsonAsync<IReadOnlyCollection<PromptCategoryResponse>>(JsonSerializerOptions.Web);
        var category = Assert.Single(categories!);
        Assert.False(category.CanEdit);
        Assert.False(category.CanDelete);

        var gsmResponse = await SendAuthorizedAsync(client, auth.AccessToken, seed.PrimarySpaceId, HttpMethod.Get, "/api/gsm-numbers");
        gsmResponse.EnsureSuccessStatusCode();
        var gsmNumbers = await gsmResponse.Content.ReadFromJsonAsync<IReadOnlyCollection<GsmNumberResponse>>(JsonSerializerOptions.Web);
        var gsmNumber = Assert.Single(gsmNumbers!);
        Assert.False(gsmNumber.CanEdit);
        Assert.False(gsmNumber.CanDelete);

        var rechargeListResponse = await SendAuthorizedAsync(
            client,
            auth.AccessToken,
            seed.PrimarySpaceId,
            HttpMethod.Get,
            $"/api/gsm-numbers/{gsmNumber.Id}/recharges");

        rechargeListResponse.EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.Forbidden, (await SendAuthorizedAsync(
            client,
            auth.AccessToken,
            seed.PrimarySpaceId,
            HttpMethod.Post,
            $"/api/gsm-numbers/{gsmNumber.Id}/recharges",
            JsonContent.Create(new
            {
                rechargedOn = new DateOnly(2026, 6, 20),
                amount = 50m,
                note = "Recarga de teste"
            }))).StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, (await SendAuthorizedAsync(
            client,
            auth.AccessToken,
            seed.PrimarySpaceId,
            HttpMethod.Post,
            "/api/spaces",
            JsonContent.Create(new { name = "Novo espaço" }))).StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, (await SendAuthorizedAsync(
            client,
            auth.AccessToken,
            seed.PrimarySpaceId,
            HttpMethod.Post,
            "/api/spaces/share",
            JsonContent.Create(new { email = "member@organiza.club", role = "Member" }))).StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, (await SendAuthorizedAsync(
            client,
            auth.AccessToken,
            seed.PrimarySpaceId,
            HttpMethod.Post,
            "/api/cores",
            JsonContent.Create(new { name = "Novo núcleo", imageUrl = (string?)null }))).StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, (await SendAuthorizedAsync(
            client,
            auth.AccessToken,
            seed.PrimarySpaceId,
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
            seed.PrimarySpaceId,
            HttpMethod.Post,
            "/api/prompts",
            JsonContent.Create(new
            {
                coreId = seed.CoreId,
                title = "Novo prompt",
                description = (string?)null,
                promptText = "Conteúdo",
                categoryIds = new[] { seed.CategoryId },
                linkUrl = (string?)null,
                linkTitle = (string?)null
            }))).StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, (await SendAuthorizedAsync(
            client,
            auth.AccessToken,
            seed.PrimarySpaceId,
            HttpMethod.Post,
            "/api/gsm-numbers",
            JsonContent.Create(new
            {
                title = "Nova linha",
                number = "11912345678",
                description = (string?)null,
                daysWithoutRecharge = (int?)null,
                acquiredOn = new DateOnly(2026, 1, 10),
                status = "Ativo"
            }))).StatusCode);
    }

    private static async Task<SeedResult> SeedDataAsync(OrganizaClubApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OrganizaClubDbContext>();

        var owner = new AppUser
        {
            Email = "owner@organiza.club",
            PasswordHash = "hash",
            DisplayName = "Owner",
            SystemRole = SystemRole.User
        };
        var member = new AppUser
        {
            Email = "member@organiza.club",
            PasswordHash = "hash",
            DisplayName = "Member",
            SystemRole = SystemRole.User
        };

        var primarySpace = new Space { Name = "Espaço Principal" };
        var secondarySpace = new Space { Name = "Espaço Secundária" };
        var ownerMember = new SpaceMember { Space = primarySpace, User = owner, Role = SpaceRole.Owner };
        var regularMember = new SpaceMember { Space = primarySpace, User = member, Role = SpaceRole.Member };

        var core = new Core
        {
            Space = primarySpace,
            CreatedByMember = ownerMember,
            Name = "Núcleo"
        };
        var project = new Project
        {
            SpaceId = primarySpace.Id,
            Core = core,
            CreatedByMember = ownerMember,
            Name = "Projeto"
        };
        var activity = new Activity
        {
            SpaceId = primarySpace.Id,
            Project = project,
            CreatedByMember = ownerMember,
            Title = "Atividade",
            Status = ActivityStatus.NaoIniciada,
            Priority = Priority.Media
        };
        var category = new PromptCategory
        {
            Space = primarySpace,
            CreatedByMember = ownerMember,
            Name = "Categoria"
        };
        var prompt = new Prompt
        {
            Space = primarySpace,
            Core = core,
            CreatedByMember = ownerMember,
            Title = "Prompt",
            PromptText = "Conteúdo"
        };
        var gsmNumber = new GsmNumber
        {
            Space = primarySpace,
            CreatedByMember = ownerMember,
            Title = "Linho espaço",
            NormalizedNumber = "5511912345678",
            AcquiredOn = new DateOnly(2026, 1, 10),
            Status = GsmNumberStatus.Ativo
        };
        prompt.CategoryAssignments.Add(new PromptCategoryAssignment
        {
            Prompt = prompt,
            Category = category
        });

        db.Users.AddRange(owner, member);
        db.Spaces.AddRange(primarySpace, secondarySpace);
        db.SpaceMembers.AddRange(ownerMember, regularMember);
        db.Cores.Add(core);
        db.Projects.Add(project);
        db.Activities.Add(activity);
        db.PromptCategories.Add(category);
        db.Prompts.Add(prompt);
        db.GsmNumbers.Add(gsmNumber);
        await db.SaveChangesAsync();

        return new SeedResult(primarySpace.Id, core.Id, project.Id, category.Id.ToString());
    }

    private static HttpRequestMessage CreateAuthorizedRequest(
        string accessToken,
        Guid spaceId,
        HttpMethod method,
        string path)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Add("X-Space-Id", spaceId.ToString());
        return request;
    }

    private static async Task<HttpResponseMessage> SendAuthorizedAsync(
        HttpClient client,
        string accessToken,
        Guid spaceId,
        HttpMethod method,
        string path,
        HttpContent? content = null)
    {
        using var request = CreateAuthorizedRequest(accessToken, spaceId, method, path);
        request.Content = content;
        return await client.SendAsync(request);
    }

    private sealed record SeedResult(Guid PrimarySpaceId, Guid CoreId, Guid ProjectId, string CategoryId);

    private sealed record AuthResponse(string AccessToken, string RefreshToken, string ExpiresAt, AuthUserResponse User, IReadOnlyCollection<AuthSpaceResponse> Spaces);

    private sealed record AuthUserResponse(string Id, string Email, string DisplayName, string SystemRole);

    private sealed record AuthSpaceResponse(string Id, string Name, string Role);

    private sealed record SpaceMemberResponse(Guid Id, Guid UserId, string DisplayName, string Email, string? PhoneNumber, string Role, bool IsCurrentUser);

    private sealed record CoreResponse(Guid Id, string Name, bool CanEdit, bool CanDelete);

    private sealed record ProjectResponse(Guid Id, Guid CoreId, string Name, bool CanEdit, bool CanDelete);

    private sealed record ActivityResponse(
        Guid Id,
        Guid ProjectId,
        string Title,
        string Status,
        DateTimeOffset CreatedAt,
        DateOnly? DueDate,
        bool CanEdit,
        bool CanDelete);

    private sealed record PromptCategoryResponse(Guid Id, string Name, bool CanEdit, bool CanDelete);

    private sealed record GsmNumberResponse(Guid Id, bool CanEdit, bool CanDelete);

    private sealed record PromptListResponse(IReadOnlyCollection<PromptListItemResponse> Items, int Page, int PageSize, int TotalCount);

    private sealed record PromptListItemResponse(Guid Id, string Title, bool CanEdit, bool CanDelete);

    private sealed class OrganizaClubApiFactory : WebApplicationFactory<Program>, IAsyncDisposable
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
                    ["SuperAdmin:Email"] = "superadmin@organiza.club",
                    ["SuperAdmin:Password"] = "super-secret",
                    ["SuperAdmin:DisplayName"] = "SuperAdmin"
                });
            });

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<OrganizaClubDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<OrganizaClubDbContext>>();
                services.RemoveAll<OrganizaClubDbContext>();
                services.RemoveAll<IOrganizaClubDbContext>();
                services.RemoveAll<IObjectStorage>();

                services.AddDbContext<OrganizaClubDbContext>(options => options.UseInMemoryDatabase(databaseName));
                services.AddScoped<IOrganizaClubDbContext>(provider => provider.GetRequiredService<OrganizaClubDbContext>());
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
