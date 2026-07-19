using OrganizaClub.Domain.Common;
using OrganizaClub.Domain.Spaces;

namespace OrganizaClub.Domain.Projects;

public sealed class Activity : AuditableEntity, ISpaceScoped
{
    public Guid SpaceId { get; set; }

    public Guid ProjectId { get; set; }
    public Project? Project { get; set; }

    public Guid? ResponsibleMemberId { get; set; }
    public SpaceMember? ResponsibleMember { get; set; }

    public Guid? CreatedByMemberId { get; set; }
    public SpaceMember? CreatedByMember { get; set; }

    public required string Title { get; set; }
    public string? Description { get; set; }
    public string? ImageObjectKey { get; set; }
    public string? ImageContentType { get; set; }
    public DateTimeOffset? ImageUpdatedAt { get; set; }
    public DateOnly? DueDate { get; set; }
    // Registra a última entrada na coluna concluída; o serviço limpa ao reabrir.
    public DateTimeOffset? CompletedAt { get; set; }
    public ActivityStatus Status { get; set; } = ActivityStatus.NaoIniciada;
    public Priority Priority { get; set; } = Priority.Media;
    public decimal? Size { get; set; }

    public ICollection<PendingItem> PendingItems { get; } = new List<PendingItem>();
    public ICollection<ActivityComment> Comments { get; } = new List<ActivityComment>();
}
