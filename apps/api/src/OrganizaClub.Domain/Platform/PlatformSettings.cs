using OrganizaClub.Domain.Common;

namespace OrganizaClub.Domain.Platform;

public sealed class PlatformSettings : AuditableEntity
{
    public string Key { get; set; } = "platform";
    public string AdminName { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public string ContactPhone { get; set; } = string.Empty;
    public string ManagementPhone { get; set; } = string.Empty;
    public string Instagram { get; set; } = string.Empty;
    public string AddressLine1 { get; set; } = string.Empty;
    public string AddressLine2 { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
}
