using HomePit.Application.Common;
using HomePit.Domain.Households;
using HomePit.Domain.Notifications;
using Microsoft.EntityFrameworkCore;

namespace HomePit.Application.Auth;

public sealed class AuthService(
    IHomePitDbContext db,
    IPasswordHasher passwordHasher,
    ITokenService tokenService,
    TimeProvider timeProvider,
    IUserContext userContext)
{
    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);
        ValidatePassword(request.Password);

        if (await db.Users.AnyAsync(user => user.Email == email, cancellationToken))
        {
            throw new ConflictException("Este e-mail já está cadastrado.");
        }

        var systemRole = await db.Users.AnyAsync(cancellationToken)
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

        var response = IssueTokens(user, memberships);
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
        var user = await db.Users
            .Include(item => item.HouseholdMembers)
                .ThenInclude(member => member.Household)
            .FirstOrDefaultAsync(item => item.Email == email && item.IsActive, cancellationToken);

        if (user is null || !passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            throw new ForbiddenException("E-mail ou senha inválidos.");
        }

        var memberships = user.HouseholdMembers
            .Where(member => member.IsActive)
            .ToArray();

        var response = IssueTokens(user, memberships);
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

        var memberships = savedToken.User.HouseholdMembers
            .Where(member => member.IsActive)
            .ToArray();

        var response = IssueTokens(savedToken.User, memberships);
        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = savedToken.UserId,
            TokenHash = tokenService.HashRefreshToken(response.RefreshToken),
            ExpiresAt = now.AddDays(30)
        });

        await db.SaveChangesAsync(cancellationToken);
        return response;
    }

    public async Task<UserDto> UpdateProfileAsync(UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        var user = await db.Users
            .FirstOrDefaultAsync(item => item.Id == userContext.UserId && item.IsActive, cancellationToken)
            ?? throw new ForbiddenException("Usuário não encontrado.");

        user.DisplayName = RequiredText(request.DisplayName, "Informe o nome.");
        user.PhoneNumber = NormalizeOptional(request.PhoneNumber);

        await db.SaveChangesAsync(cancellationToken);

        return new UserDto(user.Id, user.Email, user.DisplayName, user.PhoneNumber, user.SystemRole);
    }

    private AuthResponse IssueTokens(AppUser user, IReadOnlyCollection<HouseholdMember> memberships)
    {
        return new AuthResponse(
            tokenService.CreateAccessToken(user, memberships),
            tokenService.CreateRefreshToken(),
            tokenService.AccessTokenExpiresAt,
            new UserDto(user.Id, user.Email, user.DisplayName, user.PhoneNumber, user.SystemRole),
            memberships
                .Select(member => new HouseholdDto(member.HouseholdId, member.Household?.Name ?? string.Empty, member.Role))
                .ToArray());
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

    private static void ValidatePassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
        {
            throw new ValidationException("A senha precisa ter pelo menos 8 caracteres.");
        }
    }
}
