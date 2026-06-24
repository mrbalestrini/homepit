using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using HomePit.Application.Auth;
using HomePit.Application.Common;
using HomePit.Application.Storage;
using HomePit.Domain.Households;
using HomePit.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace HomePit.IntegrationTests;

public sealed class PromptEndpointsTests
{
    [Fact]
    public async Task Prompt_crud_and_filters_support_multiple_categories_and_without_universe()
    {
        await using var factory = new HomePitApiFactory();
        using var client = factory.CreateClient();
        var seed = await SeedHouseholdAsync(factory);

        var categoryA = await CreateCategoryAsync(client, seed, "Categoria A");
        var categoryB = await CreateCategoryAsync(client, seed, "Categoria B");
        var universe = await CreateUniverseAsync(client, seed, "Universo Criativo", "https://cdn.homepit.dev/universo-criativo.png");

        var promptWithUniverse = await CreatePromptAsync(client, seed, new
        {
            universeId = universe.Id,
            title = "Prompt Universo",
            description = "Com universo",
            promptText = "Texto A",
            categoryIds = new[] { categoryA.Id.ToString() },
            linkUrl = (string?)null,
            linkTitle = (string?)null
        });

        var promptWithoutUniverse = await CreatePromptAsync(client, seed, new
        {
            universeId = (string?)null,
            title = "Prompt Solto",
            description = "Sem universo",
            promptText = "Texto B",
            categoryIds = new[] { categoryB.Id.ToString() },
            linkUrl = (string?)null,
            linkTitle = (string?)null
        });

        await CreatePromptAsync(client, seed, new
        {
            universeId = universe.Id,
            title = "Prompt Misto",
            description = "Duas categorias",
            promptText = "Texto C",
            categoryIds = new[] { categoryA.Id.ToString(), categoryB.Id.ToString() },
            linkUrl = (string?)null,
            linkTitle = (string?)null
        });

        var defaultListResponse = await SendAuthorizedAsync(
            client,
            seed.AccessToken,
            seed.HouseholdId,
            HttpMethod.Get,
            "/api/prompts?page=1&pageSize=12");

        defaultListResponse.EnsureSuccessStatusCode();
        var defaultList = await defaultListResponse.Content.ReadFromJsonAsync<PromptListResponse>(JsonSerializerOptions.Web);

        Assert.NotNull(defaultList);
        Assert.Equal(3, defaultList.TotalCount);
        Assert.Contains(defaultList.Items, item => item.Id == promptWithUniverse.Id && item.UniverseImageUrl == universe.ImageUrl);

        var allCategoriesResponse = await SendAuthorizedAsync(
            client,
            seed.AccessToken,
            seed.HouseholdId,
            HttpMethod.Get,
            $"/api/prompts?categoryId={categoryA.Id}&categoryId={categoryB.Id}");

        allCategoriesResponse.EnsureSuccessStatusCode();
        var filtered = await allCategoriesResponse.Content.ReadFromJsonAsync<PromptListResponse>(JsonSerializerOptions.Web);

        Assert.NotNull(filtered);
        Assert.Equal(3, filtered.TotalCount);

        var categoriesResponse = await SendAuthorizedAsync(
            client,
            seed.AccessToken,
            seed.HouseholdId,
            HttpMethod.Get,
            "/api/prompt-categories");

        categoriesResponse.EnsureSuccessStatusCode();
        var categories = await categoriesResponse.Content.ReadFromJsonAsync<IReadOnlyCollection<PromptCategoryResponse>>(JsonSerializerOptions.Web);

        Assert.NotNull(categories);
        var listedCategoryA = Assert.Single(categories, category => category.Id == categoryA.Id);
        var listedCategoryB = Assert.Single(categories, category => category.Id == categoryB.Id);
        Assert.Equal(2, listedCategoryA.UsageCount);
        Assert.Equal(1, listedCategoryA.ReplacementRequiredCount);
        Assert.Equal(2, listedCategoryB.UsageCount);
        Assert.Equal(1, listedCategoryB.ReplacementRequiredCount);

        var withoutUniverseResponse = await SendAuthorizedAsync(
            client,
            seed.AccessToken,
            seed.HouseholdId,
            HttpMethod.Get,
            "/api/prompts?withoutUniverse=true");

        withoutUniverseResponse.EnsureSuccessStatusCode();
        var withoutUniverse = await withoutUniverseResponse.Content.ReadFromJsonAsync<PromptListResponse>(JsonSerializerOptions.Web);

        Assert.NotNull(withoutUniverse);
        var onlyItem = Assert.Single(withoutUniverse.Items);
        Assert.Equal(promptWithoutUniverse.Id, onlyItem.Id);
        Assert.Null(onlyItem.UniverseId);

        var detailResponse = await SendAuthorizedAsync(
            client,
            seed.AccessToken,
            seed.HouseholdId,
            HttpMethod.Get,
            $"/api/prompts/{promptWithUniverse.Id}");
        detailResponse.EnsureSuccessStatusCode();
        var detailPrompt = await detailResponse.Content.ReadFromJsonAsync<PromptDetailResponse>(JsonSerializerOptions.Web);
        Assert.NotNull(detailPrompt);
        Assert.Equal(universe.ImageUrl, detailPrompt.UniverseImageUrl);

        var updatedResponse = await SendAuthorizedAsync(
            client,
            seed.AccessToken,
            seed.HouseholdId,
            HttpMethod.Put,
            $"/api/prompts/{promptWithUniverse.Id}",
            JsonContent.Create(new
            {
                universeId = universe.Id,
                title = "Prompt Universo Atualizado",
                description = "Descrição atualizada",
                promptText = "Texto A2",
                categoryIds = new[] { categoryA.Id.ToString(), categoryB.Id.ToString() },
                linkUrl = "https://homepit.dev/ref",
                linkTitle = "Referência"
            }));

        updatedResponse.EnsureSuccessStatusCode();
        var updatedPrompt = await updatedResponse.Content.ReadFromJsonAsync<PromptDetailResponse>(JsonSerializerOptions.Web);
        Assert.NotNull(updatedPrompt);
        Assert.Equal("Prompt Universo Atualizado", updatedPrompt.Title);
        Assert.Equal(2, updatedPrompt.Categories.Count);

        var deleteResponse = await SendAuthorizedAsync(
            client,
            seed.AccessToken,
            seed.HouseholdId,
            HttpMethod.Delete,
            $"/api/prompts/{promptWithoutUniverse.Id}");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task Prompt_archiving_is_reversible_and_filtered_by_state()
    {
        await using var factory = new HomePitApiFactory();
        using var client = factory.CreateClient();
        var seed = await SeedHouseholdAsync(factory);

        var categoryA = await CreateCategoryAsync(client, seed, "Categoria A");
        var categoryB = await CreateCategoryAsync(client, seed, "Categoria B");
        var prompt = await CreatePromptAsync(client, seed, new
        {
            universeId = (string?)null,
            title = "Prompt arquivável",
            description = "Guardado para referência",
            promptText = "Texto arquivável",
            categoryIds = new[] { categoryA.Id.ToString(), categoryB.Id.ToString() },
            linkUrl = (string?)null,
            linkTitle = (string?)null
        });

        using (var uploadRequest = CreateAuthorizedRequest(seed.AccessToken, seed.HouseholdId, HttpMethod.Post, $"/api/prompts/{prompt.Id}/image"))
        {
            using var form = new MultipartFormDataContent();
            using var file = new ByteArrayContent([9, 8, 7, 6]);
            file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            form.Add(file, "file", "prompt.png");
            uploadRequest.Content = form;

            var uploadResponse = await client.SendAsync(uploadRequest);
            uploadResponse.EnsureSuccessStatusCode();
        }

        var archiveResponse = await SendAuthorizedAsync(
            client,
            seed.AccessToken,
            seed.HouseholdId,
            HttpMethod.Post,
            $"/api/prompts/{prompt.Id}/archive");

        archiveResponse.EnsureSuccessStatusCode();
        var archivedPrompt = await archiveResponse.Content.ReadFromJsonAsync<PromptDetailResponse>(JsonSerializerOptions.Web);

        Assert.NotNull(archivedPrompt);
        Assert.True(archivedPrompt.IsArchived);
        Assert.True(archivedPrompt.HasImage);
        Assert.Equal(2, archivedPrompt.Categories.Count);

        var activeListResponse = await SendAuthorizedAsync(
            client,
            seed.AccessToken,
            seed.HouseholdId,
            HttpMethod.Get,
            "/api/prompts?page=1&pageSize=12");

        activeListResponse.EnsureSuccessStatusCode();
        var activeList = await activeListResponse.Content.ReadFromJsonAsync<PromptListResponse>(JsonSerializerOptions.Web);

        Assert.NotNull(activeList);
        Assert.DoesNotContain(activeList.Items, item => item.Id == prompt.Id);

        var archivedListResponse = await SendAuthorizedAsync(
            client,
            seed.AccessToken,
            seed.HouseholdId,
            HttpMethod.Get,
            "/api/prompts?archivedOnly=true&page=1&pageSize=12");

        archivedListResponse.EnsureSuccessStatusCode();
        var archivedList = await archivedListResponse.Content.ReadFromJsonAsync<PromptListResponse>(JsonSerializerOptions.Web);

        Assert.NotNull(archivedList);
        var archivedItem = Assert.Single(archivedList.Items, item => item.Id == prompt.Id);
        Assert.True(archivedItem.IsArchived);

        var unarchiveResponse = await SendAuthorizedAsync(
            client,
            seed.AccessToken,
            seed.HouseholdId,
            HttpMethod.Delete,
            $"/api/prompts/{prompt.Id}/archive");

        unarchiveResponse.EnsureSuccessStatusCode();
        var unarchivedPrompt = await unarchiveResponse.Content.ReadFromJsonAsync<PromptDetailResponse>(JsonSerializerOptions.Web);

        Assert.NotNull(unarchivedPrompt);
        Assert.False(unarchivedPrompt.IsArchived);
        Assert.True(unarchivedPrompt.HasImage);
        Assert.Equal(2, unarchivedPrompt.Categories.Count);

        var activeAfterRestoreResponse = await SendAuthorizedAsync(
            client,
            seed.AccessToken,
            seed.HouseholdId,
            HttpMethod.Get,
            "/api/prompts?page=1&pageSize=12");

        activeAfterRestoreResponse.EnsureSuccessStatusCode();
        var activeAfterRestore = await activeAfterRestoreResponse.Content.ReadFromJsonAsync<PromptListResponse>(JsonSerializerOptions.Web);

        Assert.NotNull(activeAfterRestore);
        Assert.Contains(activeAfterRestore.Items, item => item.Id == prompt.Id && !item.IsArchived);
    }

    [Fact]
    public async Task Delete_universe_preserves_prompt_and_clears_universe_reference()
    {
        await using var factory = new HomePitApiFactory();
        using var client = factory.CreateClient();
        var seed = await SeedHouseholdAsync(factory);

        var category = await CreateCategoryAsync(client, seed, "Categoria");
        var universe = await CreateUniverseAsync(client, seed, "Universo");
        var prompt = await CreatePromptAsync(client, seed, new
        {
            universeId = universe.Id,
            title = "Prompt com universo",
            description = (string?)null,
            promptText = "Conteúdo",
            categoryIds = new[] { category.Id.ToString() },
            linkUrl = (string?)null,
            linkTitle = (string?)null
        });

        var deleteUniverseResponse = await SendAuthorizedAsync(
            client,
            seed.AccessToken,
            seed.HouseholdId,
            HttpMethod.Delete,
            $"/api/universes/{universe.Id}");

        Assert.Equal(HttpStatusCode.NoContent, deleteUniverseResponse.StatusCode);

        var promptResponse = await SendAuthorizedAsync(
            client,
            seed.AccessToken,
            seed.HouseholdId,
            HttpMethod.Get,
            $"/api/prompts/{prompt.Id}");

        promptResponse.EnsureSuccessStatusCode();
        var detail = await promptResponse.Content.ReadFromJsonAsync<PromptDetailResponse>(JsonSerializerOptions.Web);
        Assert.NotNull(detail);
        Assert.Null(detail.UniverseId);
        Assert.Null(detail.UniverseName);
    }

    [Fact]
    public async Task Prompt_image_upload_get_and_delete_work()
    {
        await using var factory = new HomePitApiFactory();
        using var client = factory.CreateClient();
        var seed = await SeedHouseholdAsync(factory);

        var category = await CreateCategoryAsync(client, seed, "Categoria");
        var prompt = await CreatePromptAsync(client, seed, new
        {
            universeId = (string?)null,
            title = "Prompt com imagem",
            description = (string?)null,
            promptText = "Conteúdo",
            categoryIds = new[] { category.Id.ToString() },
            linkUrl = (string?)null,
            linkTitle = (string?)null
        });

        using var uploadRequest = CreateAuthorizedRequest(seed.AccessToken, seed.HouseholdId, HttpMethod.Post, $"/api/prompts/{prompt.Id}/image");
        using var form = new MultipartFormDataContent();
        using var file = new ByteArrayContent([9, 8, 7, 6]);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(file, "file", "prompt.png");
        uploadRequest.Content = form;

        var uploadResponse = await client.SendAsync(uploadRequest);
        uploadResponse.EnsureSuccessStatusCode();
        var updatedPrompt = await uploadResponse.Content.ReadFromJsonAsync<PromptDetailResponse>(JsonSerializerOptions.Web);
        Assert.NotNull(updatedPrompt);
        Assert.True(updatedPrompt.HasImage);

        var getResponse = await SendAuthorizedAsync(
            client,
            seed.AccessToken,
            seed.HouseholdId,
            HttpMethod.Get,
            $"/api/prompts/{prompt.Id}/image");

        getResponse.EnsureSuccessStatusCode();
        Assert.Equal("image/png", getResponse.Content.Headers.ContentType?.MediaType);
        Assert.Equal([9, 8, 7, 6], await getResponse.Content.ReadAsByteArrayAsync());

        var deleteResponse = await SendAuthorizedAsync(
            client,
            seed.AccessToken,
            seed.HouseholdId,
            HttpMethod.Delete,
            $"/api/prompts/{prompt.Id}/image");

        deleteResponse.EnsureSuccessStatusCode();
        var promptWithoutImage = await deleteResponse.Content.ReadFromJsonAsync<PromptDetailResponse>(JsonSerializerOptions.Web);
        Assert.NotNull(promptWithoutImage);
        Assert.False(promptWithoutImage.HasImage);
    }

    [Fact]
    public async Task Activity_creation_respects_selected_status()
    {
        await using var factory = new HomePitApiFactory();
        using var client = factory.CreateClient();
        var seed = await SeedHouseholdAsync(factory);

        var universe = await CreateUniverseAsync(client, seed, "Universo Operacional");
        var project = await CreateProjectAsync(client, seed, universe.Id, "Projeto com status");

        var response = await SendAuthorizedAsync(
            client,
            seed.AccessToken,
            seed.HouseholdId,
            HttpMethod.Post,
            "/api/activities",
            JsonContent.Create(new
            {
                projectId = project.Id,
                title = "Atividade concluída na criação",
                description = "Criada já finalizada",
                dueDate = new DateOnly(2026, 6, 30),
                status = "Concluido",
                priority = "Alta",
                size = 2
            }));

        response.EnsureSuccessStatusCode();
        var activity = await response.Content.ReadFromJsonAsync<ActivityResponse>(JsonSerializerOptions.Web);

        Assert.NotNull(activity);
        Assert.Equal(project.Id, activity.ProjectId);
        Assert.Equal("Concluido", activity.Status);
        Assert.Equal(new DateOnly(2026, 6, 30), activity.DueDate);
        Assert.NotEqual(default, activity.CreatedAt);

        var updateResponse = await SendAuthorizedAsync(
            client,
            seed.AccessToken,
            seed.HouseholdId,
            HttpMethod.Put,
            $"/api/activities/{activity.Id}",
            JsonContent.Create(new
            {
                projectId = project.Id,
                title = "Atividade atualizada",
                description = "Agora com outro prazo",
                dueDate = new DateOnly(2026, 7, 5),
                status = "EmAndamento",
                priority = "Urgente",
                size = 3,
                responsibleMemberId = (Guid?)null
            }));

        updateResponse.EnsureSuccessStatusCode();
        var updatedActivity = await updateResponse.Content.ReadFromJsonAsync<ActivityResponse>(JsonSerializerOptions.Web);

        Assert.NotNull(updatedActivity);
        Assert.Equal(activity.CreatedAt, updatedActivity.CreatedAt);
        Assert.Equal(new DateOnly(2026, 7, 5), updatedActivity.DueDate);
        Assert.Equal("EmAndamento", updatedActivity.Status);
    }

    private static async Task<SeedResult> SeedHouseholdAsync(HomePitApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HomePitDbContext>();
        var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();

        var user = new AppUser
        {
            Email = $"owner-{Guid.NewGuid():N}@homepit.dev",
            PasswordHash = "hash",
            DisplayName = "Pessoa Teste",
            SystemRole = SystemRole.User
        };
        var household = new Household
        {
            Name = "Casa Integração"
        };
        var member = new HouseholdMember
        {
            Household = household,
            User = user,
            Role = HouseholdRole.Owner
        };

        db.Users.Add(user);
        db.Households.Add(household);
        db.HouseholdMembers.Add(member);
        await db.SaveChangesAsync();

        var accessToken = tokenService.CreateAccessToken(user, new[] { member });
        return new SeedResult(accessToken, household.Id);
    }

    private static async Task<CategoryResponse> CreateCategoryAsync(HttpClient client, SeedResult seed, string name)
    {
        var response = await SendAuthorizedAsync(
            client,
            seed.AccessToken,
            seed.HouseholdId,
            HttpMethod.Post,
            "/api/prompt-categories",
            JsonContent.Create(new { name }));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CategoryResponse>(JsonSerializerOptions.Web))!;
    }

