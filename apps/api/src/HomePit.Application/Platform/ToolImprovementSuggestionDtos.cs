using HomePit.Domain.Platform;

namespace HomePit.Application.Platform;

public sealed record CreateToolImprovementSuggestionRequest(
    string SuggestionText);

public sealed record UpdateToolImprovementSuggestionRequest(
    ToolImprovementSuggestionStatus Status,
    ToolImprovementSuggestionPriority Priority,
    string? InternalComment);

public sealed record BulkUpdateToolImprovementSuggestionsRequest(
    IReadOnlyCollection<Guid> SuggestionIds,
    ToolImprovementSuggestionStatus? Status,
    ToolImprovementSuggestionPriority? Priority);

public sealed record ToolImprovementSuggestionDto(
    Guid Id,
    Guid UserId,
    string UserDisplayName,
    string UserEmail,
    DateTimeOffset SubmittedAt,
    string SuggestionText,
    ToolImprovementSuggestionStatus Status,
    ToolImprovementSuggestionPriority Priority,
    string? InternalComment,
    DateTimeOffset? LastReviewedAt,
    Guid? LastReviewedByUserId,
    string? LastReviewedByDisplayName);
