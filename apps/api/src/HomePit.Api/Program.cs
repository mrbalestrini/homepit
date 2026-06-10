using System.Text;
using System.Text.Json.Serialization;
using HomePit.Api.Security;
using HomePit.Application;
using HomePit.Application.Auth;
using HomePit.Application.Common;
using HomePit.Application.Households;
using HomePit.Application.Prompts;
using HomePit.Application.Projects;
using HomePit.Infrastructure;
using HomePit.Infrastructure.Auth;
using HomePit.Infrastructure.Data;
using HomePit.Infrastructure.ObjectStorage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUserContext, HttpUserContext>();
builder.Services.AddHomePitApplication();
builder.Services.AddHomePitInfrastructure(builder.Configuration);

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddCors(options =>
{
    options.AddPolicy("web", policy =>
    {
        var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        if (origins.Length == 0)
        {
            policy.AllowAnyOrigin();
        }
        else
        {
            policy.WithOrigins(origins);
        }

        policy.AllowAnyHeader().AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseHomePitErrors();
app.UseCors("web");
app.UseAuthentication();
app.UseAuthorization();

var applyMigrationsOnStartup = app.Configuration.GetValue("Database:ApplyMigrationsOnStartup", true);
if (applyMigrationsOnStartup)
{
    await app.Services.MigrateHomePitDatabaseAsync();
}
else
{
    await app.Services.EnsureNoPendingHomePitMigrationsAsync();
}
await app.Services.EnsureHomePitObjectStorageAsync();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/api/system/info", () => Results.Ok(new
{
    name = "HomePit API",
    version = "0.1.0",
    timezone = "America/Sao_Paulo"
}));

var auth = app.MapGroup("/api/auth");
auth.MapPost("/register", async (RegisterRequest request, AuthService service, CancellationToken cancellationToken) =>
    Results.Created("/api/households", await service.RegisterAsync(request, cancellationToken)));
auth.MapPost("/login", async (LoginRequest request, AuthService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.LoginAsync(request, cancellationToken)));
auth.MapPost("/refresh", async (RefreshRequest request, AuthService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.RefreshAsync(request, cancellationToken)));

var api = app.MapGroup("/api").RequireAuthorization();

api.MapGet("/households", async (HouseholdService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.ListAsync(cancellationToken)));
api.MapPost("/households", async (CreateHouseholdRequest request, HouseholdService service, CancellationToken cancellationToken) =>
    Results.Created("/api/households", await service.CreateAsync(request, cancellationToken)));
api.MapPut("/households/{id:guid}", async (
    Guid id,
    UpdateHouseholdRequest request,
    HouseholdService service,
    CancellationToken cancellationToken) =>
        Results.Ok(await service.UpdateAsync(id, request, cancellationToken)));
api.MapDelete("/households/{id:guid}", async (
    Guid id,
    HouseholdService service,
    CancellationToken cancellationToken) =>
{
    await service.DeleteAsync(id, cancellationToken);
    return Results.NoContent();
});
api.MapGet("/households/members", async (HouseholdService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.ListMembersAsync(cancellationToken)));
api.MapPost("/households/share", async (ShareHouseholdRequest request, HouseholdService service, CancellationToken cancellationToken) =>
    Results.Created("/api/households/members", await service.ShareAsync(request, cancellationToken)));
api.MapPut("/households/members/{id:guid}", async (
    Guid id,
    UpdateHouseholdMemberRequest request,
    HouseholdService service,
    CancellationToken cancellationToken) =>
        Results.Ok(await service.UpdateMemberAsync(id, request, cancellationToken)));
api.MapDelete("/households/members/{id:guid}", async (
    Guid id,
    HouseholdService service,
    CancellationToken cancellationToken) =>
{
    await service.RemoveMemberAsync(id, cancellationToken);
    return Results.NoContent();
});
api.MapPut("/users/me", async (UpdateProfileRequest request, AuthService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.UpdateProfileAsync(request, cancellationToken)));
api.MapPost("/users/me/profile-photo", async (
    HttpRequest request,
    AuthService service,
    CancellationToken cancellationToken) =>
{
    if (!request.HasFormContentType)
    {
        throw new ValidationException("Envie a foto de perfil em multipart/form-data.");
    }

    var form = await request.ReadFormAsync(cancellationToken);
    var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
    if (file is null)
    {
        throw new ValidationException("Selecione uma imagem para a foto de perfil.");
    }

    await using var stream = file.OpenReadStream();
    return Results.Ok(await service.UploadProfilePhotoAsync(stream, file.Length, file.ContentType, cancellationToken));
});
api.MapGet("/users/me/profile-photo", async (
    HttpContext context,
    AuthService service,
    CancellationToken cancellationToken) =>
{
    var photo = await service.GetProfilePhotoAsync(cancellationToken);
    context.Response.Headers.CacheControl = "no-store";
    return Results.File(photo.Content, photo.ContentType);
});

api.MapGet("/universes", async (ProjectService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.ListUniversesAsync(cancellationToken)));
api.MapPost("/universes", async (CreateUniverseRequest request, ProjectService service, CancellationToken cancellationToken) =>
    Results.Created("/api/universes", await service.CreateUniverseAsync(request, cancellationToken)));
api.MapPut("/universes/{id:guid}", async (
    Guid id,
    UpdateUniverseRequest request,
    ProjectService service,
    CancellationToken cancellationToken) =>
        Results.Ok(await service.UpdateUniverseAsync(id, request, cancellationToken)));
api.MapDelete("/universes/{id:guid}", async (
    Guid id,
    ProjectService service,
    CancellationToken cancellationToken) =>
{
    await service.DeleteUniverseAsync(id, cancellationToken);
    return Results.NoContent();
});
api.MapPost("/universes/{id:guid}/image", async (
    Guid id,
    HttpRequest request,
    ProjectService service,
    CancellationToken cancellationToken) =>
{
    if (!request.HasFormContentType)
    {
        throw new ValidationException("Envie a imagem do universo em multipart/form-data.");
    }

    var form = await request.ReadFormAsync(cancellationToken);
    var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
    if (file is null)
    {
        throw new ValidationException("Selecione uma imagem para o universo.");
    }

    await using var stream = file.OpenReadStream();
    return Results.Ok(await service.UploadUniverseImageAsync(id, stream, file.Length, file.ContentType, cancellationToken));
});
api.MapGet("/universes/{id:guid}/image", async (
    Guid id,
    HttpContext context,
    ProjectService service,
    CancellationToken cancellationToken) =>
{
    var image = await service.GetUniverseImageAsync(id, cancellationToken);
    context.Response.Headers.CacheControl = "no-store";
    return Results.File(image.Content, image.ContentType);
});
api.MapDelete("/universes/{id:guid}/image", async (
    Guid id,
    ProjectService service,
    CancellationToken cancellationToken) =>
        Results.Ok(await service.DeleteUniverseImageAsync(id, cancellationToken)));

api.MapGet("/projects", async (Guid? universeId, ProjectService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.ListProjectsAsync(universeId, cancellationToken)));
api.MapPost("/projects", async (CreateProjectRequest request, ProjectService service, CancellationToken cancellationToken) =>
    Results.Created("/api/projects", await service.CreateProjectAsync(request, cancellationToken)));
api.MapPut("/projects/{id:guid}", async (
    Guid id,
    UpdateProjectRequest request,
    ProjectService service,
    CancellationToken cancellationToken) =>
        Results.Ok(await service.UpdateProjectAsync(id, request, cancellationToken)));
api.MapDelete("/projects/{id:guid}", async (
    Guid id,
    ProjectService service,
    CancellationToken cancellationToken) =>
{
    await service.DeleteProjectAsync(id, cancellationToken);
    return Results.NoContent();
});

api.MapGet("/activities", async (
    Guid? projectId,
    HomePit.Domain.Projects.ActivityStatus? status,
    ProjectService service,
    CancellationToken cancellationToken) =>
        Results.Ok(await service.ListActivitiesAsync(projectId, status, cancellationToken)));
api.MapPost("/activities", async (CreateActivityRequest request, ProjectService service, CancellationToken cancellationToken) =>
    Results.Created("/api/activities", await service.CreateActivityAsync(request, cancellationToken)));
api.MapPut("/activities/{id:guid}", async (
    Guid id,
    UpdateActivityRequest request,
    ProjectService service,
    CancellationToken cancellationToken) =>
        Results.Ok(await service.UpdateActivityAsync(id, request, cancellationToken)));
api.MapDelete("/activities/{id:guid}", async (
    Guid id,
    ProjectService service,
    CancellationToken cancellationToken) =>
{
    await service.DeleteActivityAsync(id, cancellationToken);
    return Results.NoContent();
});
api.MapPatch("/activities/{id:guid}/status", async (
    Guid id,
    UpdateActivityStatusRequest request,
    ProjectService service,
    CancellationToken cancellationToken) =>
        Results.Ok(await service.UpdateActivityStatusAsync(id, request, cancellationToken)));
api.MapGet("/activities/{id:guid}/comments", async (
    Guid id,
    ProjectService service,
    CancellationToken cancellationToken) =>
        Results.Ok(await service.ListActivityCommentsAsync(id, cancellationToken)));
api.MapPost("/activities/{id:guid}/comments", async (
    Guid id,
    CreateActivityCommentRequest request,
    ProjectService service,
    CancellationToken cancellationToken) =>
        Results.Created($"/api/activities/{id}/comments", await service.CreateActivityCommentAsync(id, request, cancellationToken)));
api.MapPut("/activities/{activityId:guid}/comments/{commentId:guid}", async (
    Guid activityId,
    Guid commentId,
    UpdateActivityCommentRequest request,
    ProjectService service,
    CancellationToken cancellationToken) =>
        Results.Ok(await service.UpdateActivityCommentAsync(activityId, commentId, request, cancellationToken)));
api.MapDelete("/activities/{activityId:guid}/comments/{commentId:guid}", async (
    Guid activityId,
    Guid commentId,
    ProjectService service,
    CancellationToken cancellationToken) =>
{
    await service.DeleteActivityCommentAsync(activityId, commentId, cancellationToken);
    return Results.NoContent();
});

api.MapGet("/activities/{id:guid}/pending-items", async (
    Guid id,
    ProjectService service,
    CancellationToken cancellationToken) =>
        Results.Ok(await service.ListPendingItemsAsync(id, cancellationToken)));
api.MapPost("/activities/{id:guid}/pending-items", async (
    Guid id,
    CreatePendingItemRequest request,
    ProjectService service,
    CancellationToken cancellationToken) =>
        Results.Created($"/api/activities/{id}/pending-items", await service.CreatePendingItemAsync(id, request, cancellationToken)));

api.MapGet("/prompts", async (
    HttpRequest request,
    PromptService service,
    CancellationToken cancellationToken) =>
        Results.Ok(await service.ListPromptsAsync(
            ReadOptionalQueryString(request, "search"),
            ReadOptionalGuidQuery(request, "universeId"),
            ReadOptionalBoolQuery(request, "withoutUniverse") ?? false,
            ReadGuidCollectionQuery(request, "categoryId"),
            ReadOptionalIntQuery(request, "page") ?? 1,
            ReadOptionalIntQuery(request, "pageSize") ?? 12,
            cancellationToken)));
api.MapGet("/prompts/{id:guid}", async (Guid id, PromptService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.GetPromptAsync(id, cancellationToken)));
api.MapPost("/prompts", async (CreatePromptRequest request, PromptService service, CancellationToken cancellationToken) =>
    Results.Created("/api/prompts", await service.CreatePromptAsync(request, cancellationToken)));
api.MapPut("/prompts/{id:guid}", async (
    Guid id,
    UpdatePromptRequest request,
    PromptService service,
    CancellationToken cancellationToken) =>
        Results.Ok(await service.UpdatePromptAsync(id, request, cancellationToken)));
api.MapDelete("/prompts/{id:guid}", async (
    Guid id,
    PromptService service,
    CancellationToken cancellationToken) =>
{
    await service.DeletePromptAsync(id, cancellationToken);
    return Results.NoContent();
});
api.MapPost("/prompts/{id:guid}/image", async (
    Guid id,
    HttpRequest request,
    PromptService service,
    CancellationToken cancellationToken) =>
{
    if (!request.HasFormContentType)
    {
        throw new ValidationException("Envie a imagem do prompt em multipart/form-data.");
    }

    var form = await request.ReadFormAsync(cancellationToken);
    var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
    if (file is null)
    {
        throw new ValidationException("Selecione uma imagem para o prompt.");
    }

    await using var stream = file.OpenReadStream();
    return Results.Ok(await service.UploadPromptImageAsync(id, stream, file.Length, file.ContentType, cancellationToken));
});
api.MapGet("/prompts/{id:guid}/image", async (
    Guid id,
    HttpContext context,
    PromptService service,
    CancellationToken cancellationToken) =>
{
    var image = await service.GetPromptImageAsync(id, cancellationToken);
    context.Response.Headers.CacheControl = "no-store";
    return Results.File(image.Content, image.ContentType);
});
api.MapDelete("/prompts/{id:guid}/image", async (
    Guid id,
    PromptService service,
    CancellationToken cancellationToken) =>
        Results.Ok(await service.DeletePromptImageAsync(id, cancellationToken)));

api.MapGet("/prompt-categories", async (PromptService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.ListCategoriesAsync(cancellationToken)));
api.MapPost("/prompt-categories", async (CreatePromptCategoryRequest request, PromptService service, CancellationToken cancellationToken) =>
    Results.Created("/api/prompt-categories", await service.CreateCategoryAsync(request, cancellationToken)));
api.MapPut("/prompt-categories/{id:guid}", async (
    Guid id,
    UpdatePromptCategoryRequest request,
    PromptService service,
    CancellationToken cancellationToken) =>
        Results.Ok(await service.UpdateCategoryAsync(id, request, cancellationToken)));
api.MapDelete("/prompt-categories/{id:guid}", async (
    Guid id,
    Guid? replacementCategoryId,
    PromptService service,
    CancellationToken cancellationToken) =>
{
    await service.DeleteCategoryAsync(id, replacementCategoryId, cancellationToken);
    return Results.NoContent();
});

await app.RunAsync();

static string? ReadOptionalQueryString(HttpRequest request, string key)
{
    return request.Query.TryGetValue(key, out var values)
        ? string.IsNullOrWhiteSpace(values.ToString()) ? null : values.ToString()
        : null;
}

static Guid? ReadOptionalGuidQuery(HttpRequest request, string key)
{
    var rawValue = ReadOptionalQueryString(request, key);
    if (rawValue is null)
    {
        return null;
    }

    if (Guid.TryParse(rawValue, out var guid))
    {
        return guid;
    }

    throw new ValidationException($"O parâmetro '{key}' deve ser um GUID válido.");
}

static bool? ReadOptionalBoolQuery(HttpRequest request, string key)
{
    var rawValue = ReadOptionalQueryString(request, key);
    if (rawValue is null)
    {
        return null;
    }

    if (bool.TryParse(rawValue, out var value))
    {
        return value;
    }

    throw new ValidationException($"O parâmetro '{key}' deve ser verdadeiro ou falso.");
}

static int? ReadOptionalIntQuery(HttpRequest request, string key)
{
    var rawValue = ReadOptionalQueryString(request, key);
    if (rawValue is null)
    {
        return null;
    }

    if (int.TryParse(rawValue, out var value))
    {
        return value;
    }

    throw new ValidationException($"O parâmetro '{key}' deve ser um número inteiro válido.");
}

static Guid[] ReadGuidCollectionQuery(HttpRequest request, string key)
{
    if (!request.Query.TryGetValue(key, out var values))
    {
        return [];
    }

    var results = new List<Guid>();
    foreach (var rawValue in values)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            continue;
        }

        if (!Guid.TryParse(rawValue, out var guid))
        {
            throw new ValidationException($"O parâmetro '{key}' deve conter apenas GUIDs válidos.");
        }

        results.Add(guid);
    }

    return results.ToArray();
}

public partial class Program;
