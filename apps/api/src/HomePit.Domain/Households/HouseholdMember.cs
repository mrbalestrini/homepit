using HomePit.Domain.Common;
using HomePit.Domain.Finance;
using HomePit.Domain.Gsm;
using HomePit.Domain.Notifications;
using HomePit.Domain.Prompts;
using HomePit.Domain.Projects;

namespace HomePit.Domain.Households;

public sealed class HouseholdMember : AuditableEntity, IHouseholdScoped
{
    public Guid HouseholdId { get; set; }
    public Household? Household { get; set; }

    public Guid UserId { get; set; }
    public AppUser? User { get; set; }

    public HouseholdRole Role { get; set; } = HouseholdRole.Member;
    public bool IsActive { get; set; } = true;

    public ICollection<Activity> AssignedActivities { get; } = new List<Activity>();
    public ICollection<Universe> CreatedUniverses { get; } = new List<Universe>();
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
