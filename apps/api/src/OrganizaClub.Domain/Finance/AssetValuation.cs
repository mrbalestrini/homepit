using OrganizaClub.Domain.Common;

namespace OrganizaClub.Domain.Finance;

public sealed class AssetValuation : AuditableEntity
{
    public Guid AssetId { get; set; }
    public Asset? Asset { get; set; }

    public int ReferenceYear { get; set; }
    public required string Label { get; set; }
    public decimal Amount { get; set; }
    public string? Notes { get; set; }
}
