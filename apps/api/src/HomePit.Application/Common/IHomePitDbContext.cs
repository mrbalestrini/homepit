using HomePit.Domain.Households;
using HomePit.Domain.Institutional;
using HomePit.Domain.Finance;
using HomePit.Domain.Gsm;
using HomePit.Domain.Notifications;
using HomePit.Domain.Platform;
using HomePit.Domain.Plans;
using HomePit.Domain.Prompts;
using HomePit.Domain.Projects;
using Microsoft.EntityFrameworkCore;

namespace HomePit.Application.Common;

public interface IHomePitDbContext
{
    DbSet<AppUser> Users { get; }
    DbSet<Household> Households { get; }
    DbSet<HouseholdMember> HouseholdMembers { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<InstitutionalPage> InstitutionalPages { get; }
    DbSet<InstitutionalBenefit> InstitutionalBenefits { get; }
    DbSet<InstitutionalStep> InstitutionalSteps { get; }
    DbSet<PlatformSettings> PlatformSettings { get; }
    DbSet<ToolImprovementSuggestion> ToolImprovementSuggestions { get; }
    DbSet<FinanceCategory> FinanceCategories { get; }
    DbSet<FinancePeriod> FinancePeriods { get; }
    DbSet<FinanceRecurringTemplate> FinanceRecurringTemplates { get; }
    DbSet<FinanceEntry> FinanceEntries { get; }
    DbSet<Asset> Assets { get; }
    DbSet<AssetPropertyDetails> AssetPropertyDetails { get; }
    DbSet<AssetVehicleDetails> AssetVehicleDetails { get; }
    DbSet<AssetValuation> AssetValuations { get; }
    DbSet<CreditCardAccount> CreditCardAccounts { get; }
    DbSet<CreditCardTransaction> CreditCardTransactions { get; }
    DbSet<CreditCardStatement> CreditCardStatements { get; }
    DbSet<GsmNumber> GsmNumbers { get; }
    DbSet<GsmRecharge> GsmRecharges { get; }
    DbSet<Universe> Universes { get; }
    DbSet<Project> Projects { get; }
    DbSet<Activity> Activities { get; }
    DbSet<ActivityComment> ActivityComments { get; }
    DbSet<PendingItem> PendingItems { get; }
    DbSet<PlanDefinition> PlanDefinitions { get; }
    DbSet<Prompt> Prompts { get; }
    DbSet<PromptCategory> PromptCategories { get; }
    DbSet<PromptCategoryAssignment> PromptCategoryAssignments { get; }
    DbSet<UserPlanImageAsset> UserPlanImageAssets { get; }
    DbSet<UserSubscription> UserSubscriptions { get; }
    DbSet<NotificationPreference> NotificationPreferences { get; }
    DbSet<NotificationRun> NotificationRuns { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
