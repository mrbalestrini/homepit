using System.Net.Mail;
using OrganizaClub.Application.Common;
using OrganizaClub.Domain.Platform;
using OrganizaClub.Domain.Spaces;
using Microsoft.EntityFrameworkCore;

namespace OrganizaClub.Application.Platform;

public sealed class PlatformSettingsService(
    IOrganizaClubDbContext db,
    IUserContext userContext)
{
    private const string SettingsKey = "platform";

    public async Task<PublicPlatformSettingsDto> GetPublicAsync(CancellationToken cancellationToken)
    {
        var settings = await FindSettingsAsync(asTracking: false, cancellationToken);
        return settings is null ? CreateDefaultPublicDto() : ToPublicDto(settings);
    }

    public async Task<PlatformSettingsDto> GetAdminAsync(CancellationToken cancellationToken)
    {
        EnsureSuperAdmin();
        var settings = await FindSettingsAsync(asTracking: false, cancellationToken);
        return settings is null ? CreateDefaultAdminDto() : ToAdminDto(settings);
    }

    public async Task<PlatformSettingsDto> UpdateAsync(
        UpdatePlatformSettingsRequest request,
        CancellationToken cancellationToken)
    {
        EnsureSuperAdmin();
        var normalized = Normalize(request);
        var settings = await FindSettingsAsync(asTracking: true, cancellationToken);

        if (settings is null)
        {
            settings = new PlatformSettings { Key = SettingsKey };
            db.PlatformSettings.Add(settings);
        }

        Apply(settings, normalized);
        await db.SaveChangesAsync(cancellationToken);

        return ToAdminDto(settings);
    }

    private async Task<PlatformSettings?> FindSettingsAsync(bool asTracking, CancellationToken cancellationToken)
    {
        var query = db.PlatformSettings.Where(item => item.Key == SettingsKey);
        if (!asTracking)
        {
            query = query.AsNoTracking();
        }

        return await query.FirstOrDefaultAsync(cancellationToken);
    }

    private static PlatformSettingsDto ToAdminDto(PlatformSettings settings)
    {
        return new PlatformSettingsDto(
            settings.AdminName,
            settings.ContactEmail,
            settings.ContactPhone,
            settings.ManagementPhone,
            settings.Instagram,
            settings.AddressLine1,
            settings.AddressLine2,
            settings.City,
            settings.State,
            settings.PostalCode,
            HasCompleteAddress(settings));
    }

    private static PublicPlatformSettingsDto ToPublicDto(PlatformSettings settings)
    {
        return new PublicPlatformSettingsDto(
            settings.ContactEmail,
            settings.ContactPhone,
            settings.Instagram,
            settings.AddressLine1,
            settings.AddressLine2,
            settings.City,
            settings.State,
            settings.PostalCode,
            HasCompleteAddress(settings));
    }

    private static PlatformSettingsDto CreateDefaultAdminDto() =>
        new(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, false);

    private static PublicPlatformSettingsDto CreateDefaultPublicDto() =>
        new(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, false);

    private static bool HasCompleteAddress(PlatformSettings settings)
    {
        return !string.IsNullOrWhiteSpace(settings.AddressLine1)
            && !string.IsNullOrWhiteSpace(settings.AddressLine2)
            && !string.IsNullOrWhiteSpace(settings.City)
            && !string.IsNullOrWhiteSpace(settings.State)
            && !string.IsNullOrWhiteSpace(settings.PostalCode);
    }

    private static NormalizedPlatformSettings Normalize(UpdatePlatformSettingsRequest request)
    {
        return new NormalizedPlatformSettings(
            NormalizeOptionalText(request.AdminName, 160),
            NormalizeOptionalEmail(request.ContactEmail),
            NormalizeOptionalText(request.ContactPhone, 40),
            NormalizeOptionalText(request.ManagementPhone, 40),
            NormalizeOptionalText(request.Instagram, 160),
            NormalizeOptionalText(request.AddressLine1, 160),
            NormalizeOptionalText(request.AddressLine2, 160),
            NormalizeOptionalText(request.City, 120),
            NormalizeOptionalText(request.State, 80),
            NormalizeOptionalText(request.PostalCode, 20));
    }

    private static void Apply(PlatformSettings settings, NormalizedPlatformSettings normalized)
    {
        settings.AdminName = normalized.AdminName;
        settings.ContactEmail = normalized.ContactEmail;
        settings.ContactPhone = normalized.ContactPhone;
        settings.ManagementPhone = normalized.ManagementPhone;
        settings.Instagram = normalized.Instagram;
        settings.AddressLine1 = normalized.AddressLine1;
        settings.AddressLine2 = normalized.AddressLine2;
        settings.City = normalized.City;
        settings.State = normalized.State;
        settings.PostalCode = normalized.PostalCode;
    }

    private void EnsureSuperAdmin()
    {
        if (userContext.SystemRole != SystemRole.SuperAdmin)
        {
            throw new ForbiddenException("Somente o superadmin pode gerenciar as configurações da plataforma.");
        }
    }

    private static string NormalizeOptionalText(string? value, int maxLength)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        if (normalized.Length > maxLength)
        {
            throw new ValidationException($"O texto deve ter no máximo {maxLength} caracteres.");
        }

        return normalized;
    }

    private static string NormalizeOptionalEmail(string? value)
    {
        var normalized = NormalizeOptionalText(value, 320);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        try
        {
            return new MailAddress(normalized).Address;
        }
        catch (FormatException)
        {
            throw new ValidationException("Informe um e-mail de contato válido.");
        }
    }

    private sealed record NormalizedPlatformSettings(
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
}
