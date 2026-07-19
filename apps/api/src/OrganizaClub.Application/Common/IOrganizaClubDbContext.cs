using OrganizaClub.Domain.Spaces;
using OrganizaClub.Domain.Integrations;
using OrganizaClub.Domain.Institutional;
using OrganizaClub.Domain.Finance;
using OrganizaClub.Domain.Gsm;
using OrganizaClub.Domain.Notifications;
using OrganizaClub.Domain.Platform;
using OrganizaClub.Domain.Plans;
using OrganizaClub.Domain.Prompts;
using OrganizaClub.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using OrganizaClub.Domain.Common;

namespace OrganizaClub.Application.Common;

public interface IOrganizaClubDbContext
{
    DbSet<AppUser> Users { get; }
    DbSet<Space> Spaces { get; }
    DbSet<SpaceMember> SpaceMembers { get; }
    DbSet<SpaceInvitation> SpaceInvitations { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<IntegrationConnection> IntegrationConnections { get; }
    DbSet<IntegrationAuditEvent> IntegrationAuditEvents { get; }
    DbSet<IntegrationIdempotencyRecord> IntegrationIdempotencyRecords { get; }
    DbSet<OAuthAuthorizationInteraction> OAuthAuthorizationInteractions { get; }
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
    DbSet<Core> Cores { get; }
    DbSet<Project> Projects { get; }
    DbSet<MemberEffortAllocation> MemberEffortAllocations { get; }
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
    void SetExpectedUpdatedAt(AuditableEntity entity, DateTimeOffset expectedUpdatedAt);
}
