using System.Text;
using System.Text.Json;
using OrganizaClub.Application.Common;
using OrganizaClub.Application.Projects;
using OrganizaClub.Domain.Common;
using OrganizaClub.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace OrganizaClub.Api.Integrations;

public sealed record IntegrationResource<T>(T Data, string Etag);

public sealed record IntegrationPage<T>(IReadOnlyCollection<IntegrationResource<T>> Items, string? NextCursor);

public sealed record IntegrationCoreDto(Guid Id, string Name, bool HasImage, DateTimeOffset? ImageUpdatedAt, Guid? CreatedByMemberId, int ProjectCount, bool IsOutOfPlan);
public sealed record IntegrationProjectDto(Guid Id, Guid CoreId, string CoreName, bool CoreHasImage, DateTimeOffset? CoreImageUpdatedAt, string Name, Guid? CreatedByMemberId, int ActivityCount, bool IsOutOfPlan);
public sealed record IntegrationActivityDto(Guid Id, Guid ProjectId, string ProjectName, Guid CoreId, string CoreName, bool CoreHasImage, DateTimeOffset? CoreImageUpdatedAt, Guid? CreatedByMemberId, DateTimeOffset CreatedAt, string Title, string? Description, bool HasImage, DateTimeOffset? ImageUpdatedAt, DateOnly? DueDate, DateTimeOffset? CompletedAt, object Status, object Priority, decimal? Size, Guid? ResponsibleMemberId, string? ResponsibleName, int PendingCount, int CommentCount);

public static class IntegrationExternalDto
{
    public static IntegrationCoreDto ToExternal(CoreDto item) => new(item.Id, item.Name, item.HasImage, item.ImageUpdatedAt, item.CreatedByMemberId, item.ProjectCount, item.IsOutOfPlan);
    public static IntegrationProjectDto ToExternal(ProjectDto item) => new(item.Id, item.CoreId, item.CoreName, item.CoreHasImage, item.CoreImageUpdatedAt, item.Name, item.CreatedByMemberId, item.ActivityCount, item.IsOutOfPlan);
    public static IntegrationActivityDto ToExternal(ActivityDto item) => new(item.Id, item.ProjectId, item.ProjectName, item.CoreId, item.CoreName, item.CoreHasImage, item.CoreImageUpdatedAt, item.CreatedByMemberId, item.CreatedAt, item.Title, item.Description, item.HasImage, item.ImageUpdatedAt, item.DueDate, item.CompletedAt, item.Status, item.Priority, item.Size, item.ResponsibleMemberId, item.ResponsibleName, item.PendingCount, item.CommentCount);
}

public sealed class IntegrationRestSupport
{
    private const int DefaultLimit = 50;
    private const int MaximumLimit = 200;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<IntegrationPage<T>> PageAsync<TEntity, T>(
        IReadOnlyCollection<T> values,
        Func<T, Guid> id,
        DbSet<TEntity> entities,
        string scope,
        string? cursor,
        int? limit,
        CancellationToken cancellationToken)
        where TEntity : AuditableEntity
    {
        var pageSize = ReadLimit(limit);
        var continuation = ReadCursor(cursor, scope);
        var ids = values.Select(id).Distinct().ToArray();
        var versions = await entities.AsNoTracking()
            .Where(entity => ids.Contains(entity.Id))
            .Select(entity => new VersionedId(entity.Id, entity.UpdatedAt))
            .ToDictionaryAsync(item => item.Id, item => item.UpdatedAt, cancellationToken);

        var ordered = values
            .Where(value => versions.ContainsKey(id(value)))
            .Select(value => new VersionedValue<T>(value, id(value), versions[id(value)]))
            .OrderByDescending(item => item.UpdatedAt.UtcDateTime.Ticks)
            .ThenByDescending(item => item.Id)
            .Where(item => continuation is null || IsAfter(item, continuation))
            .Take(pageSize + 1)
            .ToArray();

        var hasMore = ordered.Length > pageSize;
        var items = ordered.Take(pageSize)
            .Select(item => new IntegrationResource<T>(item.Value, CreateEtag(item.Id, item.UpdatedAt)))
            .ToArray();
        var last = items.Length == 0 ? null : ordered[Math.Min(ordered.Length, pageSize) - 1];
        return new IntegrationPage<T>(items, hasMore && last is not null
            ? WriteCursor(new Cursor(scope, last.UpdatedAt.UtcDateTime.Ticks, last.Id))
            : null);
    }

