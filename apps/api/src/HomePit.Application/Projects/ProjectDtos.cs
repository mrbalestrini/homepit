using HomePit.Domain.Projects;

namespace HomePit.Application.Projects;

public sealed record CreateUniverseRequest(string Name, string? ImageUrl);

public sealed record UpdateUniverseRequest(string Name, string? ImageUrl);

public sealed record UniverseDto(
    Guid Id,
    string Name,
    string? ImageUrl,
    bool HasImage,
    DateTimeOffset? ImageUpdatedAt,
    Guid? CreatedByMemberId,
    int ProjectCount,
    bool CanEdit,
    bool CanDelete);

public sealed record CreateProjectRequest(Guid UniverseId, string Name);

public sealed record UpdateProjectRequest(Guid UniverseId, string Name);

public sealed record ProjectDto(
    Guid Id,
    Guid UniverseId,
    string UniverseName,
    string? UniverseImageUrl,
    bool UniverseHasImage,
    DateTimeOffset? UniverseImageUpdatedAt,
    string Name,
    Guid? CreatedByMemberId,
    int ActivityCount,
    bool CanEdit,
    bool CanDelete);

public sealed record CreateActivityRequest(
    Guid ProjectId,
    string Title,
    string? Description,
    DateOnly? DueDate,
    ActivityStatus Status,
    Priority Priority,
    decimal? Size,
    Guid? ResponsibleMemberId);

public sealed record UpdateActivityStatusRequest(ActivityStatus Status);

public sealed record UpdateActivityRequest(
    Guid ProjectId,
    string Title,
    string? Description,
    DateOnly? DueDate,
    ActivityStatus Status,
    Priority Priority,
    decimal? Size,
    Guid? ResponsibleMemberId);

public sealed record ActivityDto(
    Guid Id,
    Guid ProjectId,
    string ProjectName,
    Guid UniverseId,
    string UniverseName,
    string? UniverseImageUrl,
    bool UniverseHasImage,
    DateTimeOffset? UniverseImageUpdatedAt,
    Guid? CreatedByMemberId,
    DateTimeOffset CreatedAt,
    string Title,
    string? Description,
    DateOnly? DueDate,
    ActivityStatus Status,
    Priority Priority,
    decimal? Size,
    Guid? ResponsibleMemberId,
    string? ResponsibleName,
    int PendingCount,
    int CommentCount,
    bool CanEdit,
    bool CanDelete);

public sealed record CreateActivityCommentRequest(string Body);

public sealed record UpdateActivityCommentRequest(string Body);

public sealed record ActivityCommentDto(
    Guid Id,
    Guid ActivityId,
    Guid AuthorMemberId,
    string AuthorName,
    string Body,
    DateTimeOffset CreatedAt,
    bool IsEdited,
    bool CanEdit,
    bool CanDelete);

public sealed record CreatePendingItemRequest(
    string Title,
    string? Description,
    Priority Priority,
    DateOnly? DueDate,
    int? SnoozeDays);

public sealed record PendingItemDto(
    Guid Id,
    Guid ActivityId,
    string Title,
    string? Description,
    Priority Priority,
    DateOnly? DueDate,
    int? SnoozeDays,
    DateTimeOffset? CompletedAt);
