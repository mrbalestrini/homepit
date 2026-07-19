using OrganizaClub.Domain.Common;
using OrganizaClub.Domain.Finance;
using OrganizaClub.Domain.Gsm;
using OrganizaClub.Domain.Notifications;
using OrganizaClub.Domain.Prompts;
using OrganizaClub.Domain.Projects;

namespace OrganizaClub.Domain.Spaces;

public sealed class SpaceMember : AuditableEntity, ISpaceScoped
{
    public Guid SpaceId { get; set; }
    public Space? Space { get; set; }

    public Guid UserId { get; set; }
    public AppUser? User { get; set; }

    public SpaceRole Role { get; set; } = SpaceRole.Member;
    public bool IsActive { get; set; } = true;

    public ICollection<Activity> AssignedActivities { get; } = new List<Activity>();
    public ICollection<MemberEffortAllocation> EffortAllocations { get; } = new List<MemberEffortAllocation>();
    public ICollection<Core> CreatedCores { get; } = new List<Core>();
    public ICollection<Project> CreatedProjects { get; } = new List<Project>();
    public ICollection<Activity> CreatedActivities { get; } = new List<Activity>();
    public ICollection<Prompt> CreatedPrompts { get; } = new List<Prompt>();
    public ICollection<PromptCategory> CreatedPromptCategories { get; } = new List<PromptCategory>();
    public ICollection<GsmNumber> CreatedGsmNumbers { get; } = new List<GsmNumber>();
    public ICollection<GsmRecharge> CreatedGsmRecharges { get; } = new List<GsmRecharge>();
    public ICollection<FinanceCategory> CreatedFinanceCategories { get; } = new List<FinanceCategory>();
    public ICollection<FinanceRecurringTemplate> CreatedFinanceRecurringTemplates { get; } = new List<FinanceRecurringTemplate>();
    public ICollection<FinanceEntry> CreatedFinanceEntries { get; } = new List<FinanceEntry>();
    public ICollection<Asset> CreatedAssets { get; } = new List<Asset>();
    public ICollection<CreditCardAccount> CreatedCreditCardAccounts { get; } = new List<CreditCardAccount>();
    public ICollection<CreditCardTransaction> CreatedCreditCardTransactions { get; } = new List<CreditCardTransaction>();
    public ICollection<CreditCardStatement> CreatedCreditCardStatements { get; } = new List<CreditCardStatement>();
    public ICollection<ActivityComment> AuthoredActivityComments { get; } = new List<ActivityComment>();
    public NotificationPreference? NotificationPreference { get; set; }
}
