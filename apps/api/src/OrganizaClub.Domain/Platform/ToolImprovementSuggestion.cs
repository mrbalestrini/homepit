using OrganizaClub.Domain.Common;
using OrganizaClub.Domain.Spaces;

namespace OrganizaClub.Domain.Platform;

public sealed class ToolImprovementSuggestion : AuditableEntity
{
    public Guid UserId { get; set; }
    public AppUser? User { get; set; }
    public DateTimeOffset SubmittedAt { get; set; }
    public required string SuggestionText { get; set; }
    public ToolImprovementSuggestionStatus Status { get; set; } = ToolImprovementSuggestionStatus.NaoLido;
    public ToolImprovementSuggestionPriority Priority { get; set; } = ToolImprovementSuggestionPriority.Media;
    public string? InternalComment { get; set; }
    public DateTimeOffset? LastReviewedAt { get; set; }
    public Guid? LastReviewedByUserId { get; set; }
    public AppUser? LastReviewedByUser { get; set; }
}
