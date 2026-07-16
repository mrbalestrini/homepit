using HomePit.Application.Common;
using HomePit.Domain.Common;
using HomePit.Domain.Finance;
using HomePit.Domain.Gsm;
using HomePit.Domain.Households;
using HomePit.Domain.Institutional;
using HomePit.Domain.Integrations;
using HomePit.Domain.Notifications;
using HomePit.Domain.Platform;
using HomePit.Domain.Plans;
using HomePit.Domain.Prompts;
using HomePit.Domain.Projects;
using Microsoft.EntityFrameworkCore;

namespace HomePit.Infrastructure.Data;

public sealed class HomePitDbContext(DbContextOptions<HomePitDbContext> options)
    : DbContext(options), IHomePitDbContext
{
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Household> Households => Set<Household>();
    public DbSet<HouseholdMember> HouseholdMembers => Set<HouseholdMember>();
    public DbSet<HouseholdInvitation> HouseholdInvitations => Set<HouseholdInvitation>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<IntegrationConnection> IntegrationConnections => Set<IntegrationConnection>();
    public DbSet<IntegrationAuditEvent> IntegrationAuditEvents => Set<IntegrationAuditEvent>();
    public DbSet<IntegrationIdempotencyRecord> IntegrationIdempotencyRecords => Set<IntegrationIdempotencyRecord>();
    public DbSet<InstitutionalPage> InstitutionalPages => Set<InstitutionalPage>();
    public DbSet<InstitutionalBenefit> InstitutionalBenefits => Set<InstitutionalBenefit>();
    public DbSet<InstitutionalStep> InstitutionalSteps => Set<InstitutionalStep>();
    public DbSet<PlatformSettings> PlatformSettings => Set<PlatformSettings>();
    public DbSet<ToolImprovementSuggestion> ToolImprovementSuggestions => Set<ToolImprovementSuggestion>();
    public DbSet<FinanceCategory> FinanceCategories => Set<FinanceCategory>();
    public DbSet<FinancePeriod> FinancePeriods => Set<FinancePeriod>();
    public DbSet<FinanceRecurringTemplate> FinanceRecurringTemplates => Set<FinanceRecurringTemplate>();
    public DbSet<FinanceEntry> FinanceEntries => Set<FinanceEntry>();
    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<AssetPropertyDetails> AssetPropertyDetails => Set<AssetPropertyDetails>();
    public DbSet<AssetVehicleDetails> AssetVehicleDetails => Set<AssetVehicleDetails>();
    public DbSet<AssetValuation> AssetValuations => Set<AssetValuation>();
    public DbSet<CreditCardAccount> CreditCardAccounts => Set<CreditCardAccount>();
    public DbSet<CreditCardTransaction> CreditCardTransactions => Set<CreditCardTransaction>();
    public DbSet<CreditCardStatement> CreditCardStatements => Set<CreditCardStatement>();
    public DbSet<GsmNumber> GsmNumbers => Set<GsmNumber>();
    public DbSet<GsmRecharge> GsmRecharges => Set<GsmRecharge>();
    public DbSet<Universe> Universes => Set<Universe>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<MemberEffortAllocation> MemberEffortAllocations => Set<MemberEffortAllocation>();
    public DbSet<Activity> Activities => Set<Activity>();
    public DbSet<ActivityComment> ActivityComments => Set<ActivityComment>();
    public DbSet<PendingItem> PendingItems => Set<PendingItem>();
    public DbSet<PlanDefinition> PlanDefinitions => Set<PlanDefinition>();
    public DbSet<Prompt> Prompts => Set<Prompt>();
    public DbSet<PromptCategory> PromptCategories => Set<PromptCategory>();
    public DbSet<PromptCategoryAssignment> PromptCategoryAssignments => Set<PromptCategoryAssignment>();
    public DbSet<UserPlanImageAsset> UserPlanImageAssets => Set<UserPlanImageAsset>();
    public DbSet<UserSubscription> UserSubscriptions => Set<UserSubscription>();
    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();
    public DbSet<NotificationRun> NotificationRuns => Set<NotificationRun>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("homepit");

        ConfigureHouseholds(modelBuilder);
        ConfigureIntegrations(modelBuilder);
        ConfigureFinance(modelBuilder);
        ConfigureGsm(modelBuilder);
        ConfigureInstitutional(modelBuilder);
        ConfigurePlatform(modelBuilder);
        ConfigurePlans(modelBuilder);
        ConfigureProjects(modelBuilder);
        ConfigureIntegrationConcurrency(modelBuilder);
        ConfigurePrompts(modelBuilder);
        ConfigureNotifications(modelBuilder);
    }

    private static void ConfigureInstitutional(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<InstitutionalPage>(builder =>
        {
            builder.ToTable("institutional_pages");
            builder.Property(page => page.Slug).HasMaxLength(80).IsRequired();
            builder.Property(page => page.SeoTitle).HasMaxLength(160).IsRequired();
            builder.Property(page => page.SeoDescription).HasMaxLength(320).IsRequired();
            builder.Property(page => page.SeoImageObjectKey).HasMaxLength(512);
            builder.Property(page => page.SeoImageContentType).HasMaxLength(120);
            builder.Property(page => page.BrandName).HasMaxLength(80).IsRequired();
            builder.Property(page => page.BrandTagline).HasMaxLength(200).IsRequired();
            builder.Property(page => page.HeroEyebrow).HasMaxLength(120).IsRequired();
            builder.Property(page => page.HeroTitle).HasMaxLength(240).IsRequired();
            builder.Property(page => page.HeroDescription).HasMaxLength(1200).IsRequired();
            builder.Property(page => page.PrimaryCtaLabel).HasMaxLength(80).IsRequired();
            builder.Property(page => page.PrimaryCtaUrl).HasMaxLength(2000).IsRequired();
            builder.Property(page => page.BenefitsTitle).HasMaxLength(200).IsRequired();
            builder.Property(page => page.BenefitsDescription).HasMaxLength(600).IsRequired();
            builder.Property(page => page.StepsTitle).HasMaxLength(200).IsRequired();
            builder.Property(page => page.StepsDescription).HasMaxLength(600).IsRequired();
            builder.Property(page => page.HighlightEyebrow).HasMaxLength(120).IsRequired();
            builder.Property(page => page.HighlightTitle).HasMaxLength(240).IsRequired();
            builder.Property(page => page.HighlightDescription).HasMaxLength(1200).IsRequired();
            builder.Property(page => page.FinalCtaTitle).HasMaxLength(240).IsRequired();
            builder.Property(page => page.FinalCtaDescription).HasMaxLength(1200).IsRequired();
            builder.Property(page => page.FooterText).HasMaxLength(600).IsRequired();
            builder.Property(page => page.HeroImageAlt).HasMaxLength(300).IsRequired();
            builder.Property(page => page.HeroImageObjectKey).HasMaxLength(512);
            builder.Property(page => page.HeroImageContentType).HasMaxLength(120);
            builder.Property(page => page.HighlightImageAlt).HasMaxLength(300).IsRequired();
            builder.Property(page => page.HighlightImageObjectKey).HasMaxLength(512);
            builder.Property(page => page.HighlightImageContentType).HasMaxLength(120);
            builder.HasIndex(page => page.Slug).IsUnique();
        });

        modelBuilder.Entity<InstitutionalBenefit>(builder =>
        {
            builder.ToTable("institutional_benefits");
            builder.Property(item => item.Title).HasMaxLength(160).IsRequired();
            builder.Property(item => item.Description).HasMaxLength(600).IsRequired();
            builder.HasIndex(item => new { item.InstitutionalPageId, item.Position }).IsUnique();
            builder.HasOne(item => item.InstitutionalPage)
                .WithMany(page => page.Benefits)
                .HasForeignKey(item => item.InstitutionalPageId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<InstitutionalStep>(builder =>
        {
            builder.ToTable("institutional_steps");
            builder.Property(item => item.Title).HasMaxLength(160).IsRequired();
            builder.Property(item => item.Description).HasMaxLength(600).IsRequired();
            builder.HasIndex(item => new { item.InstitutionalPageId, item.Position }).IsUnique();
            builder.HasOne(item => item.InstitutionalPage)
                .WithMany(page => page.Steps)
                .HasForeignKey(item => item.InstitutionalPageId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.UpdatedAt = now;
            }

            if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }

    private static void ConfigureHouseholds(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppUser>(builder =>
        {
            builder.ToTable("users");
            builder.Property(user => user.Email).HasMaxLength(320).IsRequired();
            builder.Property(user => user.DisplayName).HasMaxLength(160).IsRequired();
            builder.Property(user => user.PasswordHash).HasMaxLength(512).IsRequired();
            builder.Property(user => user.PhoneNumber).HasMaxLength(40);
            builder.Property(user => user.ProfilePhotoObjectKey).HasMaxLength(512);
            builder.Property(user => user.SystemRole).HasConversion<string>().HasMaxLength(40).IsRequired();
            builder.Property(user => user.AccountState).HasConversion<string>().HasMaxLength(40).IsRequired();
            builder.HasIndex(user => user.Email).IsUnique();
            builder.HasOne(user => user.DeactivatedByUser)
                .WithMany(user => user.DeactivatedUsers)
                .HasForeignKey(user => user.DeactivatedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Household>(builder =>
        {
            builder.ToTable("households");
            builder.Property(household => household.Name).HasMaxLength(160).IsRequired();
            builder.HasIndex(household => household.CreatedByUserId);
            builder.HasOne(household => household.CreatedByUser)
                .WithMany(user => user.CreatedHouseholds)
                .HasForeignKey(household => household.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<HouseholdMember>(builder =>
        {
            builder.ToTable("household_members");
            builder.Property(member => member.Role).HasConversion<string>().HasMaxLength(40).IsRequired();
            builder.HasIndex(member => new { member.HouseholdId, member.UserId }).IsUnique();
            builder.HasOne(member => member.Household)
                .WithMany(household => household.Members)
                .HasForeignKey(member => member.HouseholdId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(member => member.User)
                .WithMany(user => user.HouseholdMembers)
                .HasForeignKey(member => member.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<HouseholdInvitation>(builder =>
        {
            builder.ToTable("household_invitations");
            builder.Property(item => item.InviteeEmail).HasMaxLength(320).IsRequired();
            builder.Property(item => item.Role).HasConversion<string>().HasMaxLength(40).IsRequired();
            builder.Property(item => item.Status).HasConversion<string>().HasMaxLength(40).IsRequired();
            builder.Property(item => item.InvitedAt).IsRequired();
            builder.Property(item => item.RespondedAt);
            builder.HasIndex(item => new { item.HouseholdId, item.InviteeEmail }).IsUnique();
            builder.HasIndex(item => new { item.InviteeEmail, item.Status });
            builder.HasOne(item => item.Household)
                .WithMany(household => household.Invitations)
                .HasForeignKey(item => item.HouseholdId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(item => item.InviterUser)
                .WithMany(user => user.SentHouseholdInvitations)
                .HasForeignKey(item => item.InviterUserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RefreshToken>(builder =>
        {
            builder.ToTable("refresh_tokens");
            builder.Property(token => token.TokenHash).HasMaxLength(128).IsRequired();
            builder.HasIndex(token => token.TokenHash).IsUnique();
            builder.HasOne(token => token.User)
                .WithMany(user => user.RefreshTokens)
                .HasForeignKey(token => token.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureIntegrationConcurrency(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FinanceCategory>().Property(item => item.UpdatedAt).IsConcurrencyToken();
        modelBuilder.Entity<FinanceRecurringTemplate>().Property(item => item.UpdatedAt).IsConcurrencyToken();
        modelBuilder.Entity<FinanceEntry>().Property(item => item.UpdatedAt).IsConcurrencyToken();
        modelBuilder.Entity<Asset>().Property(item => item.UpdatedAt).IsConcurrencyToken();
        modelBuilder.Entity<AssetValuation>().Property(item => item.UpdatedAt).IsConcurrencyToken();
        modelBuilder.Entity<CreditCardAccount>().Property(item => item.UpdatedAt).IsConcurrencyToken();
        modelBuilder.Entity<CreditCardTransaction>().Property(item => item.UpdatedAt).IsConcurrencyToken();
        modelBuilder.Entity<CreditCardStatement>().Property(item => item.UpdatedAt).IsConcurrencyToken();
        modelBuilder.Entity<Universe>().Property(item => item.UpdatedAt).IsConcurrencyToken();
        modelBuilder.Entity<Project>().Property(item => item.UpdatedAt).IsConcurrencyToken();
        modelBuilder.Entity<Activity>().Property(item => item.UpdatedAt).IsConcurrencyToken();
        modelBuilder.Entity<ActivityComment>().Property(item => item.UpdatedAt).IsConcurrencyToken();
        modelBuilder.Entity<PendingItem>().Property(item => item.UpdatedAt).IsConcurrencyToken();
    }

    public void SetExpectedUpdatedAt(AuditableEntity entity, DateTimeOffset expectedUpdatedAt)
    {
        Entry(entity).Property(item => item.UpdatedAt).OriginalValue = expectedUpdatedAt;
    }

    private static void ConfigureIntegrations(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<IntegrationConnection>(builder =>
        {
            builder.ToTable("integration_connections");
            builder.Property(item => item.Name).HasMaxLength(120).IsRequired();
            builder.Property(item => item.CredentialKind).HasConversion<string>().HasMaxLength(40).IsRequired();
            builder.Property(item => item.AccessMode).HasConversion<string>().HasMaxLength(40).IsRequired();
            builder.Property(item => item.KeyId).HasMaxLength(64);
            builder.Property(item => item.SecretHash).HasMaxLength(128);
            builder.Property(item => item.TokenPrefix).HasMaxLength(32);
            builder.Property(item => item.OAuthAuthorizationId).HasMaxLength(160);
            builder.HasIndex(item => item.KeyId).IsUnique();
            builder.HasIndex(item => new { item.UserId, item.HouseholdId, item.ExpiresAt });
            builder.HasOne(item => item.User)
                .WithMany()
                .HasForeignKey(item => item.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(item => item.Household)
                .WithMany()
                .HasForeignKey(item => item.HouseholdId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<IntegrationAuditEvent>(builder =>
        {
            builder.ToTable("integration_audit_events");
            builder.Property(item => item.Surface).HasMaxLength(24).IsRequired();
            builder.Property(item => item.Operation).HasMaxLength(160).IsRequired();
            builder.Property(item => item.ResourceType).HasMaxLength(80);
            builder.Property(item => item.TraceId).HasMaxLength(160).IsRequired();
            builder.HasIndex(item => new { item.IntegrationConnectionId, item.CreatedAt });
            builder.HasIndex(item => item.CreatedAt);
            builder.HasOne(item => item.IntegrationConnection)
                .WithMany()
                .HasForeignKey(item => item.IntegrationConnectionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<IntegrationIdempotencyRecord>(builder =>
        {
            builder.ToTable("integration_idempotency_records");
            builder.Property(item => item.Operation).HasMaxLength(160).IsRequired();
            builder.Property(item => item.IdempotencyKey).HasMaxLength(128).IsRequired();
            builder.Property(item => item.RequestHash).HasMaxLength(128).IsRequired();
            builder.Property(item => item.ResponseJson).IsRequired();
            builder.HasIndex(item => new { item.IntegrationConnectionId, item.Operation, item.IdempotencyKey }).IsUnique();
            builder.HasIndex(item => item.ExpiresAt);
            builder.HasOne(item => item.IntegrationConnection)
                .WithMany()
                .HasForeignKey(item => item.IntegrationConnectionId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigurePlatform(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PlatformSettings>(builder =>
        {
            builder.ToTable("platform_settings");
            builder.Property(item => item.Key).HasMaxLength(40).IsRequired();
            builder.Property(item => item.AdminName).HasMaxLength(160).IsRequired();
            builder.Property(item => item.ContactEmail).HasMaxLength(320).IsRequired();
            builder.Property(item => item.ContactPhone).HasMaxLength(40).IsRequired();
            builder.Property(item => item.ManagementPhone).HasMaxLength(40).IsRequired();
            builder.Property(item => item.Instagram).HasMaxLength(160).IsRequired();
            builder.Property(item => item.AddressLine1).HasMaxLength(160).IsRequired();
            builder.Property(item => item.AddressLine2).HasMaxLength(160).IsRequired();
            builder.Property(item => item.City).HasMaxLength(120).IsRequired();
            builder.Property(item => item.State).HasMaxLength(80).IsRequired();
            builder.Property(item => item.PostalCode).HasMaxLength(20).IsRequired();
            builder.HasIndex(item => item.Key).IsUnique();
        });

        modelBuilder.Entity<ToolImprovementSuggestion>(builder =>
        {
            builder.ToTable("tool_improvement_suggestions");
            builder.Property(item => item.SubmittedAt).IsRequired();
            builder.Property(item => item.SuggestionText).HasMaxLength(8000).IsRequired();
            builder.Property(item => item.Status).HasConversion<string>().HasMaxLength(40).IsRequired();
            builder.Property(item => item.Priority).HasConversion<string>().HasMaxLength(40).IsRequired();
            builder.Property(item => item.InternalComment).HasMaxLength(4000);
            builder.HasIndex(item => item.SubmittedAt);
            builder.HasIndex(item => item.Status);
            builder.HasIndex(item => item.Priority);
            builder.HasIndex(item => item.UserId);
            builder.HasOne(item => item.User)
                .WithMany()
                .HasForeignKey(item => item.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(item => item.LastReviewedByUser)
                .WithMany()
                .HasForeignKey(item => item.LastReviewedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }

    private static void ConfigurePlans(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PlanDefinition>(builder =>
        {
            builder.ToTable("plan_definitions");
            builder.Property(item => item.Slug).HasMaxLength(40).IsRequired();
            builder.Property(item => item.Name).HasMaxLength(80).IsRequired();
            builder.Property(item => item.CurrencyCode).HasMaxLength(3).IsRequired();
            builder.Property(item => item.MonthlyPrice).HasPrecision(10, 2);
            builder.Property(item => item.AnnualPrice).HasPrecision(10, 2);
            builder.Property(item => item.ShowInCatalog).HasDefaultValue(true);
            builder.Property(item => item.IsPopular).HasDefaultValue(false);
            builder.HasIndex(item => item.Slug).IsUnique();
        });

        modelBuilder.Entity<UserSubscription>(builder =>
        {
            builder.ToTable("user_subscriptions");
            builder.Property(item => item.BillingCycle).HasConversion<string>().HasMaxLength(40).IsRequired();
            builder.Property(item => item.AmountPaid).HasPrecision(10, 2);
            builder.Property(item => item.CurrencyCode).HasMaxLength(3).IsRequired();
            builder.Property(item => item.Status).HasConversion<string>().HasMaxLength(40).IsRequired();
            builder.Property(item => item.AdminNote).HasMaxLength(4000);
            builder.HasIndex(item => new { item.UserId, item.StartsAt });
            builder.HasIndex(item => new { item.UserId, item.EndsAt });
            builder.HasOne(item => item.User)
                .WithMany(user => user.Subscriptions)
                .HasForeignKey(item => item.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(item => item.PlanDefinition)
                .WithMany(plan => plan.Subscriptions)
                .HasForeignKey(item => item.PlanDefinitionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<UserPlanImageAsset>(builder =>
        {
            builder.ToTable("user_plan_image_assets");
            builder.Property(item => item.Module).HasConversion<string>().HasMaxLength(40).IsRequired();
            builder.Property(item => item.ObjectKey).HasMaxLength(512).IsRequired();
            builder.Property(item => item.ContentType).HasMaxLength(120).IsRequired();
            builder.HasIndex(item => new { item.Module, item.EntityId }).IsUnique();
            builder.HasIndex(item => new { item.UserId, item.UploadedAt });
            builder.HasOne(item => item.User)
                .WithMany(user => user.PlanImageAssets)
                .HasForeignKey(item => item.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureGsm(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<GsmNumber>(builder =>
        {
            builder.ToTable("gsm_numbers");
            builder.Property(item => item.Title).HasMaxLength(160).IsRequired();
            builder.Property(item => item.NormalizedNumber).HasMaxLength(13).IsRequired();
            builder.Property(item => item.Description).HasMaxLength(4000);
            builder.Property(item => item.Plan).HasConversion<string>().HasMaxLength(40).IsRequired();
            builder.Property(item => item.MonthlyCost).HasPrecision(10, 2);
            builder.Property(item => item.DaysWithoutRecharge);
            builder.Property(item => item.AcquiredOn).HasColumnType("date");
            builder.Property(item => item.LastRechargeOn).HasColumnType("date");
            builder.Property(item => item.Status).HasConversion<string>().HasMaxLength(40).IsRequired();
            builder.HasIndex(item => new { item.HouseholdId, item.NormalizedNumber }).IsUnique();
            builder.HasOne(item => item.Household)
                .WithMany(household => household.GsmNumbers)
                .HasForeignKey(item => item.HouseholdId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(item => item.CreatedByMember)
                .WithMany(member => member.CreatedGsmNumbers)
                .HasForeignKey(item => item.CreatedByMemberId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<GsmRecharge>(builder =>
        {
            builder.ToTable("gsm_recharges");
            builder.Property(item => item.RechargedOn).HasColumnType("date");
            builder.Property(item => item.Amount).HasPrecision(10, 2);
            builder.Property(item => item.Note).HasMaxLength(4000);
            builder.HasIndex(item => new { item.HouseholdId, item.GsmNumberId, item.RechargedOn });
            builder.HasOne(item => item.Household)
                .WithMany(household => household.GsmRecharges)
                .HasForeignKey(item => item.HouseholdId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(item => item.GsmNumber)
                .WithMany(number => number.Recharges)
                .HasForeignKey(item => item.GsmNumberId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(item => item.CreatedByMember)
                .WithMany(member => member.CreatedGsmRecharges)
                .HasForeignKey(item => item.CreatedByMemberId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }

    private static void ConfigureFinance(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FinanceCategory>(builder =>
        {
            builder.ToTable("finance_categories");
            builder.Property(item => item.Name).HasMaxLength(160).IsRequired();
            builder.Property(item => item.IsDefault).HasDefaultValue(false);
            builder.HasIndex(item => new { item.HouseholdId, item.Name }).IsUnique();
            builder.HasIndex(item => new { item.HouseholdId, item.SortOrder });
            builder.HasOne(item => item.Household)
                .WithMany(household => household.FinanceCategories)
                .HasForeignKey(item => item.HouseholdId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(item => item.CreatedByMember)
                .WithMany(member => member.CreatedFinanceCategories)
                .HasForeignKey(item => item.CreatedByMemberId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<FinancePeriod>(builder =>
        {
            builder.ToTable("finance_periods");
            builder.HasIndex(period => new { period.HouseholdId, period.Year, period.Month }).IsUnique();
            builder.HasOne(period => period.Household)
                .WithMany(household => household.FinancePeriods)
                .HasForeignKey(period => period.HouseholdId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<FinanceRecurringTemplate>(builder =>
        {
            builder.ToTable("finance_recurring_templates");
            builder.Property(item => item.Title).HasMaxLength(200).IsRequired();
            builder.Property(item => item.Notes).HasMaxLength(4000);
            builder.Property(item => item.Type).HasConversion<string>().HasMaxLength(40).IsRequired();
            builder.Property(item => item.DefaultAmount).HasPrecision(10, 2);
            builder.Property(item => item.Recurrence).HasConversion<string>().HasMaxLength(40).IsRequired();
            builder.Property(item => item.IsActive).HasDefaultValue(true);
            builder.HasIndex(item => new { item.HouseholdId, item.IsActive, item.Recurrence });
            builder.HasOne(item => item.Household)
                .WithMany(household => household.FinanceRecurringTemplates)
                .HasForeignKey(item => item.HouseholdId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(item => item.CreatedByMember)
                .WithMany(member => member.CreatedFinanceRecurringTemplates)
                .HasForeignKey(item => item.CreatedByMemberId)
                .OnDelete(DeleteBehavior.SetNull);
            builder.HasOne(item => item.Universe)
                .WithMany()
                .HasForeignKey(item => item.UniverseId)
                .OnDelete(DeleteBehavior.SetNull);
            builder.HasOne(item => item.Project)
                .WithMany()
                .HasForeignKey(item => item.ProjectId)
                .OnDelete(DeleteBehavior.SetNull);
            builder.HasOne(item => item.Category)
                .WithMany(category => category.RecurringTemplates)
                .HasForeignKey(item => item.CategoryId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<FinanceEntry>(builder =>
        {
            builder.ToTable("finance_entries");
            builder.Property(item => item.Title).HasMaxLength(240).IsRequired();
            builder.Property(item => item.Notes).HasMaxLength(4000);
            builder.Property(item => item.Amount).HasPrecision(10, 2);
            builder.Property(item => item.Type).HasConversion<string>().HasMaxLength(40).IsRequired();
            builder.Property(item => item.ReferenceDate).HasColumnType("date");
            builder.Property(item => item.Origin).HasConversion<string>().HasMaxLength(40).IsRequired();
            builder.HasIndex(item => new { item.HouseholdId, item.FinancePeriodId, item.ReferenceDate });
            builder.HasIndex(item => item.CreditCardStatementId).IsUnique();
            builder.HasOne(item => item.Household)
                .WithMany(household => household.FinanceEntries)
                .HasForeignKey(item => item.HouseholdId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(item => item.FinancePeriod)
                .WithMany(period => period.Entries)
                .HasForeignKey(item => item.FinancePeriodId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(item => item.CreatedByMember)
                .WithMany(member => member.CreatedFinanceEntries)
                .HasForeignKey(item => item.CreatedByMemberId)
                .OnDelete(DeleteBehavior.SetNull);
            builder.HasOne(item => item.RecurringTemplate)
                .WithMany(template => template.Entries)
                .HasForeignKey(item => item.RecurringTemplateId)
                .OnDelete(DeleteBehavior.SetNull);
            builder.HasOne(item => item.CreditCardStatement)
                .WithOne(statement => statement.FinanceEntry)
                .HasForeignKey<FinanceEntry>(item => item.CreditCardStatementId)
                .OnDelete(DeleteBehavior.SetNull);
            builder.HasOne(item => item.Universe)
                .WithMany()
                .HasForeignKey(item => item.UniverseId)
                .OnDelete(DeleteBehavior.SetNull);
            builder.HasOne(item => item.Project)
                .WithMany()
                .HasForeignKey(item => item.ProjectId)
                .OnDelete(DeleteBehavior.SetNull);
            builder.HasOne(item => item.Category)
                .WithMany(category => category.Entries)
                .HasForeignKey(item => item.CategoryId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Asset>(builder =>
        {
            builder.ToTable("assets");
            builder.Property(item => item.Title).HasMaxLength(200).IsRequired();
            builder.Property(item => item.Type).HasConversion<string>().HasMaxLength(40).IsRequired();
            builder.Property(item => item.CurrentValue).HasPrecision(12, 2);
            builder.Property(item => item.RemainingDebt).HasPrecision(12, 2);
            builder.Property(item => item.Notes).HasMaxLength(4000);
            builder.HasIndex(item => new { item.HouseholdId, item.Type, item.Title });
            builder.HasOne(item => item.Household)
                .WithMany(household => household.Assets)
                .HasForeignKey(item => item.HouseholdId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(item => item.CreatedByMember)
                .WithMany(member => member.CreatedAssets)
                .HasForeignKey(item => item.CreatedByMemberId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<AssetPropertyDetails>(builder =>
        {
            builder.ToTable("asset_property_details");
            builder.HasKey(item => item.AssetId);
            builder.Property(item => item.RegistryNumber).HasMaxLength(160);
            builder.Property(item => item.PropertyInscription).HasMaxLength(160);
            builder.Property(item => item.PrivateAreaSquareMeters).HasPrecision(8, 2);
            builder.Property(item => item.DebtCheckOn).HasColumnType("date");
            builder.HasOne(item => item.Asset)
                .WithOne(asset => asset.PropertyDetails)
                .HasForeignKey<AssetPropertyDetails>(item => item.AssetId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AssetVehicleDetails>(builder =>
        {
            builder.ToTable("asset_vehicle_details");
            builder.HasKey(item => item.AssetId);
            builder.Property(item => item.Brand).HasMaxLength(120);
            builder.Property(item => item.Model).HasMaxLength(160);
            builder.Property(item => item.YearModel).HasMaxLength(80);
            builder.Property(item => item.Renavam).HasMaxLength(40);
            builder.HasOne(item => item.Asset)
                .WithOne(asset => asset.VehicleDetails)
                .HasForeignKey<AssetVehicleDetails>(item => item.AssetId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AssetValuation>(builder =>
        {
            builder.ToTable("asset_valuations");
            builder.Property(item => item.Label).HasMaxLength(120).IsRequired();
            builder.Property(item => item.Amount).HasPrecision(12, 2);
            builder.Property(item => item.Notes).HasMaxLength(4000);
            builder.HasIndex(item => new { item.AssetId, item.ReferenceYear, item.Label }).IsUnique();
            builder.HasOne(item => item.Asset)
                .WithMany(asset => asset.Valuations)
                .HasForeignKey(item => item.AssetId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CreditCardAccount>(builder =>
        {
            builder.ToTable("credit_card_accounts");
            builder.Property(item => item.Name).HasMaxLength(160).IsRequired();
            builder.Property(item => item.Brand).HasMaxLength(120);
            builder.Property(item => item.LastFourDigits).HasMaxLength(4);
            builder.Property(item => item.Notes).HasMaxLength(4000);
            builder.Property(item => item.IsActive).HasDefaultValue(true);
            builder.HasIndex(item => new { item.HouseholdId, item.Name }).IsUnique();
            builder.HasOne(item => item.Household)
                .WithMany(household => household.CreditCardAccounts)
                .HasForeignKey(item => item.HouseholdId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(item => item.CreatedByMember)
                .WithMany(member => member.CreatedCreditCardAccounts)
                .HasForeignKey(item => item.CreatedByMemberId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<CreditCardStatement>(builder =>
        {
            builder.ToTable("credit_card_statements");
            builder.Property(item => item.ClosingDate).HasColumnType("date");
            builder.Property(item => item.DueDate).HasColumnType("date");
            builder.Property(item => item.TotalAmount).HasPrecision(10, 2);
            builder.Property(item => item.Notes).HasMaxLength(4000);
            builder.Property(item => item.ExternalSource).HasMaxLength(120);
            builder.Property(item => item.ExternalReference).HasMaxLength(240);
            builder.HasIndex(item => new { item.CreditCardAccountId, item.DueDate });
            builder.HasOne(item => item.Household)
                .WithMany(household => household.CreditCardStatements)
                .HasForeignKey(item => item.HouseholdId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(item => item.CreditCardAccount)
                .WithMany(account => account.Statements)
                .HasForeignKey(item => item.CreditCardAccountId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(item => item.CreatedByMember)
                .WithMany(member => member.CreatedCreditCardStatements)
                .HasForeignKey(item => item.CreatedByMemberId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<CreditCardTransaction>(builder =>
        {
            builder.ToTable("credit_card_transactions");
            builder.Property(item => item.Title).HasMaxLength(200).IsRequired();
            builder.Property(item => item.Merchant).HasMaxLength(160);
            builder.Property(item => item.Amount).HasPrecision(10, 2);
            builder.Property(item => item.PurchasedOn).HasColumnType("date");
            builder.Property(item => item.Notes).HasMaxLength(4000);
            builder.Property(item => item.ExternalSource).HasMaxLength(120);
            builder.Property(item => item.ExternalReference).HasMaxLength(240);
            builder.HasIndex(item => new { item.CreditCardAccountId, item.PurchasedOn });
            builder.HasIndex(item => item.CreditCardStatementId);
            builder.HasOne(item => item.Household)
                .WithMany(household => household.CreditCardTransactions)
                .HasForeignKey(item => item.HouseholdId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(item => item.CreditCardAccount)
                .WithMany(account => account.Transactions)
                .HasForeignKey(item => item.CreditCardAccountId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(item => item.CreditCardStatement)
                .WithMany(statement => statement.Transactions)
                .HasForeignKey(item => item.CreditCardStatementId)
                .OnDelete(DeleteBehavior.SetNull);
            builder.HasOne(item => item.CreatedByMember)
                .WithMany(member => member.CreatedCreditCardTransactions)
                .HasForeignKey(item => item.CreatedByMemberId)
                .OnDelete(DeleteBehavior.SetNull);
            builder.HasOne(item => item.Universe)
                .WithMany()
                .HasForeignKey(item => item.UniverseId)
                .OnDelete(DeleteBehavior.SetNull);
            builder.HasOne(item => item.Project)
                .WithMany()
                .HasForeignKey(item => item.ProjectId)
                .OnDelete(DeleteBehavior.SetNull);
            builder.HasOne(item => item.Category)
                .WithMany(category => category.CreditCardTransactions)
                .HasForeignKey(item => item.CategoryId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }

    private static void ConfigureProjects(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Universe>(builder =>
        {
            builder.ToTable("universes");
            builder.Property(universe => universe.Name).HasMaxLength(160).IsRequired();
            builder.Property(universe => universe.ImageUrl).HasMaxLength(2000);
            builder.Property(universe => universe.ImageObjectKey).HasMaxLength(512);
            builder.Property(universe => universe.ImageContentType).HasMaxLength(120);
            builder.HasIndex(universe => new { universe.HouseholdId, universe.Name }).IsUnique();
            builder.HasOne(universe => universe.Household)
                .WithMany(household => household.Universes)
                .HasForeignKey(universe => universe.HouseholdId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(universe => universe.CreatedByMember)
                .WithMany(member => member.CreatedUniverses)
                .HasForeignKey(universe => universe.CreatedByMemberId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Project>(builder =>
        {
            builder.ToTable("projects");
            builder.Property(project => project.Name).HasMaxLength(200).IsRequired();
            builder.HasIndex(project => new { project.HouseholdId, project.UniverseId, project.Name }).IsUnique();
            builder.HasOne(project => project.Universe)
                .WithMany(universe => universe.Projects)
                .HasForeignKey(project => project.UniverseId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(project => project.CreatedByMember)
                .WithMany(member => member.CreatedProjects)
                .HasForeignKey(project => project.CreatedByMemberId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<MemberEffortAllocation>(builder =>
        {
            builder.ToTable("member_effort_allocations", tableBuilder =>
            {
                tableBuilder.HasCheckConstraint("CK_member_effort_allocations_points_non_negative", "\"Points\" >= 0");
                tableBuilder.HasCheckConstraint(
                    "CK_member_effort_allocations_scope",
                    "(\"ScopeType\" = 'Household' AND \"UniverseId\" IS NULL AND \"ProjectId\" IS NULL) OR (\"ScopeType\" = 'Universe' AND \"UniverseId\" IS NOT NULL AND \"ProjectId\" IS NULL) OR (\"ScopeType\" = 'Project' AND \"UniverseId\" IS NULL AND \"ProjectId\" IS NOT NULL)");
            });
            builder.Property(item => item.ScopeType).HasConversion<string>().HasMaxLength(20).IsRequired();
            builder.Property(item => item.Weekday).HasConversion<string>().HasMaxLength(20).IsRequired();
            builder.Property(item => item.Points).HasPrecision(8, 2);
            builder.HasIndex(item => new { item.HouseholdMemberId, item.Weekday })
                .IsUnique()
                .HasFilter("\"ScopeType\" = 'Household'");
            builder.HasIndex(item => new { item.HouseholdMemberId, item.UniverseId, item.Weekday })
                .IsUnique()
                .HasFilter("\"ScopeType\" = 'Universe'");
            builder.HasIndex(item => new { item.HouseholdMemberId, item.ProjectId, item.Weekday })
                .IsUnique()
                .HasFilter("\"ScopeType\" = 'Project'");
            builder.HasOne(item => item.Household)
                .WithMany(household => household.MemberEffortAllocations)
                .HasForeignKey(item => item.HouseholdId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(item => item.HouseholdMember)
                .WithMany(member => member.EffortAllocations)
                .HasForeignKey(item => item.HouseholdMemberId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(item => item.Universe)
                .WithMany(universe => universe.EffortAllocations)
                .HasForeignKey(item => item.UniverseId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(item => item.Project)
                .WithMany(project => project.EffortAllocations)
                .HasForeignKey(item => item.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Activity>(builder =>
        {
            builder.ToTable("activities");
            builder.Property(activity => activity.Title).HasMaxLength(240).IsRequired();
            builder.Property(activity => activity.Description).HasMaxLength(4000);
            builder.Property(activity => activity.ImageObjectKey).HasMaxLength(512);
            builder.Property(activity => activity.ImageContentType).HasMaxLength(120);
            builder.Property(activity => activity.DueDate).HasColumnType("date");
            builder.Property(activity => activity.CompletedAt).HasColumnType("timestamp with time zone");
            builder.Property(activity => activity.Status).HasConversion<string>().HasMaxLength(40).IsRequired();
            builder.Property(activity => activity.Priority).HasConversion<string>().HasMaxLength(40).IsRequired();
            builder.Property(activity => activity.Size).HasPrecision(8, 2);
            builder.HasOne(activity => activity.Project)
                .WithMany(project => project.Activities)
                .HasForeignKey(activity => activity.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(activity => activity.ResponsibleMember)
                .WithMany(member => member.AssignedActivities)
                .HasForeignKey(activity => activity.ResponsibleMemberId)
                .OnDelete(DeleteBehavior.SetNull);
            builder.HasOne(activity => activity.CreatedByMember)
                .WithMany(member => member.CreatedActivities)
                .HasForeignKey(activity => activity.CreatedByMemberId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ActivityComment>(builder =>
        {
            builder.ToTable("activity_comments");
            builder.Property(comment => comment.Body).HasMaxLength(4000).IsRequired();
            builder.HasIndex(comment => new { comment.HouseholdId, comment.ActivityId, comment.CreatedAt });
            builder.HasOne(comment => comment.Activity)
                .WithMany(activity => activity.Comments)
                .HasForeignKey(comment => comment.ActivityId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(comment => comment.AuthorMember)
                .WithMany(member => member.AuthoredActivityComments)
                .HasForeignKey(comment => comment.AuthorMemberId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PendingItem>(builder =>
        {
            builder.ToTable("pending_items");
            builder.Property(item => item.Title).HasMaxLength(240).IsRequired();
            builder.Property(item => item.Description).HasMaxLength(4000);
            builder.Property(item => item.Priority).HasConversion<string>().HasMaxLength(40).IsRequired();
            builder.HasOne(item => item.Activity)
                .WithMany(activity => activity.PendingItems)
                .HasForeignKey(item => item.ActivityId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureNotifications(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<NotificationPreference>(builder =>
        {
            builder.ToTable("notification_preferences");
            builder.Property(preference => preference.WhatsAppPhoneNumber).HasMaxLength(40);
            builder.Property(preference => preference.TimeZoneId).HasMaxLength(80).IsRequired();
            builder.HasIndex(preference => preference.HouseholdMemberId).IsUnique();
            builder.HasOne(preference => preference.Household)
                .WithMany()
                .HasForeignKey(preference => preference.HouseholdId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(preference => preference.HouseholdMember)
                .WithOne(member => member.NotificationPreference)
                .HasForeignKey<NotificationPreference>(preference => preference.HouseholdMemberId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<NotificationRun>(builder =>
        {
            builder.ToTable("notification_runs");
            builder.Property(run => run.Kind).HasMaxLength(80).IsRequired();
            builder.Property(run => run.ProviderMessageId).HasMaxLength(160).IsRequired();
            builder.HasIndex(run => new { run.HouseholdId, run.HouseholdMemberId, run.Kind, run.LocalDate }).IsUnique();
            builder.HasOne(run => run.Household)
                .WithMany(household => household.NotificationRuns)
                .HasForeignKey(run => run.HouseholdId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigurePrompts(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Prompt>(builder =>
        {
            builder.ToTable("prompts", tableBuilder =>
                tableBuilder.HasCheckConstraint(
                    "CK_prompts_link_url_title_pair",
                    """
                    ("LinkUrl" IS NULL AND "LinkTitle" IS NULL)
                    OR
                    ("LinkUrl" IS NOT NULL AND "LinkTitle" IS NOT NULL)
                    """));
            builder.Property(prompt => prompt.Title).HasMaxLength(240).IsRequired();
            builder.Property(prompt => prompt.Description).HasMaxLength(4000);
            builder.Property(prompt => prompt.PromptText).HasMaxLength(20000).IsRequired();
            builder.Property(prompt => prompt.LinkUrl).HasMaxLength(2000);
            builder.Property(prompt => prompt.LinkTitle).HasMaxLength(240);
            builder.Property(prompt => prompt.IsArchived).HasDefaultValue(false);
            builder.Property(prompt => prompt.ImageObjectKey).HasMaxLength(512);
            builder.Property(prompt => prompt.ImageContentType).HasMaxLength(120);
            builder.HasIndex(prompt => new { prompt.HouseholdId, prompt.IsArchived, prompt.UpdatedAt });
            builder.HasIndex(prompt => new { prompt.HouseholdId, prompt.IsArchived, prompt.UniverseId, prompt.UpdatedAt });
            builder.HasOne(prompt => prompt.Household)
                .WithMany(household => household.Prompts)
                .HasForeignKey(prompt => prompt.HouseholdId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(prompt => prompt.CreatedByMember)
                .WithMany(member => member.CreatedPrompts)
                .HasForeignKey(prompt => prompt.CreatedByMemberId)
                .OnDelete(DeleteBehavior.SetNull);
            builder.HasOne(prompt => prompt.Universe)
                .WithMany(universe => universe.Prompts)
                .HasForeignKey(prompt => prompt.UniverseId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<PromptCategory>(builder =>
        {
            builder.ToTable("prompt_categories");
            builder.Property(category => category.Name).HasMaxLength(160).IsRequired();
            builder.HasIndex(category => new { category.HouseholdId, category.Name }).IsUnique();
            builder.HasOne(category => category.Household)
                .WithMany(household => household.PromptCategories)
                .HasForeignKey(category => category.HouseholdId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(category => category.CreatedByMember)
                .WithMany(member => member.CreatedPromptCategories)
                .HasForeignKey(category => category.CreatedByMemberId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<PromptCategoryAssignment>(builder =>
        {
            builder.ToTable("prompt_category_assignments");
            builder.HasKey(assignment => new { assignment.PromptId, assignment.CategoryId });
            builder.HasIndex(assignment => new { assignment.PromptId, assignment.CategoryId }).IsUnique();
            builder.HasOne(assignment => assignment.Prompt)
                .WithMany(prompt => prompt.CategoryAssignments)
                .HasForeignKey(assignment => assignment.PromptId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(assignment => assignment.Category)
                .WithMany(category => category.PromptAssignments)
                .HasForeignKey(assignment => assignment.CategoryId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
