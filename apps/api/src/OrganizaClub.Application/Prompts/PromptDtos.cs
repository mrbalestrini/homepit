namespace OrganizaClub.Application.Prompts;

public sealed record PromptCategoryReferenceDto(Guid Id, string Name);

public sealed record CreatePromptRequest(
    Guid? CoreId,
    string Title,
    string? Description,
    string PromptText,
    IReadOnlyCollection<Guid> CategoryIds,
    string? LinkUrl,
    string? LinkTitle);

public sealed record UpdatePromptRequest(
    Guid? CoreId,
    string Title,
    string? Description,
    string PromptText,
    IReadOnlyCollection<Guid> CategoryIds,
    string? LinkUrl,
    string? LinkTitle);

public sealed record PromptListItemDto(
    Guid Id,
    Guid? CoreId,
    string? CoreName,
    string? CoreImageUrl,
    bool CoreHasImage,
    DateTimeOffset? CoreImageUpdatedAt,
    string Title,
    string? Description,
    string PromptText,
    IReadOnlyCollection<PromptCategoryReferenceDto> Categories,
    string? LinkUrl,
    string? LinkTitle,
    Guid? CreatedByMemberId,
    bool IsArchived,
    bool HasImage,
    DateTimeOffset? ImageUpdatedAt,
    DateTimeOffset UpdatedAt,
    bool CanEdit,
    bool CanDelete);

public sealed record PromptListResponse(
    IReadOnlyCollection<PromptListItemDto> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record PromptDetailDto(
    Guid Id,
    Guid? CoreId,
    string? CoreName,
    string? CoreImageUrl,
    bool CoreHasImage,
    DateTimeOffset? CoreImageUpdatedAt,
    string Title,
    string? Description,
    string PromptText,
    IReadOnlyCollection<PromptCategoryReferenceDto> Categories,
    string? LinkUrl,
    string? LinkTitle,
    Guid? CreatedByMemberId,
    bool IsArchived,
    bool HasImage,
    DateTimeOffset? ImageUpdatedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    bool CanEdit,
    bool CanDelete);

public sealed record CreatePromptCategoryRequest(string Name);

public sealed record UpdatePromptCategoryRequest(string Name);

public sealed record PromptCategoryDto(
    Guid Id,
    string Name,
    Guid? CreatedByMemberId,
    int UsageCount,
    int ReplacementRequiredCount,
    bool CanEdit,
    bool CanDelete);
