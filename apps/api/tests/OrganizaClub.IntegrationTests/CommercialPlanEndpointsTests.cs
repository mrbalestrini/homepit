using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using OrganizaClub.Application.Common;
using OrganizaClub.Application.Storage;
using OrganizaClub.Domain.Spaces;
using OrganizaClub.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace OrganizaClub.IntegrationTests;

public sealed class CommercialPlanEndpointsTests
{
    [Fact]
    public async Task Free_user_cannot_create_space()
    {
        await using var factory = new OrganizaClubApiFactory();
        using var client = factory.CreateClient();

        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email = "free@organiza.club",
            password = "free-secret",
            displayName = "Free User",
            phoneNumber = (string?)null
        });

        registerResponse.EnsureSuccessStatusCode();
        var auth = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>(JsonSerializerOptions.Web);
        Assert.NotNull(auth);

        var createSpaceResponse = await SendAuthorizedAsync(
            client,
            auth.AccessToken,
            spaceId: null,
            HttpMethod.Post,
            "/api/spaces",
            JsonContent.Create(new { name = "Espaço Bloqueada" }));

        Assert.Equal(HttpStatusCode.BadRequest, createSpaceResponse.StatusCode);
        var error = await createSpaceResponse.Content.ReadFromJsonAsync<ProblemDetailsResponse>(JsonSerializerOptions.Web);
        Assert.Equal("O plano Free não permite criar espaços próprios.", error?.Detail);
    }

    [Fact]
    public async Task Superadmin_can_manage_plans_and_subscriptions()
    {
        await using var factory = new OrganizaClubApiFactory();
        using var client = factory.CreateClient();
        var seed = await SeedUserAsync(factory);

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "superadmin@organiza.club",
            password = "super-secret"
        });

        loginResponse.EnsureSuccessStatusCode();
        var superAdminAuth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>(JsonSerializerOptions.Web);
        Assert.NotNull(superAdminAuth);

        var plansResponse = await SendAuthorizedAsync(client, superAdminAuth.AccessToken, null, HttpMethod.Get, "/api/admin/platform/plans");
        plansResponse.EnsureSuccessStatusCode();
        var plans = await plansResponse.Content.ReadFromJsonAsync<IReadOnlyCollection<PlanDefinitionResponse>>(JsonSerializerOptions.Web);
        Assert.NotNull(plans);
        Assert.Equal(5, plans.Count);
        Assert.True(plans!.Single(item => item.Slug == "gold").IsPopular);

        var standardPlan = Assert.Single(plans, item => item.Slug == "standard");
        var updatePlanResponse = await SendAuthorizedAsync(
            client,
            superAdminAuth.AccessToken,
            null,
            HttpMethod.Put,
            $"/api/admin/platform/plans/{standardPlan.Id}",
            JsonContent.Create(new
            {
                monthlyPrice = 11.90m,
                annualPrice = 119.00m,
                maxOwnedSpaces = 1,
                maxCores = 4,
                maxProjects = 4,
                maxInvitedMembers = 8,
                maxOriginalImages = 35,
                showInCatalog = true,
                isPopular = true
            }));

        updatePlanResponse.EnsureSuccessStatusCode();

        var updatedPlansResponse = await SendAuthorizedAsync(client, superAdminAuth.AccessToken, null, HttpMethod.Get, "/api/admin/platform/plans");
        updatedPlansResponse.EnsureSuccessStatusCode();
        var updatedPlans = await updatedPlansResponse.Content.ReadFromJsonAsync<IReadOnlyCollection<PlanDefinitionResponse>>(JsonSerializerOptions.Web);
        Assert.NotNull(updatedPlans);
        Assert.True(updatedPlans!.Single(item => item.Slug == "standard").IsPopular);
        Assert.False(updatedPlans.Single(item => item.Slug == "gold").IsPopular);

        var createSubscriptionResponse = await SendAuthorizedAsync(
            client,
            superAdminAuth.AccessToken,
            null,
            HttpMethod.Post,
            "/api/admin/platform/subscriptions",
            JsonContent.Create(new
            {
                userId = seed.UserId,
                planDefinitionId = standardPlan.Id,
                billingCycle = "Monthly",
                startsAt = DateTimeOffset.UtcNow.AddDays(-1),
                endsAt = DateTimeOffset.UtcNow.AddDays(29),
                amountPaid = 0m,
                currencyCode = "BRL",
                status = "Active",
                adminNote = "voucher"
            }));

        createSubscriptionResponse.EnsureSuccessStatusCode();

        var usersResponse = await SendAuthorizedAsync(client, superAdminAuth.AccessToken, null, HttpMethod.Get, "/api/admin/users");
        usersResponse.EnsureSuccessStatusCode();
        var users = await usersResponse.Content.ReadFromJsonAsync<IReadOnlyCollection<AdminUserListItemResponse>>(JsonSerializerOptions.Web);
        var seededUser = Assert.Single(users!, item => item.Id == seed.UserId);
        Assert.Equal("Standard", seededUser.EffectivePlanName);
        Assert.Equal("Active", seededUser.ActiveSubscriptionStatus);
    }

    [Fact]
    public async Task Public_plan_catalog_hides_unlisted_plans_but_keeps_the_current_plan_for_the_user()
    {
        await using var factory = new OrganizaClubApiFactory();
        using var client = factory.CreateClient();

        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email = "catalog-user@organiza.club",
            password = "catalog-secret",
            displayName = "Catalog User",
            phoneNumber = (string?)null
        });

        registerResponse.EnsureSuccessStatusCode();
        var userAuth = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>(JsonSerializerOptions.Web);
        Assert.NotNull(userAuth);

        var adminLoginResponse = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "superadmin@organiza.club",
            password = "super-secret"
        });

        adminLoginResponse.EnsureSuccessStatusCode();
        var superAdminAuth = await adminLoginResponse.Content.ReadFromJsonAsync<AuthResponse>(JsonSerializerOptions.Web);
        Assert.NotNull(superAdminAuth);

        var plansResponse = await SendAuthorizedAsync(client, superAdminAuth!.AccessToken, null, HttpMethod.Get, "/api/admin/platform/plans");
        plansResponse.EnsureSuccessStatusCode();
        var plans = await plansResponse.Content.ReadFromJsonAsync<IReadOnlyCollection<PlanDefinitionResponse>>(JsonSerializerOptions.Web);
        var standardPlan = Assert.Single(plans!, item => item.Slug == "standard");

        var hidePlanResponse = await SendAuthorizedAsync(
            client,
            superAdminAuth.AccessToken,
            null,
            HttpMethod.Put,
            $"/api/admin/platform/plans/{standardPlan.Id}",
            JsonContent.Create(new
            {
                monthlyPrice = 9.90m,
                annualPrice = 99.00m,
                maxOwnedSpaces = 1,
                maxCores = 3,
                maxProjects = 3,
                maxInvitedMembers = (int?)null,
                maxOriginalImages = 30,
                showInCatalog = false,
                isPopular = standardPlan.IsPopular
            }));

        hidePlanResponse.EnsureSuccessStatusCode();

        var anonymousPlansResponse = await client.GetAsync("/api/plans");
        anonymousPlansResponse.EnsureSuccessStatusCode();
        var anonymousPlans = await anonymousPlansResponse.Content.ReadFromJsonAsync<IReadOnlyCollection<PlanDefinitionResponse>>(JsonSerializerOptions.Web);
        Assert.NotNull(anonymousPlans);
        Assert.DoesNotContain(anonymousPlans!, item => item.Slug == "standard");

        var createSubscriptionResponse = await SendAuthorizedAsync(
            client,
            superAdminAuth.AccessToken,
            null,
            HttpMethod.Post,
            "/api/admin/platform/subscriptions",
            JsonContent.Create(new
            {
                userId = userAuth!.User.Id,
                planDefinitionId = standardPlan.Id,
                billingCycle = "Monthly",
                startsAt = DateTimeOffset.UtcNow.AddDays(-1),
                endsAt = DateTimeOffset.UtcNow.AddDays(29),
                amountPaid = 9.90m,
                currencyCode = "BRL",
                status = "Active",
                adminNote = (string?)null
            }));

        createSubscriptionResponse.EnsureSuccessStatusCode();

        var userPlansResponse = await SendAuthorizedAsync(client, userAuth.AccessToken, null, HttpMethod.Get, "/api/plans");
        userPlansResponse.EnsureSuccessStatusCode();
        var userPlans = await userPlansResponse.Content.ReadFromJsonAsync<IReadOnlyCollection<PlanDefinitionResponse>>(JsonSerializerOptions.Web);
        Assert.NotNull(userPlans);
        Assert.Contains(userPlans!, item => item.Slug == "standard" && !item.ShowInCatalog);
    }

    [Fact]
    public async Task Superadmin_can_manage_platform_settings()
    {
        await using var factory = new OrganizaClubApiFactory();
        using var client = factory.CreateClient();

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "superadmin@organiza.club",
            password = "super-secret"
        });

        loginResponse.EnsureSuccessStatusCode();
        var superAdminAuth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>(JsonSerializerOptions.Web);
        Assert.NotNull(superAdminAuth);

        var initialResponse = await SendAuthorizedAsync(
            client,
            superAdminAuth.AccessToken,
            null,
            HttpMethod.Get,
            "/api/admin/platform/settings");

        initialResponse.EnsureSuccessStatusCode();
        var initialSettings = await initialResponse.Content.ReadFromJsonAsync<PlatformSettingsResponse>(JsonSerializerOptions.Web);
        Assert.NotNull(initialSettings);
        Assert.False(initialSettings!.CanShowAddressOnLanding);

        var updateResponse = await SendAuthorizedAsync(
            client,
            superAdminAuth.AccessToken,
            null,
            HttpMethod.Put,
            "/api/admin/platform/settings",
            JsonContent.Create(new
            {
                adminName = "Equipe OrganizaClub",
                contactEmail = "contato@organiza.club",
                contactPhone = "(11) 99999-0000",
                managementPhone = "(11) 98888-7777",
                instagram = "@organizaclub",
                addressLine1 = "Rua das Flores, 123",
                addressLine2 = "Sala 21",
                city = "São Paulo",
                state = "SP",
                postalCode = "01310-000"
            }));

        updateResponse.EnsureSuccessStatusCode();
        var updatedSettings = await updateResponse.Content.ReadFromJsonAsync<PlatformSettingsResponse>(JsonSerializerOptions.Web);
        Assert.NotNull(updatedSettings);
        Assert.Equal("Equipe OrganizaClub", updatedSettings!.AdminName);
        Assert.True(updatedSettings.CanShowAddressOnLanding);

        var publicResponse = await client.GetAsync("/api/platform-settings");
        publicResponse.EnsureSuccessStatusCode();
        var publicSettings = await publicResponse.Content.ReadFromJsonAsync<PublicPlatformSettingsResponse>(JsonSerializerOptions.Web);
        Assert.NotNull(publicSettings);
        Assert.Equal("contato@organiza.club", publicSettings!.ContactEmail);
        Assert.True(publicSettings.CanShowAddressOnLanding);
    }

    [Fact]
    public async Task Current_user_plan_and_creation_listing_return_global_usage()
    {
        await using var factory = new OrganizaClubApiFactory();
        using var client = factory.CreateClient();

        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email = "profile-plan@organiza.club",
            password = "profile-secret",
            displayName = "Profile User",
            phoneNumber = (string?)null
        });

        registerResponse.EnsureSuccessStatusCode();
        var auth = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>(JsonSerializerOptions.Web);
        Assert.NotNull(auth);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OrganizaClubDbContext>();
            var space = new Space
            {
                Name = "Espaço Perfil",
                CreatedByUserId = auth!.User.Id
            };
            var membership = new SpaceMember
            {
                Space = space,
                UserId = auth.User.Id,
                Role = SpaceRole.Owner
            };
            var core = new OrganizaClub.Domain.Projects.Core
            {
                Space = space,
                CreatedByMember = membership,
                Name = "Núcleo Perfil"
            };

            db.Spaces.Add(space);
            db.SpaceMembers.Add(membership);
            db.Cores.Add(core);
            db.Projects.Add(new OrganizaClub.Domain.Projects.Project
            {
                SpaceId = space.Id,
                Core = core,
                CreatedByMember = membership,
                Name = "Projeto Perfil"
            });
            await db.SaveChangesAsync();
        }

        var summaryResponse = await SendAuthorizedAsync(client, auth!.AccessToken, null, HttpMethod.Get, "/api/users/me/plan");
        summaryResponse.EnsureSuccessStatusCode();
        var summary = await summaryResponse.Content.ReadFromJsonAsync<CurrentUserPlanSummaryResponse>(JsonSerializerOptions.Web);
        Assert.NotNull(summary);
        Assert.Equal(1, summary!.Usage.OwnedSpaceCount);
        Assert.Equal(1, summary.Usage.CoreCount);
        Assert.Equal(1, summary.Usage.ProjectCount);
        Assert.Equal(0, summary.Usage.InvitedMemberCount);

        var creationsResponse = await SendAuthorizedAsync(
            client,
            auth.AccessToken,
            null,
            HttpMethod.Get,
            "/api/users/me/plan/creations/projects");
        creationsResponse.EnsureSuccessStatusCode();
        var projects = await creationsResponse.Content.ReadFromJsonAsync<IReadOnlyCollection<PlanCreationItemResponse>>(JsonSerializerOptions.Web);
        var project = Assert.Single(projects!);
        Assert.Equal("Projeto Perfil", project.Name);
        Assert.Equal("Espaço Perfil", project.SpaceName);
        Assert.True(project.CanDelete);
    }

    private static async Task<SeedUserResult> SeedUserAsync(OrganizaClubApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OrganizaClubDbContext>();

        var user = new AppUser
        {
            Email = "standard@organiza.club",
            PasswordHash = "hash",
            DisplayName = "Standard User",
            SystemRole = SystemRole.User
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        return new SeedUserResult(user.Id);
    }

    private static async Task<HttpResponseMessage> SendAuthorizedAsync(
        HttpClient client,
        string accessToken,
        Guid? spaceId,
        HttpMethod method,
        string path,
        HttpContent? content = null)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        if (spaceId.HasValue)
        {
            request.Headers.Add("X-Space-Id", spaceId.Value.ToString());
        }

        request.Content = content;
        return await client.SendAsync(request);
    }

    private sealed record SeedUserResult(Guid UserId);

    private sealed record AuthResponse(string AccessToken, AuthUserResponse User);
    private sealed record AuthUserResponse(Guid Id, string Email, string DisplayName, string SystemRole);
    private sealed record ProblemDetailsResponse(string? Detail);
    private sealed record PlanDefinitionResponse(Guid Id, string Slug, string Name, bool IsPopular, bool ShowInCatalog);
    private sealed record AdminUserListItemResponse(Guid Id, string EffectivePlanName, string? ActiveSubscriptionStatus);
    private sealed record CurrentUserPlanSummaryResponse(PlanUsageSummaryResponse Usage);
    private sealed record PlanUsageSummaryResponse(
        int OwnedSpaceCount,
        int CoreCount,
        int ProjectCount,
        int InvitedMemberCount,
        int ManagedOriginalImageCount);
    private sealed record PlanCreationItemResponse(Guid Id, string Name, Guid SpaceId, string SpaceName, bool CanDelete);
    private sealed record PlatformSettingsResponse(
        string AdminName,
        string ContactEmail,
        string ContactPhone,
        string ManagementPhone,
        string Instagram,
        string AddressLine1,
        string AddressLine2,
        string City,
        string State,
        string PostalCode,
        bool CanShowAddressOnLanding);
    private sealed record PublicPlatformSettingsResponse(
        string ContactEmail,
        string ContactPhone,
        string Instagram,
        string AddressLine1,
        string AddressLine2,
        string City,
        string State,
        string PostalCode,
        bool CanShowAddressOnLanding);

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
