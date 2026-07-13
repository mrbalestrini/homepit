using HomePit.Domain.Projects;

namespace HomePit.Application.Projects;

public sealed record EffortAllocationInput(
    EffortScopeType ScopeType,
    Guid? ScopeId,
    EffortWeekday Weekday,
    decimal Points);

public sealed record UpdateEffortPlanRequest(IReadOnlyCollection<EffortAllocationInput> Allocations);

public sealed record EffortPlanDayDto(
    EffortWeekday Weekday,
    decimal? ExplicitPoints,
    decimal EffectivePoints,
    decimal SharedPoints);

public sealed record EffortPlanScopeDto(
    EffortScopeType ScopeType,
    Guid? ScopeId,
    Guid? ParentScopeId,
    string Name,
    IReadOnlyCollection<EffortPlanDayDto> Days);

public sealed record EffortPlanDto(
    Guid HouseholdId,
    Guid HouseholdMemberId,
    IReadOnlyCollection<EffortPlanScopeDto> Scopes);

public enum ActivityRelevanceQueueState
{
    Scheduled,
    Overflow,
    MissingEstimate
}

public sealed record ActivityRelevanceDto(
    Guid ActivityId,
    int Position,
    int Score,
    int PriorityScore,
    int DueDateScore,
    int AgeScore,
    int AssignmentScore,
    ActivityRelevanceQueueState QueueState);

public sealed record ActivityRelevanceResponse(
    DateOnly Date,
    EffortWeekday Weekday,
    decimal CapacityPoints,
    decimal ScheduledPoints,
    IReadOnlyCollection<ActivityRelevanceDto> Items);
