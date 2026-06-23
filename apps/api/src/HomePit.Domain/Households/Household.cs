using HomePit.Domain.Common;
using HomePit.Domain.Gsm;
using HomePit.Domain.Notifications;
using HomePit.Domain.Prompts;
using HomePit.Domain.Projects;

namespace HomePit.Domain.Households;

public sealed class Household : AuditableEntity
{
    public required string Name { get; set; }

    public ICollection<HouseholdMember> Members { get; } = new List<HouseholdMember>();
    public ICollection<Universe> Universes { get; } = new List<Universe>();
    public ICollection<Prompt> Prompts { get; } = new List<Prompt>();
    public ICollection<PromptCategory> PromptCategories { get; } = new List<PromptCategory>();
    public ICollection<GsmNumber> GsmNumbers { get; } = new List<GsmNumber>();
    public ICollection<NotificationRun> NotificationRuns { get; } = new List<NotificationRun>();
}
