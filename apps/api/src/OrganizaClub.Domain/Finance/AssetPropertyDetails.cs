namespace OrganizaClub.Domain.Finance;

public sealed class AssetPropertyDetails
{
    public Guid AssetId { get; set; }
    public Asset? Asset { get; set; }

    public string? RegistryNumber { get; set; }
    public string? PropertyInscription { get; set; }
    public decimal? PrivateAreaSquareMeters { get; set; }
    public DateOnly? DebtCheckOn { get; set; }
}