    public async Task<IntegrationResource<T>> ResourceAsync<TEntity, T>(
        T value,
        Guid id,
        DbSet<TEntity> entities,
        CancellationToken cancellationToken)
        where TEntity : AuditableEntity
    {
        var updatedAt = await entities.AsNoTracking()
            .Where(entity => entity.Id == id)
            .Select(entity => (DateTimeOffset?)entity.UpdatedAt)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Recurso não encontrado.");
        return new IntegrationResource<T>(value, CreateEtag(id, updatedAt));
    }

    public async Task<DateTimeOffset> ReadExpectedVersionAsync<TEntity>(
        Guid id,
        string? ifMatch,
        DbSet<TEntity> entities,
        CancellationToken cancellationToken)
        where TEntity : AuditableEntity
    {
        if (string.IsNullOrWhiteSpace(ifMatch))
        {
            throw new PreconditionRequiredException("Informe a ETag recebida no cabeçalho If-Match.");
        }

        var etag = ReadEtag(ifMatch);
        if (etag.Id != id)
        {
            throw new ValidationException("A ETag não pertence ao recurso informado.");
        }

        var current = await entities.AsNoTracking()
            .Where(entity => entity.Id == id)
            .Select(entity => (DateTimeOffset?)entity.UpdatedAt)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Recurso não encontrado.");
        if (current.UtcDateTime.Ticks != etag.UpdatedAtTicks)
        {
            throw new PreconditionFailedException("O recurso foi alterado desde a última leitura.");
        }

        return current;
    }

    public static void SetEtag(HttpResponse response, string etag) => response.Headers.ETag = etag;

    private static int ReadLimit(int? limit)
    {
        if (limit is null)
        {
            return DefaultLimit;
        }

        if (limit < 1 || limit > MaximumLimit)
        {
            throw new ValidationException($"Informe limit entre 1 e {MaximumLimit}.");
        }

        return limit.Value;
    }

    private static bool IsAfter<T>(VersionedValue<T> value, Cursor cursor) =>
        value.UpdatedAt.UtcDateTime.Ticks < cursor.UpdatedAtTicks ||
        (value.UpdatedAt.UtcDateTime.Ticks == cursor.UpdatedAtTicks && value.Id.CompareTo(cursor.Id) < 0);

    private static string CreateEtag(Guid id, DateTimeOffset updatedAt) => Quote(Encode(new Etag(id, updatedAt.UtcDateTime.Ticks)));

    private static Etag ReadEtag(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length < 3 || trimmed[0] != '"' || trimmed[^1] != '"')
        {
            throw new ValidationException("If-Match deve conter uma ETag válida entre aspas.");
        }

        try
        {
            return Decode<Etag>(trimmed[1..^1]);
        }
        catch (Exception exception) when (exception is JsonException or FormatException or ArgumentException)
        {
            throw new ValidationException("If-Match deve conter uma ETag válida.");
        }
    }

    private static Cursor? ReadCursor(string? value, string scope)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        try
        {
            var cursor = Decode<Cursor>(value);
            if (!string.Equals(cursor.Scope, scope, StringComparison.Ordinal))
            {
                throw new ValidationException("O cursor não corresponde à consulta atual.");
            }

            return cursor;
        }
        catch (ValidationException)
        {
            throw;
        }
        catch (Exception exception) when (exception is JsonException or FormatException or ArgumentException)
        {
            throw new ValidationException("Informe um cursor válido.");
        }
    }

    private static string WriteCursor(Cursor cursor) => Encode(cursor);

    private static string Encode<T>(T value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, Json)))
        .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static T Decode<T>(string value)
    {
        var base64 = value.Replace('-', '+').Replace('_', '/');
        base64 = base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');
        return JsonSerializer.Deserialize<T>(Convert.FromBase64String(base64), Json)
            ?? throw new JsonException("Cursor sem conteúdo.");
    }

    private static string Quote(string value) => $"\"{value}\"";

    private sealed record VersionedId(Guid Id, DateTimeOffset UpdatedAt);
    private sealed record VersionedValue<T>(T Value, Guid Id, DateTimeOffset UpdatedAt);
    private sealed record Cursor(string Scope, long UpdatedAtTicks, Guid Id);
    private sealed record Etag(Guid Id, long UpdatedAtTicks);
}
