using HomePit.Application.Common;
using HomePit.Application.Storage;
using HomePit.Domain.Households;
using HomePit.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace HomePit.Application.Auth;

public sealed class AuthService(
    IHomePitDbContext db,
    IPasswordHasher passwordHasher,
    ITokenService tokenService,
    TimeProvider timeProvider,
    IUserContext userContext,
    IObjectStorage objectStorage,
    SuperAdminOptions superAdminOptions)
{
    private const long ProfilePhotoMaxBytes = 5 * 1024 * 1024;
    private const string SuperAdminReadOnlyMessage = "O superadmin possui acesso somente leitura nesta etapa.";

    private static readonly HashSet<string> AllowedProfilePhotoContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp"
    };

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);
        ValidatePassword(request.Password);

        if (await db.Users.AnyAsync(user => user.Email == email, cancellationToken))
        {
            throw new ConflictException("Este e-mail já está cadastrado.");
        }

        var systemRole = await db.Users.AnyAsync(user => user.SystemRole != SystemRole.SuperAdmin, cancellationToken)
            ? SystemRole.User
            : SystemRole.Admin;

        var user = new AppUser
        {
            Email = email,
            PasswordHash = passwordHasher.Hash(request.Password),
            DisplayName = RequiredText(request.DisplayName, "Informe o nome."),
            PhoneNumber = NormalizeOptional(request.PhoneNumber),
            SystemRole = systemRole
        };

        var memberships = new List<HouseholdMember>();
        var householdName = NormalizeOptional(request.HouseholdName);
        if (householdName is not null)
        {
            var household = new Household { Name = householdName };
            var member = new HouseholdMember
            {
                Household = household,
                User = user,
                Role = HouseholdRole.Owner
            };

            var preference = new NotificationPreference
            {
                Household = household,
                HouseholdMember = member,
                WhatsAppPhoneNumber = user.PhoneNumber
            };

            db.Households.Add(household);
            db.HouseholdMembers.Add(member);
            db.NotificationPreferences.Add(preference);
            memberships.Add(member);
        }

        db.Users.Add(user);

        var response = await IssueTokensAsync(user, memberships, cancellationToken);
        db.RefreshTokens.Add(new RefreshToken
        {
            User = user,
            TokenHash = tokenService.HashRefreshToken(response.RefreshToken),
            ExpiresAt = timeProvider.GetUtcNow().AddDays(30)
        });

        await db.SaveChangesAsync(cancellationToken);
        return response;
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);

        if (IsSuperAdminEmail(email))
        {
            if (!IsSuperAdminPassword(request.Password))
            {
                throw new ForbiddenException("E-mail ou senha inválidos.");
            }

            var superAdminUser = await EnsureSuperAdminUserAsync(cancellationToken);
            var response = await IssueTokensAsync(superAdminUser, Array.Empty<HouseholdMember>(), cancellationToken);
            db.RefreshTokens.Add(new RefreshToken
            {
                UserId = superAdminUser.Id,
                TokenHash = tokenService.HashRefreshToken(response.RefreshToken),
                ExpiresAt = timeProvider.GetUtcNow().AddDays(30)
            });

            await db.SaveChangesAsync(cancellationToken);
            return response;
        }

        var user = await db.Users
            .Include(item => item.HouseholdMembers)
                .ThenInclude(member => member.Household)
            .FirstOrDefaultAsync(item => item.Email == email && item.IsActive, cancellationToken);

        if (user is null || user.SystemRole == SystemRole.SuperAdmin || !passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            throw new ForbiddenException("E-mail ou senha inválidos.");
        }

        var memberships = user.HouseholdMembers
            .Where(member => member.IsActive)
            .ToArray();

        var response = await IssueTokensAsync(user, memberships, cancellationToken);
        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = tokenService.HashRefreshToken(response.RefreshToken),
            ExpiresAt = timeProvider.GetUtcNow().AddDays(30)
        });

        await db.SaveChangesAsync(cancellationToken);
        return response;
    }

    public async Task<AuthResponse> RefreshAsync(RefreshRequest request, CancellationToken cancellationToken)
    {
        var tokenHash = tokenService.HashRefreshToken(request.RefreshToken);
        var now = timeProvider.GetUtcNow();
        var savedToken = await db.RefreshTokens
            .Include(token => token.User)
                .ThenInclude(user => user!.HouseholdMembers)
                    .ThenInclude(member => member.Household)
            .FirstOrDefaultAsync(token =>
                token.TokenHash == tokenHash &&
                token.RevokedAt == null &&
                token.ExpiresAt > now,
                cancellationToken);

        if (savedToken?.User is null || !savedToken.User.IsActive)
        {
            throw new ForbiddenException("Sessão expirada ou inválida.");
        }

        savedToken.RevokedAt = now;

        var user = savedToken.User.SystemRole == SystemRole.SuperAdmin
            ? await EnsureSuperAdminUserAsync(cancellationToken)
            : savedToken.User;

        var memberships = user.SystemRole == SystemRole.SuperAdmin
            ? Array.Empty<HouseholdMember>()
            : user.HouseholdMembers
            .Where(member => member.IsActive)
            .ToArray();

        var response = await IssueTokensAsync(user, memberships, cancellationToken);
        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = tokenService.HashRefreshToken(response.RefreshToken),
            ExpiresAt = now.AddDays(30)
        });

        await db.SaveChangesAsync(cancellationToken);
        return response;
    }

    public async Task<UserDto> UpdateProfileAsync(UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        EnsureWritableUser();
        var user = await FindCurrentUserAsync(cancellationToken);

        user.DisplayName = RequiredText(request.DisplayName, "Informe o nome.");
        user.PhoneNumber = NormalizeOptional(request.PhoneNumber);

        await db.SaveChangesAsync(cancellationToken);

        return ToUserDto(user);
    }

    public async Task<UserDto> UploadProfilePhotoAsync(
        Stream content,
        long contentLength,
        string? contentType,
        CancellationToken cancellationToken)
    {
        EnsureWritableUser();
        if (contentLength <= 0)
        {
            throw new ValidationException("Envie uma imagem com conteúdo para a foto de perfil.");
        }

        if (contentLength > ProfilePhotoMaxBytes)
        {
            throw new ValidationException($"A foto de perfil deve ter no máximo {FormatMegabytes(ProfilePhotoMaxBytes)} MB.");
        }

        var normalizedContentType = NormalizeProfilePhotoContentType(contentType);
        var user = await FindCurrentUserAsync(cancellationToken);
        var objectKey = ObjectStorageKeys.UserProfilePhoto(user.Id);

        await objectStorage.PutAsync(
            new ObjectStoragePutRequest(objectKey, content, contentLength, normalizedContentType),
            cancellationToken);

        user.ProfilePhotoObjectKey = objectKey;
        user.ProfilePhotoUpdatedAt = timeProvider.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);

        return ToUserDto(user);
    }

    public async Task<StoredObject> GetProfilePhotoAsync(CancellationToken cancellationToken)
    {
        var user = await FindCurrentUserAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(user.ProfilePhotoObjectKey))
        {
            throw new NotFoundException("Foto de perfil não encontrada.");
        }

        return await objectStorage.GetAsync(user.ProfilePhotoObjectKey, cancellationToken);
    }

    private async Task<AuthResponse> IssueTokensAsync(
        AppUser user,
        IReadOnlyCollection<HouseholdMember> memberships,
        CancellationToken cancellationToken)
    {
        var households = user.SystemRole == SystemRole.SuperAdmin
            ? await db.Households
                .AsNoTracking()
                .OrderBy(household => household.Name)
                .Select(household => new HouseholdDto(household.Id, household.Name, HouseholdRole.Member))
                .ToArrayAsync(cancellationToken)
            : memberships
                .Select(member => new HouseholdDto(member.HouseholdId, member.Household?.Name ?? string.Empty, member.Role))
                .ToArray();

        return new AuthResponse(
            tokenService.CreateAccessToken(user, memberships),
            tokenService.CreateRefreshToken(),
            tokenService.AccessTokenExpiresAt,
            ToUserDto(user),
            households);
    }

    private async Task<AppUser> FindCurrentUserAsync(CancellationToken cancellationToken)
    {
        return await db.Users
            .FirstOrDefaultAsync(item => item.Id == userContext.UserId && item.IsActive, cancellationToken)
            ?? throw new ForbiddenException("Usuário não encontrado.");
    }

    private static UserDto ToUserDto(AppUser user)
    {
        return new UserDto(
            user.Id,
            user.Email,
            user.DisplayName,
            user.PhoneNumber,
            user.SystemRole,
            !string.IsNullOrWhiteSpace(user.ProfilePhotoObjectKey),
            user.ProfilePhotoUpdatedAt);
    }

    private static string NormalizeEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ValidationException("Informe o e-mail.");
        }

        return email.Trim().ToLowerInvariant();
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string RequiredText(string value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ValidationException(message);
        }

        return value.Trim();
    }

    private static string NormalizeProfilePhotoContentType(string? contentType)
    {
        var normalized = NormalizeOptional(contentType);
        if (normalized is null || !AllowedProfilePhotoContentTypes.Contains(normalized))
        {
            throw new ValidationException("A foto de perfil deve estar em JPG, PNG ou WEBP.");
        }

        return normalized;
    }

    private static long FormatMegabytes(long bytes)
    {
        return bytes / (1024 * 1024);
    }

    private static void ValidatePassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
        {
            throw new ValidationException("A senha precisa ter pelo menos 8 caracteres.");
        }
    }

    private bool IsSuperAdminEmail(string email)
    {
        return superAdminOptions.IsEnabled &&
            string.Equals(email, NormalizeEmail(superAdminOptions.Email!), StringComparison.Ordinal);
    }

    private bool IsSuperAdminPassword(string password)
    {
        if (!superAdminOptions.IsEnabled)
        {
            return false;
        }

        var configuredPassword = superAdminOptions.Password!;
        var left = Encoding.UTF8.GetBytes(password);
        var right = Encoding.UTF8.GetBytes(configuredPassword);
        return left.Length == right.Length && CryptographicOperations.FixedTimeEquals(left, right);
    }

    private void EnsureWritableUser()
    {
        if (userContext.SystemRole == SystemRole.SuperAdmin)
        {
            throw new ForbiddenException(SuperAdminReadOnlyMessage);
        }
    }

    private async Task<AppUser> EnsureSuperAdminUserAsync(CancellationToken cancellationToken)
    {
        if (!superAdminOptions.IsEnabled)
        {
            throw new ForbiddenException("Sessão expirada ou inválida.");
        }

        var email = NormalizeEmail(superAdminOptions.Email!);
        var displayName = NormalizeOptional(superAdminOptions.DisplayName) ?? "SuperAdmin";
        var candidates = await db.Users
            .Where(user => user.SystemRole == SystemRole.SuperAdmin || user.Email == email)
            .ToArrayAsync(cancellationToken);

        var superAdminUser = candidates.FirstOrDefault(user => user.SystemRole == SystemRole.SuperAdmin);
        if (superAdminUser is null)
        {
            superAdminUser = candidates.FirstOrDefault(user => user.Email == email);
        }

        if (superAdminUser is not null && candidates.Any(user => user.Email == email && user.Id != superAdminUser.Id))
        {
            throw new ConflictException("O e-mail configurado para o superadmin já está em uso por outro usuário.");
        }

        if (superAdminUser is null)
        {
            superAdminUser = new AppUser
            {
                Email = email,
                PasswordHash = passwordHasher.Hash(superAdminOptions.Password!),
                DisplayName = displayName,
                SystemRole = SystemRole.SuperAdmin,
                IsActive = true
            };
            db.Users.Add(superAdminUser);
            return superAdminUser;
        }

        superAdminUser.Email = email;
        superAdminUser.PasswordHash = passwordHasher.Hash(superAdminOptions.Password!);
        superAdminUser.DisplayName = displayName;
        superAdminUser.SystemRole = SystemRole.SuperAdmin;
        superAdminUser.IsActive = true;
        return superAdminUser;
    }
}
