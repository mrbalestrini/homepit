using HomePit.Domain.Common;

namespace HomePit.Domain.Institutional;

public sealed class InstitutionalBenefit : AuditableEntity
{
    public Guid InstitutionalPageId { get; set; }
    public InstitutionalPage? InstitutionalPage { get; set; }
    public int Position { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
