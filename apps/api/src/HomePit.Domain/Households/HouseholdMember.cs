using HomePit.Domain.Common;
using HomePit.Domain.Notifications;
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
    public ICollection<ActivityComment> AuthoredActivityComments { get; } = new List<ActivityComment>();
    public NotificationPreference? NotificationPreference { get; set; }
}
