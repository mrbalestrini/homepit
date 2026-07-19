using OrganizaClub.Domain.Common;
using OrganizaClub.Domain.Spaces;

namespace OrganizaClub.Domain.Finance;

public sealed class FinancePeriod : AuditableEntity, ISpaceScoped
{
    public Guid SpaceId { get; set; }
    public Space? Space { get; set; }

    public int Year { get; set; }
    public int Month { get; set; }

    public ICollection<FinanceEntry> Entries { get; } = new List<FinanceEntry>();
}
