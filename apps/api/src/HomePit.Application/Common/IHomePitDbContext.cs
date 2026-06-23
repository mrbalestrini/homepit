using HomePit.Domain.Households;
using HomePit.Domain.Institutional;
using HomePit.Domain.Gsm;
using HomePit.Domain.Notifications;
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
    DbSet<GsmNumber> GsmNumbers { get; }
    DbSet<Universe> Universes { get; }
    DbSet<Project> Projects { get; }
    DbSet<Activity> Activities { get; }
    DbSet<ActivityComment> ActivityComments { get; }
    DbSet<PendingItem> PendingItems { get; }
    DbSet<Prompt> Prompts { get; }
    DbSet<PromptCategory> PromptCategories { get; }
    DbSet<PromptCategoryAssignment> PromptCategoryAssignments { get; }
    DbSet<NotificationPreference> NotificationPreferences { get; }
    DbSet<NotificationRun> NotificationRuns { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
