namespace HomePit.Application.Platform;

public sealed record PlatformSettingsDto(
    string AdminName,
    string ContactEmail,
    string ContactPhone,
    string ManagementPhone,
    string Instagram,
    string AddressLine1,
    string AddressLine2,
    string City,
    string State,
    string PostalCode,
    bool CanShowAddressOnLanding);

public sealed record PublicPlatformSettingsDto(
    string ContactEmail,
    string ContactPhone,
    string Instagram,
    string AddressLine1,
    string AddressLine2,
    string City,
    string State,
    string PostalCode,
    bool CanShowAddressOnLanding);

public sealed record UpdatePlatformSettingsRequest(
    string AdminName,
    string ContactEmail,
    string ContactPhone,
    string ManagementPhone,
    string Instagram,
    string AddressLine1,
    string AddressLine2,
    string City,
    string State,
    string PostalCode);
