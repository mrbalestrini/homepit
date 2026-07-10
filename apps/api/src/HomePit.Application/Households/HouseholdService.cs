using HomePit.Application.Auth;
using HomePit.Application.Common;
using HomePit.Application.Plans;
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

public sealed record HouseholdInvitationDto(
    Guid Id,
    Guid HouseholdId,
    string HouseholdName,
    string InviteeEmail,
    Guid InviterUserId,
    string InviterDisplayName,
    HouseholdRole Role,
    HouseholdInvitationStatus Status,
    DateTimeOffset InvitedAt,
    DateTimeOffset? RespondedAt,
    bool IsIncoming);

public sealed class HouseholdService(
    IHomePitDbContext db,
    IUserContext userContext,
    HomePitDataPurgeService dataPurgeService,
    CommercialPlanService commercialPlanService,
    TimeProvider timeProvider)
{
    private const string SuperAdminReadOnlyMessage = "O superadmin possui acesso somente leitura nesta etapa.";

    public async Task<IReadOnlyCollection<HouseholdDto>> ListAsync(CancellationToken cancellationToken)
    {
        if (userContext.SystemRole == SystemRole.SuperAdmin)
        {
            return await db.Households
                .AsNoTracking()
                .OrderBy(household => household.Name)
                .Select(household => new HouseholdDto(
                    household.Id,
                    household.Name,
                    HouseholdRole.Member,
                    household.CreatedAt,
                    household.CreatedByUserId == userContext.UserId))
                .ToArrayAsync(cancellationToken);
        }

        return await db.HouseholdMembers
            .AsNoTracking()
            .Where(member => member.UserId == userContext.UserId && member.IsActive)
            .OrderBy(member => member.Household!.Name)
            .Select(member => new HouseholdDto(
                member.HouseholdId,
                member.Household!.Name,
                member.Role,
                member.Household!.CreatedAt,
                member.Household!.CreatedByUserId == userContext.UserId))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<HouseholdDto> CreateAsync(CreateHouseholdRequest request, CancellationToken cancellationToken)
    {
        EnsureWritable();
        await commercialPlanService.EnsureCanCreateHouseholdAsync(userContext.UserId, cancellationToken);
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ValidationException("Informe o nome da casa.");
        }

        var household = new Household
        {
            Name = request.Name.Trim(),
            CreatedByUserId = userContext.UserId
        };
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
        return ToHouseholdDto(household, member.Role);
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

        return ToHouseholdDto(member.Household, member.Role);
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

    public async Task<IReadOnlyCollection<HouseholdInvitationDto>> ListInvitationsAsync(CancellationToken cancellationToken)
    {
        if (userContext.SystemRole == SystemRole.SuperAdmin)
        {
            return Array.Empty<HouseholdInvitationDto>();
        }

        var currentUser = await db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(user => user.Id == userContext.UserId && user.IsActive, cancellationToken)
            ?? throw new ForbiddenException("Usuário não encontrado.");

        var currentEmail = NormalizeEmail(currentUser.Email);
        var invitations = await db.HouseholdInvitations
            .AsNoTracking()
            .Include(item => item.Household)
            .Include(item => item.InviterUser)
            .Where(item =>
                item.InviteeEmail == currentEmail ||
                item.InviterUserId == currentUser.Id)
            .OrderByDescending(item => item.InvitedAt)
            .ToArrayAsync(cancellationToken);

        return invitations
            .Select(item => new HouseholdInvitationDto(
                item.Id,
                item.HouseholdId,
                item.Household?.Name ?? string.Empty,
                item.InviteeEmail,
                item.InviterUserId,
                item.InviterUser?.DisplayName ?? string.Empty,
                item.Role,
                item.Status,
                item.InvitedAt,
                item.RespondedAt,
                item.InviteeEmail == currentEmail))
            .ToArray();
    }

    public async Task<HouseholdInvitationDto> ShareAsync(ShareHouseholdRequest request, CancellationToken cancellationToken)
    {
        EnsureWritable();
        var currentMember = await ResolveCurrentMembershipAsync(requireManager: true, cancellationToken);
        var currentUserEmail = await db.Users
            .AsNoTracking()
            .Where(user => user.Id == userContext.UserId && user.IsActive)
            .Select(user => user.Email)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new ForbiddenException("Usuário não encontrado.");
        var email = NormalizeEmail(request.Email);
        var role = NormalizeSharedRole(request.Role);

        var user = await db.Users
            .FirstOrDefaultAsync(item => item.Email == email && item.IsActive, cancellationToken)
            ?? throw new NotFoundException("Usuário não encontrado. Peça para a pessoa criar uma conta antes de compartilhar a casa.");

        if (user.Id == userContext.UserId)
        {
            throw new ValidationException("Não é possível convidar você mesmo.");
        }

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

        await commercialPlanService.EnsureCanInviteMemberToHouseholdAsync(
            currentMember.HouseholdId,
            user.Id,
            cancellationToken);

        var invitation = await db.HouseholdInvitations
            .Include(item => item.Household)
            .Include(item => item.InviterUser)
            .FirstOrDefaultAsync(item =>
                item.HouseholdId == currentMember.HouseholdId &&
                item.InviteeEmail == email,
                cancellationToken);

        if (invitation is null)
        {
            invitation = new HouseholdInvitation
            {
                HouseholdId = currentMember.HouseholdId,
                InviterUserId = userContext.UserId,
                InviteeEmail = email,
                Role = role,
                Status = HouseholdInvitationStatus.Pending,
                InvitedAt = timeProvider.GetUtcNow()
            };
            db.HouseholdInvitations.Add(invitation);
        }
        else
        {
            invitation.InviterUserId = userContext.UserId;
            invitation.InviteeEmail = email;
            invitation.Role = role;
            invitation.Status = HouseholdInvitationStatus.Pending;
            invitation.InvitedAt = timeProvider.GetUtcNow();
            invitation.RespondedAt = null;
        }

        await db.SaveChangesAsync(cancellationToken);
        return ToInvitationDto(invitation, currentEmail: NormalizeEmail(currentUserEmail));
    }

    public async Task<HouseholdDto> AcceptInvitationAsync(Guid invitationId, CancellationToken cancellationToken)
    {
        EnsureWritable();
        var currentUser = await db.Users
            .FirstOrDefaultAsync(user => user.Id == userContext.UserId && user.IsActive, cancellationToken)
            ?? throw new ForbiddenException("Usuário não encontrado.");

        var currentEmail = NormalizeEmail(currentUser.Email);
        var invitation = await db.HouseholdInvitations
            .Include(item => item.Household)
            .ThenInclude(household => household!.CreatedByUser)
            .FirstOrDefaultAsync(item => item.Id == invitationId, cancellationToken)
            ?? throw new NotFoundException("Convite não encontrado.");

        if (invitation.Status != HouseholdInvitationStatus.Pending)
        {
            throw new ValidationException("Este convite já foi respondido.");
        }

        if (!string.Equals(invitation.InviteeEmail, currentEmail, StringComparison.OrdinalIgnoreCase))
        {
            throw new ForbiddenException("Você não tem acesso a este convite.");
        }

        var member = await db.HouseholdMembers
            .Include(item => item.Household)
            .FirstOrDefaultAsync(item =>
                item.HouseholdId == invitation.HouseholdId &&
                item.UserId == currentUser.Id,
                cancellationToken);

        if (member is null)
        {
            member = new HouseholdMember
            {
                HouseholdId = invitation.HouseholdId,
                UserId = currentUser.Id,
                User = currentUser,
                Role = invitation.Role,
                IsActive = true
            };
            db.HouseholdMembers.Add(member);
            db.NotificationPreferences.Add(new NotificationPreference
            {
                HouseholdId = invitation.HouseholdId,
                HouseholdMember = member,
                WhatsAppPhoneNumber = currentUser.PhoneNumber
            });
        }
        else
        {
            member.IsActive = true;
            member.Role = invitation.Role;

            if (!await db.NotificationPreferences.AnyAsync(
                preference => preference.HouseholdMemberId == member.Id,
                cancellationToken))
            {
                db.NotificationPreferences.Add(new NotificationPreference
                {
                    HouseholdId = invitation.HouseholdId,
                    HouseholdMember = member,
                    WhatsAppPhoneNumber = currentUser.PhoneNumber
                });
            }
        }

        invitation.Status = HouseholdInvitationStatus.Accepted;
        invitation.RespondedAt = timeProvider.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);

        return ToHouseholdDto(invitation.Household!, invitation.Role);
    }

    public async Task DeclineInvitationAsync(Guid invitationId, CancellationToken cancellationToken)
    {
        EnsureWritable();
        var currentUser = await db.Users
            .FirstOrDefaultAsync(user => user.Id == userContext.UserId && user.IsActive, cancellationToken)
            ?? throw new ForbiddenException("Usuário não encontrado.");

        var currentEmail = NormalizeEmail(currentUser.Email);
        var invitation = await db.HouseholdInvitations
            .FirstOrDefaultAsync(item => item.Id == invitationId, cancellationToken)
            ?? throw new NotFoundException("Convite não encontrado.");

        if (invitation.Status != HouseholdInvitationStatus.Pending)
        {
            throw new ValidationException("Este convite já foi respondido.");
        }

        if (!string.Equals(invitation.InviteeEmail, currentEmail, StringComparison.OrdinalIgnoreCase))
        {
            throw new ForbiddenException("Você não tem acesso a este convite.");
        }

        invitation.Status = HouseholdInvitationStatus.Declined;
        invitation.RespondedAt = timeProvider.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);
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

    private static HouseholdInvitationDto ToInvitationDto(HouseholdInvitation invitation, string currentEmail)
    {
        return new HouseholdInvitationDto(
            invitation.Id,
            invitation.HouseholdId,
            invitation.Household?.Name ?? string.Empty,
            invitation.InviteeEmail,
            invitation.InviterUserId,
            invitation.InviterUser?.DisplayName ?? string.Empty,
            invitation.Role,
            invitation.Status,
            invitation.InvitedAt,
            invitation.RespondedAt,
            invitation.InviteeEmail == currentEmail);
    }

    private HouseholdDto ToHouseholdDto(Household household, HouseholdRole role)
    {
        return new HouseholdDto(
            household.Id,
            household.Name,
            role,
            household.CreatedAt,
            household.CreatedByUserId == userContext.UserId);
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
