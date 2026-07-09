using HomePit.Application.Auth;
using HomePit.Application.Common;
using HomePit.Domain.Finance;
using HomePit.Domain.Households;
using HomePit.Domain.Notifications;
using Microsoft.EntityFrameworkCore;

namespace HomePit.Application.Households;

public sealed record CreateHouseholdRequest(string Name);

public sealed record UpdateHouseholdRequest(string Name);

public sealed record ShareHouseholdRequest(string Email, HouseholdRole Role);

public sealed record UpdateHouseholdMemberRequest(HouseholdRole Role);

public sealed record HouseholdMemberDto(
    Guid Id,
    Guid UserId,
    string DisplayName,
    string Email,
    string? PhoneNumber,
    bool HasProfilePhoto,
    DateTimeOffset? ProfilePhotoUpdatedAt,
    HouseholdRole Role,
    bool IsCurrentUser);

public sealed class HouseholdService(
    IHomePitDbContext db,
    IUserContext userContext,
    HomePitDataPurgeService dataPurgeService)
{
    private const string SuperAdminReadOnlyMessage = "O superadmin possui acesso somente leitura nesta etapa.";

    public async Task<IReadOnlyCollection<HouseholdDto>> ListAsync(CancellationToken cancellationToken)
    {
        if (userContext.SystemRole == SystemRole.SuperAdmin)
        {
            return await db.Households
                .AsNoTracking()
                .OrderBy(household => household.Name)
                .Select(household => new HouseholdDto(household.Id, household.Name, HouseholdRole.Member, household.CreatedAt))
                .ToArrayAsync(cancellationToken);
        }

        return await db.HouseholdMembers
            .AsNoTracking()
            .Where(member => member.UserId == userContext.UserId && member.IsActive)
            .OrderBy(member => member.Household!.Name)
            .Select(member => new HouseholdDto(member.HouseholdId, member.Household!.Name, member.Role, member.Household!.CreatedAt))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<HouseholdDto> CreateAsync(CreateHouseholdRequest request, CancellationToken cancellationToken)
    {
        EnsureWritable();
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ValidationException("Informe o nome da casa.");
        }

        var household = new Household { Name = request.Name.Trim() };
        var member = new HouseholdMember
        {
            Household = household,
            UserId = userContext.UserId,
            Role = HouseholdRole.Owner
        };

        db.Households.Add(household);
        db.HouseholdMembers.Add(member);
        db.FinanceCategories.AddRange(FinanceCategoryCatalog.CreateDefaults(household.Id, member.Id));
        db.NotificationPreferences.Add(new NotificationPreference
        {
            Household = household,
            HouseholdMember = member
        });

        await db.SaveChangesAsync(cancellationToken);
        return new HouseholdDto(household.Id, household.Name, member.Role, household.CreatedAt);
    }

    public async Task<HouseholdDto> UpdateAsync(
        Guid householdId,
        UpdateHouseholdRequest request,
        CancellationToken cancellationToken)
    {
        EnsureWritable();
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ValidationException("Informe o nome da casa.");
        }

        var member = await ResolveMembershipForHouseholdAsync(householdId, cancellationToken);
        EnsureOwner(member, "Somente o proprietário pode editar a casa.");

        member.Household!.Name = request.Name.Trim();
        await db.SaveChangesAsync(cancellationToken);

        return new HouseholdDto(member.HouseholdId, member.Household.Name, member.Role, member.Household.CreatedAt);
    }

    public async Task DeleteAsync(Guid householdId, CancellationToken cancellationToken)
    {
        EnsureWritable();
        var member = await ResolveMembershipForHouseholdAsync(householdId, cancellationToken);
        EnsureOwner(member, "Somente o proprietário pode excluir a casa.");

        await dataPurgeService.DeleteHouseholdAsync(householdId, cancellationToken);
    }

    public async Task<IReadOnlyCollection<HouseholdMemberDto>> ListMembersAsync(CancellationToken cancellationToken)
    {
        if (userContext.SystemRole == SystemRole.SuperAdmin)
        {
            var householdId = await ResolveSuperAdminHouseholdIdAsync(cancellationToken);
            return await db.HouseholdMembers
                .AsNoTracking()
                .Include(member => member.User)
                .Where(member => member.HouseholdId == householdId && member.IsActive)
                .OrderBy(member => member.User!.DisplayName)
                .Select(member => new HouseholdMemberDto(
                    member.Id,
                    member.UserId,
                    member.User!.DisplayName,
                    member.User.Email,
                    member.User.PhoneNumber,
                    !string.IsNullOrWhiteSpace(member.User.ProfilePhotoObjectKey),
                    member.User.ProfilePhotoUpdatedAt,
                    member.Role,
                    false))
                .ToArrayAsync(cancellationToken);
        }

        var currentMember = await ResolveCurrentMembershipAsync(requireManager: false, cancellationToken);

        return await db.HouseholdMembers
            .AsNoTracking()
            .Include(member => member.User)
            .Where(member => member.HouseholdId == currentMember.HouseholdId && member.IsActive)
            .OrderBy(member => member.User!.DisplayName)
            .Select(member => new HouseholdMemberDto(
                member.Id,
                member.UserId,
                member.User!.DisplayName,
                member.User.Email,
                member.User.PhoneNumber,
                !string.IsNullOrWhiteSpace(member.User.ProfilePhotoObjectKey),
                member.User.ProfilePhotoUpdatedAt,
                member.Role,
                member.UserId == userContext.UserId))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<HouseholdMemberDto> ShareAsync(ShareHouseholdRequest request, CancellationToken cancellationToken)
    {
        EnsureWritable();
        var currentMember = await ResolveCurrentMembershipAsync(requireManager: true, cancellationToken);
        var email = NormalizeEmail(request.Email);
        var role = NormalizeSharedRole(request.Role);

        var user = await db.Users
            .FirstOrDefaultAsync(item => item.Email == email && item.IsActive, cancellationToken)
            ?? throw new NotFoundException("Usuário não encontrado. Peça para a pessoa criar uma conta antes de compartilhar a casa.");

        var existingMember = await db.HouseholdMembers
            .Include(member => member.User)
            .FirstOrDefaultAsync(member =>
                member.HouseholdId == currentMember.HouseholdId &&
                member.UserId == user.Id,
                cancellationToken);

        if (existingMember is { IsActive: true })
        {
            throw new ConflictException("Este usuário já participa desta casa.");
        }

        if (existingMember is not null)
        {
            existingMember.Role = role;
            existingMember.IsActive = true;

            if (!await db.NotificationPreferences.AnyAsync(
                preference => preference.HouseholdMemberId == existingMember.Id,
                cancellationToken))
            {
                db.NotificationPreferences.Add(new NotificationPreference
                {
                    HouseholdId = currentMember.HouseholdId,
                    HouseholdMemberId = existingMember.Id,
                    WhatsAppPhoneNumber = user.PhoneNumber
                });
            }

            await db.SaveChangesAsync(cancellationToken);
            return ToMemberDto(existingMember, userContext.UserId);
        }

        var member = new HouseholdMember
        {
            HouseholdId = currentMember.HouseholdId,
            UserId = user.Id,
            User = user,
            Role = role
        };

        db.HouseholdMembers.Add(member);
        db.NotificationPreferences.Add(new NotificationPreference
        {
            HouseholdId = currentMember.HouseholdId,
            HouseholdMember = member,
            WhatsAppPhoneNumber = user.PhoneNumber
        });

        await db.SaveChangesAsync(cancellationToken);
        return ToMemberDto(member, userContext.UserId);
    }

    public async Task<HouseholdMemberDto> UpdateMemberAsync(
        Guid memberId,
        UpdateHouseholdMemberRequest request,
        CancellationToken cancellationToken)
    {
        EnsureWritable();
        var currentMember = await ResolveCurrentMembershipAsync(requireManager: false, cancellationToken);
        EnsureOwner(currentMember, "Somente o proprietário pode editar membros.");

        var member = await db.HouseholdMembers
            .Include(item => item.User)
            .FirstOrDefaultAsync(item =>
                item.Id == memberId &&
                item.HouseholdId == currentMember.HouseholdId &&
                item.IsActive,
                cancellationToken)
            ?? throw new NotFoundException("Membro não encontrado.");

        var nextRole = NormalizeEditableRole(request.Role);
        await EnsureOwnerChangeAllowedAsync(member, nextRole, cancellationToken);

        member.Role = nextRole;
        await db.SaveChangesAsync(cancellationToken);

        return ToMemberDto(member, userContext.UserId);
    }

    public async Task RemoveMemberAsync(Guid memberId, CancellationToken cancellationToken)
    {
        EnsureWritable();
        var currentMember = await ResolveCurrentMembershipAsync(requireManager: false, cancellationToken);
        EnsureOwner(currentMember, "Somente o proprietário pode remover membros.");

        var member = await db.HouseholdMembers
            .Include(item => item.User)
            .FirstOrDefaultAsync(item =>
                item.Id == memberId &&
                item.HouseholdId == currentMember.HouseholdId &&
                item.IsActive,
                cancellationToken)
            ?? throw new NotFoundException("Membro não encontrado.");

        await EnsureOwnerChangeAllowedAsync(member, nextRole: null, cancellationToken);

        member.IsActive = false;
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<HouseholdMember> ResolveCurrentMembershipAsync(
        bool requireManager,
        CancellationToken cancellationToken)
    {
        var query = db.HouseholdMembers
            .Include(member => member.Household)
            .Where(member => member.UserId == userContext.UserId && member.IsActive);

        var member = userContext.HouseholdId is null
            ? await ResolveSingleMembershipAsync(query, cancellationToken)
            : await query.FirstOrDefaultAsync(item => item.HouseholdId == userContext.HouseholdId.Value, cancellationToken)
                ?? throw new ForbiddenException("Você não tem acesso a esta casa.");

        if (requireManager && member.Role is not (HouseholdRole.Owner or HouseholdRole.Admin))
        {
            throw new ForbiddenException("Somente proprietários e administradores podem compartilhar a casa.");
        }

        return member;
    }

    private async Task<HouseholdMember> ResolveMembershipForHouseholdAsync(
        Guid householdId,
        CancellationToken cancellationToken)
    {
        return await db.HouseholdMembers
            .Include(member => member.Household)
            .FirstOrDefaultAsync(member =>
                member.HouseholdId == householdId &&
                member.UserId == userContext.UserId &&
                member.IsActive,
                cancellationToken)
            ?? throw new ForbiddenException("Você não tem acesso a esta casa.");
    }

    private static void EnsureOwner(HouseholdMember member, string message)
    {
        if (member.Role is not HouseholdRole.Owner)
        {
            throw new ForbiddenException(message);
        }
    }

    private static async Task<HouseholdMember> ResolveSingleMembershipAsync(
        IQueryable<HouseholdMember> query,
        CancellationToken cancellationToken)
    {
        var memberships = await query.Take(2).ToArrayAsync(cancellationToken);
        return memberships.Length switch
        {
            0 => throw new ForbiddenException("Usuário sem casa vinculada."),
            1 => memberships[0],
            _ => throw new ValidationException("Informe X-Household-Id para escolher a casa.")
        };
    }

    private static string NormalizeEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ValidationException("Informe o e-mail.");
        }

        return email.Trim().ToLowerInvariant();
    }

    private static HouseholdRole NormalizeSharedRole(HouseholdRole role)
    {
        return role switch
        {
            HouseholdRole.Admin => HouseholdRole.Admin,
            HouseholdRole.Member => HouseholdRole.Member,
            HouseholdRole.Owner => throw new ValidationException("Compartilhe a casa como administrador ou membro."),
            _ => throw new ValidationException("Perfil inválido para compartilhamento.")
        };
    }

    private static HouseholdRole NormalizeEditableRole(HouseholdRole role)
    {
        return role switch
        {
            HouseholdRole.Admin => HouseholdRole.Admin,
            HouseholdRole.Member => HouseholdRole.Member,
            HouseholdRole.Owner => throw new ValidationException("Edite membros apenas como administrador ou membro."),
            _ => throw new ValidationException("Perfil inválido para edição.")
        };
    }

    private async Task EnsureOwnerChangeAllowedAsync(
        HouseholdMember member,
        HouseholdRole? nextRole,
        CancellationToken cancellationToken)
    {
        if (member.Role is not HouseholdRole.Owner || nextRole is HouseholdRole.Owner)
        {
            return;
        }

        var ownerCount = await db.HouseholdMembers.CountAsync(
            item => item.HouseholdId == member.HouseholdId && item.IsActive && item.Role == HouseholdRole.Owner,
            cancellationToken);

        if (ownerCount <= 1)
        {
            throw new ValidationException("A casa precisa manter ao menos um proprietário ativo.");
        }
    }

    private static HouseholdMemberDto ToMemberDto(HouseholdMember member, Guid currentUserId)
    {
        return new HouseholdMemberDto(
            member.Id,
            member.UserId,
            member.User?.DisplayName ?? string.Empty,
            member.User?.Email ?? string.Empty,
            member.User?.PhoneNumber,
            !string.IsNullOrWhiteSpace(member.User?.ProfilePhotoObjectKey),
            member.User?.ProfilePhotoUpdatedAt,
            member.Role,
            member.UserId == currentUserId);
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

    private void EnsureWritable()
    {
        if (userContext.SystemRole == SystemRole.SuperAdmin)
        {
            throw new ForbiddenException(SuperAdminReadOnlyMessage);
        }
    }
}
