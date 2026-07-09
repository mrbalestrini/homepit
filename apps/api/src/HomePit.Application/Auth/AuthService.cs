using HomePit.Application.Common;
using HomePit.Application.Images;
using HomePit.Application.Plans;
using HomePit.Application.Storage;
using HomePit.Domain.Finance;
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
    IImageUploadProcessor imageUploadProcessor,
    HomePitDataPurgeService dataPurgeService,
    SuperAdminOptions superAdminOptions,
    CommercialPlanService commercialPlanService)
{
    private const string SuperAdminReadOnlyMessage = "O superadmin possui acesso somente leitura nesta etapa.";
    private static readonly TimeSpan PendingDeletionWindow = TimeSpan.FromDays(30);

    private static readonly ImageUploadValidationMessages ProfilePhotoImageMessages = new(
        "Envie uma imagem com conteúdo para a foto de perfil.",
        "A foto de perfil deve ter no máximo 5 MB.",
        "A foto de perfil deve estar em JPG, PNG, WEBP, GIF ou BMP.",
        "Envie um arquivo de imagem válido para a foto de perfil.",
        "Imagens animadas não são aceitas na foto de perfil.");

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

        db.Users.Add(user);

        var response = await IssueTokensAsync(user, Array.Empty<HouseholdMember>(), cancellationToken);
        db.RefreshTokens.Add(new RefreshToken
        {
            User = user,
            TokenHash = tokenService.HashRefreshToken(response.RefreshToken),
            ExpiresAt = timeProvider.GetUtcNow().AddDays(30)
        });

        await db.SaveChangesAsync(cancellationToken);
        return response;
    }

    public async Task<DeleteOwnAccountResult> DeleteOwnAccountAsync(CancellationToken cancellationToken)
    {
        EnsureWritableUser();
        var user = await FindCurrentUserAsync(cancellationToken);
        var ownedHouseholdCount = await db.HouseholdMembers.CountAsync(
            member => member.UserId == user.Id && member.IsActive && member.Role == HouseholdRole.Owner,
            cancellationToken);

        if (ownedHouseholdCount == 0)
        {
            await dataPurgeService.DeleteUserAsync(user.Id, cancellationToken);
            return new DeleteOwnAccountResult(true, null);
        }

        var scheduledDeletionAt = timeProvider.GetUtcNow().Add(PendingDeletionWindow);
        user.AccountState = AccountState.PendingSelfDeletion;
        user.ScheduledDeletionAt = scheduledDeletionAt;
        user.DeactivatedAt = timeProvider.GetUtcNow();
        user.DeactivatedByUserId = user.Id;
        await db.SaveChangesAsync(cancellationToken);

        return new DeleteOwnAccountResult(false, scheduledDeletionAt);
    }

    public async Task<UserDto> ReactivateOwnAccountAsync(CancellationToken cancellationToken)
    {
        EnsureWritableUser();
        var user = await FindCurrentUserAsync(cancellationToken);
        if (user.AccountState != AccountState.PendingSelfDeletion)
        {
            throw new ValidationException("Somente contas com cancelamento pendente podem ser reativadas.");
        }

        ReactivateUser(user);
        await db.SaveChangesAsync(cancellationToken);
        return ToUserDto(user);
    }

    public async Task<IReadOnlyCollection<AdminUserListItemDto>> ListAdminUsersAsync(CancellationToken cancellationToken)
    {
        EnsureSuperAdmin();
        var users = await db.Users
            .AsNoTracking()
            .Include(user => user.HouseholdMembers)
            .Where(user => user.IsActive)
            .OrderByDescending(user => user.SystemRole == SystemRole.SuperAdmin)
            .ThenBy(user => user.DisplayName)
            .ToArrayAsync(cancellationToken);

        var results = new List<AdminUserListItemDto>(users.Length);
        foreach (var user in users)
        {
            results.Add(await BuildAdminUserAsync(user, cancellationToken));
        }

        return results;
    }

    public async Task<AdminUserListItemDto> DeactivateUserAsSuperAdminAsync(Guid userId, CancellationToken cancellationToken)
    {
        EnsureSuperAdmin();
        var user = await FindManagedUserAsync(userId, cancellationToken);
        user.AccountState = AccountState.DisabledBySuperAdmin;
        user.ScheduledDeletionAt = null;
        user.DeactivatedAt = timeProvider.GetUtcNow();
        user.DeactivatedByUserId = userContext.UserId;
        await db.SaveChangesAsync(cancellationToken);
        return await BuildAdminUserAsync(user.Id, cancellationToken);
    }

    public async Task<AdminUserListItemDto> ReactivateUserAsSuperAdminAsync(Guid userId, CancellationToken cancellationToken)
    {
        EnsureSuperAdmin();
        var user = await FindManagedUserAsync(userId, cancellationToken);
        if (user.AccountState != AccountState.DisabledBySuperAdmin)
        {
            throw new ValidationException("Somente contas desativadas pelo superadmin podem ser reativadas aqui.");
        }

        ReactivateUser(user);
        await db.SaveChangesAsync(cancellationToken);
        return await BuildAdminUserAsync(user.Id, cancellationToken);
    }

    public async Task DeleteUserAsSuperAdminAsync(Guid userId, CancellationToken cancellationToken)
    {
        EnsureSuperAdmin();
        await FindManagedUserAsync(userId, cancellationToken);
        await dataPurgeService.DeleteUserAsync(userId, cancellationToken);
    }

    public async Task<int> PurgeScheduledDeletionsAsync(CancellationToken cancellationToken)
    {
        var dueUserIds = await db.Users
            .AsNoTracking()
            .Where(user =>
                user.IsActive &&
                user.AccountState == AccountState.PendingSelfDeletion &&
                user.ScheduledDeletionAt != null &&
                user.ScheduledDeletionAt <= timeProvider.GetUtcNow())
            .Select(user => user.Id)
            .ToArrayAsync(cancellationToken);

        foreach (var userId in dueUserIds)
        {
            await dataPurgeService.DeleteUserAsync(userId, cancellationToken);
        }

        return dueUserIds.Length;
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
            var superAdminResponse = await IssueTokensAsync(superAdminUser, Array.Empty<HouseholdMember>(), cancellationToken);
            db.RefreshTokens.Add(new RefreshToken
            {
                UserId = superAdminUser.Id,
                TokenHash = tokenService.HashRefreshToken(superAdminResponse.RefreshToken),
                ExpiresAt = timeProvider.GetUtcNow().AddDays(30)
            });

            await db.SaveChangesAsync(cancellationToken);
            return superAdminResponse;
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
        var preparedImage = await imageUploadProcessor.PrepareAsync(
            content,
            contentLength,
            contentType,
            ImageUploadPolicies.Common,
            ProfilePhotoImageMessages,
            cancellationToken);
        var user = await FindCurrentUserAsync(cancellationToken);
        var objectKey = ObjectStorageKeys.UserProfilePhoto(user.Id);

        await using var uploadStream = new MemoryStream(preparedImage.Content, writable: false);
        await objectStorage.PutAsync(
            new ObjectStoragePutRequest(objectKey, uploadStream, preparedImage.ContentLength, preparedImage.ContentType),
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

    public async Task<StoredObject> GetProfilePhotoAsync(Guid userId, CancellationToken cancellationToken)
    {
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var user = await db.Users
            .AsNoTracking()
            .Where(item => item.Id == userId)
            .Select(item => new
            {
                item.Id,
                item.ProfilePhotoObjectKey,
                IsHouseholdMember = item.HouseholdMembers.Any(member =>
                    member.HouseholdId == currentMember.HouseholdId && member.IsActive),
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (user is null || !user.IsHouseholdMember || string.IsNullOrWhiteSpace(user.ProfilePhotoObjectKey))
        {
            throw new NotFoundException("Foto de perfil não encontrada.");
        }

        return await objectStorage.GetAsync(user.ProfilePhotoObjectKey, cancellationToken);
    }

    private async Task<HouseholdMember> ResolveCurrentMemberAsync(CancellationToken cancellationToken)
    {
        var householdId = await ResolveHouseholdIdAsync(cancellationToken);

        if (userContext.SystemRole == SystemRole.SuperAdmin)
        {
            return new HouseholdMember
            {
                HouseholdId = householdId,
                UserId = userContext.UserId,
                Role = HouseholdRole.Member
            };
        }

        return await db.HouseholdMembers
            .Include(member => member.User)
            .FirstOrDefaultAsync(member =>
                member.HouseholdId == householdId &&
                member.UserId == userContext.UserId &&
                member.IsActive,
                cancellationToken)
            ?? throw new ForbiddenException("Você não tem acesso a esta casa.");
    }

    private async Task<Guid> ResolveHouseholdIdAsync(CancellationToken cancellationToken)
    {
        if (userContext.SystemRole == SystemRole.SuperAdmin)
        {
            return await ResolveSuperAdminHouseholdIdAsync(cancellationToken);
        }

        var memberships = await db.HouseholdMembers
            .AsNoTracking()
            .Where(member => member.UserId == userContext.UserId && member.IsActive)
            .Select(member => member.HouseholdId)
            .ToArrayAsync(cancellationToken);

        if (memberships.Length == 0)
        {
            throw new ForbiddenException("Usuário sem casa vinculada.");
        }

        if (userContext.HouseholdId is null)
        {
            if (memberships.Length == 1)
            {
                return memberships[0];
            }

            throw new ValidationException("Informe X-Household-Id para escolher a casa.");
        }

        if (!memberships.Contains(userContext.HouseholdId.Value))
        {
            throw new ForbiddenException("Você não tem acesso a esta casa.");
        }

        return userContext.HouseholdId.Value;
    }

    private async Task<Guid> ResolveSuperAdminHouseholdIdAsync(CancellationToken cancellationToken)
    {
        if (userContext.HouseholdId is null)
        {
            var householdIds = await db.Households
                .AsNoTracking()
                .OrderBy(household => household.Name)
                .Select(household => household.Id)
                .Take(2)
                .ToArrayAsync(cancellationToken);

            return householdIds.Length switch
            {
                0 => throw new NotFoundException("Casa não encontrada."),
                1 => householdIds[0],
                _ => throw new ValidationException("Informe X-Household-Id para escolher a casa.")
            };
        }

        var exists = await db.Households
            .AsNoTracking()
            .AnyAsync(household => household.Id == userContext.HouseholdId.Value, cancellationToken);

        if (!exists)
        {
            throw new NotFoundException("Casa não encontrada.");
        }

        return userContext.HouseholdId.Value;
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
                .Select(household => new HouseholdDto(household.Id, household.Name, HouseholdRole.Member, household.CreatedAt))
                .ToArrayAsync(cancellationToken)
            : memberships
                .Select(member => new HouseholdDto(
                    member.HouseholdId,
                    member.Household?.Name ?? string.Empty,
                    member.Role,
                    member.Household!.CreatedAt))
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

    private UserDto ToUserDto(AppUser user)
    {
        return new UserDto(
            user.Id,
            user.Email,
            user.DisplayName,
            user.PhoneNumber,
            user.SystemRole,
            user.AccountState,
            user.ScheduledDeletionAt,
            superAdminOptions.SupportEmail,
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

    private void EnsureSuperAdmin()
    {
        if (userContext.SystemRole != SystemRole.SuperAdmin)
        {
            throw new ForbiddenException("Somente o superadmin pode gerenciar usuários.");
        }
    }

    private void ReactivateUser(AppUser user)
    {
        user.AccountState = AccountState.Active;
        user.ScheduledDeletionAt = null;
        user.DeactivatedAt = null;
        user.DeactivatedByUserId = null;
    }

    private async Task<AppUser> FindManagedUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await db.Users
            .FirstOrDefaultAsync(item => item.Id == userId && item.IsActive, cancellationToken)
            ?? throw new NotFoundException("Usuário não encontrado.");

        if (user.SystemRole == SystemRole.SuperAdmin)
        {
            throw new ForbiddenException("O usuário superadmin é protegido e não pode ser alterado aqui.");
        }

        return user;
    }

    private async Task<AdminUserListItemDto> BuildAdminUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await db.Users
            .AsNoTracking()
            .Include(item => item.HouseholdMembers)
            .FirstOrDefaultAsync(user => user.Id == userId && user.IsActive, cancellationToken)
            ?? throw new NotFoundException("Usuário não encontrado.");

        return await BuildAdminUserAsync(user, cancellationToken);
    }

    private async Task<AdminUserListItemDto> BuildAdminUserAsync(AppUser user, CancellationToken cancellationToken)
    {
        var commercialSummary = await commercialPlanService.GetAdminUserCommercialSummaryAsync(user.Id, cancellationToken);

        return new AdminUserListItemDto(
            user.Id,
            user.Email,
            user.DisplayName,
            user.PhoneNumber,
            user.SystemRole,
            user.AccountState,
            user.ScheduledDeletionAt,
            user.DeactivatedAt,
            user.HouseholdMembers.Count(member => member.IsActive && member.Role == HouseholdRole.Owner),
            user.HouseholdMembers.Count(member => member.IsActive),
            user.SystemRole == SystemRole.SuperAdmin,
            commercialSummary.EffectivePlanSlug,
            commercialSummary.EffectivePlanName,
            commercialSummary.ActiveSubscriptionStartsAt,
            commercialSummary.ActiveSubscriptionEndsAt,
            commercialSummary.ActiveSubscriptionBillingCycle,
            commercialSummary.ActiveSubscriptionAmountPaid,
            commercialSummary.ActiveSubscriptionCurrencyCode,
            commercialSummary.ActiveSubscriptionStatus);
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
                AccountState = AccountState.Active,
                IsActive = true
            };
            db.Users.Add(superAdminUser);
            return superAdminUser;
        }

        superAdminUser.Email = email;
        superAdminUser.PasswordHash = passwordHasher.Hash(superAdminOptions.Password!);
        superAdminUser.DisplayName = displayName;
        superAdminUser.SystemRole = SystemRole.SuperAdmin;
        superAdminUser.AccountState = AccountState.Active;
        superAdminUser.ScheduledDeletionAt = null;
        superAdminUser.DeactivatedAt = null;
        superAdminUser.DeactivatedByUserId = null;
        superAdminUser.IsActive = true;
        return superAdminUser;
    }
}
