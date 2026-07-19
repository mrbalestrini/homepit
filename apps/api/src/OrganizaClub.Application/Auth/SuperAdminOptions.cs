namespace OrganizaClub.Application.Auth;

public sealed class SuperAdminOptions
{
    public const string SectionName = "SuperAdmin";

    public string? Email { get; set; }
    public string? Password { get; set; }
    public string? DisplayName { get; set; }
    public string? SupportEmail => string.IsNullOrWhiteSpace(Email) ? null : Email.Trim();

    public bool IsEnabled =>
        !string.IsNullOrWhiteSpace(Email) &&
        !string.IsNullOrWhiteSpace(Password);
}