    private static async Task<UniverseResponse> CreateUniverseAsync(HttpClient client, SeedResult seed, string name, string? imageUrl = null)
    {
        var response = await SendAuthorizedAsync(
            client,
            seed.AccessToken,
            seed.HouseholdId,
            HttpMethod.Post,
            "/api/universes",
            JsonContent.Create(new { name, imageUrl }));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<UniverseResponse>(JsonSerializerOptions.Web))!;
    }

    private static async Task<ProjectResponse> CreateProjectAsync(HttpClient client, SeedResult seed, Guid universeId, string name)
    {
        var response = await SendAuthorizedAsync(
            client,
            seed.AccessToken,
            seed.HouseholdId,
            HttpMethod.Post,
            "/api/projects",
            JsonContent.Create(new { universeId, name }));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProjectResponse>(JsonSerializerOptions.Web))!;
    }

    private static async Task<PromptDetailResponse> CreatePromptAsync(HttpClient client, SeedResult seed, object body)
    {
        var response = await SendAuthorizedAsync(
            client,
            seed.AccessToken,
            seed.HouseholdId,
            HttpMethod.Post,
            "/api/prompts",
            JsonContent.Create(body));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PromptDetailResponse>(JsonSerializerOptions.Web))!;
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

    private sealed record SeedResult(string AccessToken, Guid HouseholdId);

    private sealed record UniverseResponse(Guid Id, string Name, string? ImageUrl);

    private sealed record ProjectResponse(Guid Id, Guid UniverseId, string Name);

    private sealed record CategoryResponse(Guid Id, string Name);

    private sealed record ActivityResponse(Guid Id, Guid ProjectId, string Title, string Status, DateTimeOffset CreatedAt, DateOnly? DueDate);

    private sealed record PromptCategoryResponse(Guid Id, string Name, int UsageCount, int ReplacementRequiredCount);

    private sealed record PromptCategoryReferenceResponse(Guid Id, string Name);

    private sealed record PromptListResponse(IReadOnlyCollection<PromptListItemResponse> Items, int Page, int PageSize, int TotalCount);

    private sealed record PromptListItemResponse(Guid Id, Guid? UniverseId, string? UniverseName, string? UniverseImageUrl, string Title, bool IsArchived);

    private sealed record PromptDetailResponse(
        Guid Id,
        Guid? UniverseId,
        string? UniverseName,
        string? UniverseImageUrl,
        string Title,
        bool IsArchived,
        IReadOnlyCollection<PromptCategoryReferenceResponse> Categories,
        bool HasImage);

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
                    ["ObjectStorage:CreateBucketOnStartup"] = "true"
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
        private readonly Dictionary<string, StoredObject> objects = [];

        public Task EnsureBucketExistsAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<StoredObject> GetAsync(string objectKey, CancellationToken cancellationToken)
        {
            if (!objects.TryGetValue(objectKey, out var storedObject))
            {
                throw new NotFoundException("Arquivo não encontrado.");
            }

            return Task.FromResult(storedObject);
        }

        public async Task PutAsync(ObjectStoragePutRequest request, CancellationToken cancellationToken)
        {
            using var buffer = new MemoryStream();
            await request.Content.CopyToAsync(buffer, cancellationToken);
            objects[request.ObjectKey] = new StoredObject(request.ObjectKey, buffer.ToArray(), request.ContentType);
        }

        public Task DeleteAsync(string objectKey, CancellationToken cancellationToken)
        {
            objects.Remove(objectKey);
            return Task.CompletedTask;
        }
    }
}
