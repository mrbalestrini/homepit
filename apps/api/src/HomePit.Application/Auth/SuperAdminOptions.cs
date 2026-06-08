namespace HomePit.Application.Auth;

public sealed class SuperAdminOptions
{
    public const string SectionName = "SuperAdmin";

    public string? Email { get; set; }
    public string? Password { get; set; }
    public string? DisplayName { get; set; }

    public bool IsEnabled =>
        !string.IsNullOrWhiteSpace(Email) &&
        !string.IsNullOrWhiteSpace(Password);
}
