using HomePit.Application.Common;
using HomePit.Domain.Common;
using HomePit.Domain.Gsm;
using HomePit.Domain.Households;
using HomePit.Domain.Institutional;
using HomePit.Domain.Notifications;
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
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<InstitutionalPage> InstitutionalPages => Set<InstitutionalPage>();
    public DbSet<InstitutionalBenefit> InstitutionalBenefits => Set<InstitutionalBenefit>();
    public DbSet<InstitutionalStep> InstitutionalSteps => Set<InstitutionalStep>();
    public DbSet<GsmNumber> GsmNumbers => Set<GsmNumber>();
    public DbSet<GsmRecharge> GsmRecharges => Set<GsmRecharge>();
    public DbSet<Universe> Universes => Set<Universe>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Activity> Activities => Set<Activity>();
    public DbSet<ActivityComment> ActivityComments => Set<ActivityComment>();
    public DbSet<PendingItem> PendingItems => Set<PendingItem>();
    public DbSet<Prompt> Prompts => Set<Prompt>();
    public DbSet<PromptCategory> PromptCategories => Set<PromptCategory>();
    public DbSet<PromptCategoryAssignment> PromptCategoryAssignments => Set<PromptCategoryAssignment>();
    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();
    public DbSet<NotificationRun> NotificationRuns => Set<NotificationRun>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("homepit");

        ConfigureHouseholds(modelBuilder);
        ConfigureGsm(modelBuilder);
        ConfigureInstitutional(modelBuilder);
        ConfigureProjects(modelBuilder);
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
            builder.HasIndex(user => user.Email).IsUnique();
        });

        modelBuilder.Entity<Household>(builder =>
        {
            builder.ToTable("households");
            builder.Property(household => household.Name).HasMaxLength(160).IsRequired();
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

        modelBuilder.Entity<Activity>(builder =>
        {
            builder.ToTable("activities");
            builder.Property(activity => activity.Title).HasMaxLength(240).IsRequired();
            builder.Property(activity => activity.Description).HasMaxLength(4000);
            builder.Property(activity => activity.ImageObjectKey).HasMaxLength(512);
            builder.Property(activity => activity.ImageContentType).HasMaxLength(120);
            builder.Property(activity => activity.DueDate).HasColumnType("date");
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
