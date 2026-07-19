using OrganizaClub.Application.Auth;
using OrganizaClub.Application.Common;
using OrganizaClub.Application.Plans;
using OrganizaClub.Domain.Finance;
using OrganizaClub.Domain.Spaces;
using OrganizaClub.Domain.Notifications;
using Microsoft.EntityFrameworkCore;

namespace OrganizaClub.Application.Spaces;

public sealed record CreateSpaceRequest(string Name);

public sealed record UpdateSpaceRequest(string Name);

public sealed record ShareSpaceRequest(string Email, SpaceRole Role);

public sealed record UpdateSpaceMemberRequest(SpaceRole Role);

public sealed record SpaceMemberDto(
    Guid Id,
    Guid UserId,
    string DisplayName,
    string Email,
    string? PhoneNumber,
    bool HasProfilePhoto,
    DateTimeOffset? ProfilePhotoUpdatedAt,
    SpaceRole Role,
    bool IsCurrentUser);

public sealed record SpaceInvitationDto(
    Guid Id,
    Guid SpaceId,
    string SpaceName,
    string InviteeEmail,
    Guid InviterUserId,
    string InviterDisplayName,
    SpaceRole Role,
    SpaceInvitationStatus Status,
    DateTimeOffset InvitedAt,
    DateTimeOffset? RespondedAt,
    bool IsIncoming);

public sealed class SpaceService(
    IOrganizaClubDbContext db,
    IUserContext userContext,
    OrganizaClubDataPurgeService dataPurgeService,
    CommercialPlanService commercialPlanService,
    TimeProvider timeProvider)
{
    private const string SuperAdminReadOnlyMessage = "O superadmin possui acesso somente leitura nesta etapa.";

    public async Task<IReadOnlyCollection<SpaceDto>> ListAsync(CancellationToken cancellationToken)
    {
        if (userContext.SystemRole == SystemRole.SuperAdmin)
        {
            return await db.Spaces
                .AsNoTracking()
                .OrderBy(space => space.Name)
                .Select(space => new SpaceDto(
                    space.Id,
                    space.Name,
                    SpaceRole.Member,
                    space.CreatedAt,
                    space.CreatedByUserId == userContext.UserId))
                .ToArrayAsync(cancellationToken);
        }

        return await db.SpaceMembers
            .AsNoTracking()
            .Where(member => member.UserId == userContext.UserId && member.IsActive)
            .OrderBy(member => member.Space!.Name)
            .Select(member => new SpaceDto(
                member.SpaceId,
                member.Space!.Name,
                member.Role,
                member.Space!.CreatedAt,
                member.Space!.CreatedByUserId == userContext.UserId))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<SpaceDto> CreateAsync(CreateSpaceRequest request, CancellationToken cancellationToken)
    {
        EnsureWritable();
        await commercialPlanService.EnsureCanCreateSpaceAsync(userContext.UserId, cancellationToken);
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ValidationException("Informe o nome do espaço.");
        }

        var space = new Space
        {
            Name = request.Name.Trim(),
            CreatedByUserId = userContext.UserId
        };
        var member = new SpaceMember
        {
            Space = space,
            UserId = userContext.UserId,
            Role = SpaceRole.Owner
        };

        db.Spaces.Add(space);
        db.SpaceMembers.Add(member);
        db.FinanceCategories.AddRange(FinanceCategoryCatalog.CreateDefaults(space.Id, member.Id));
        db.NotificationPreferences.Add(new NotificationPreference
        {
            Space = space,
            SpaceMember = member
        });

        await db.SaveChangesAsync(cancellationToken);
        return ToSpaceDto(space, member.Role);
    }

    public async Task<SpaceDto> UpdateAsync(
        Guid spaceId,
        UpdateSpaceRequest request,
        CancellationToken cancellationToken)
    {
        EnsureWritable();
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ValidationException("Informe o nome do espaço.");
        }

        var member = await ResolveMembershipForSpaceAsync(spaceId, cancellationToken);
        EnsureOwner(member, "Somente o proprietário pode editar o espaço.");

        member.Space!.Name = request.Name.Trim();
        await db.SaveChangesAsync(cancellationToken);

        return ToSpaceDto(member.Space, member.Role);
    }

    public async Task DeleteAsync(Guid spaceId, CancellationToken cancellationToken)
    {
        EnsureWritable();
        var member = await ResolveMembershipForSpaceAsync(spaceId, cancellationToken);
        EnsureOwner(member, "Somente o proprietário pode excluir o espaço.");

        await dataPurgeService.DeleteSpaceAsync(spaceId, cancellationToken);
    }

    public async Task<IReadOnlyCollection<SpaceMemberDto>> ListMembersAsync(CancellationToken cancellationToken)
    {
        if (userContext.SystemRole == SystemRole.SuperAdmin)
        {
            var spaceId = await ResolveSuperAdminSpaceIdAsync(cancellationToken);
            return await db.SpaceMembers
                .AsNoTracking()
                .Include(member => member.User)
                .Where(member => member.SpaceId == spaceId && member.IsActive)
                .OrderBy(member => member.User!.DisplayName)
                .Select(member => new SpaceMemberDto(
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

        return await db.SpaceMembers
            .AsNoTracking()
            .Include(member => member.User)
            .Where(member => member.SpaceId == currentMember.SpaceId && member.IsActive)
            .OrderBy(member => member.User!.DisplayName)
            .Select(member => new SpaceMemberDto(
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

    public async Task<IReadOnlyCollection<SpaceInvitationDto>> ListInvitationsAsync(CancellationToken cancellationToken)
    {
        if (userContext.SystemRole == SystemRole.SuperAdmin)
        {
            return Array.Empty<SpaceInvitationDto>();
        }

        var currentUser = await db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(user => user.Id == userContext.UserId && user.IsActive, cancellationToken)
            ?? throw new ForbiddenException("Usuário não encontrado.");

        var currentEmail = NormalizeEmail(currentUser.Email);
        var invitations = await db.SpaceInvitations
            .AsNoTracking()
            .Include(item => item.Space)
            .Include(item => item.InviterUser)
            .Where(item =>
                item.InviteeEmail == currentEmail ||
                item.InviterUserId == currentUser.Id)
            .OrderByDescending(item => item.InvitedAt)
            .ToArrayAsync(cancellationToken);

        return invitations
            .Select(item => new SpaceInvitationDto(
                item.Id,
                item.SpaceId,
                item.Space?.Name ?? string.Empty,
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

    public async Task<SpaceInvitationDto> ShareAsync(ShareSpaceRequest request, CancellationToken cancellationToken)
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
            ?? throw new NotFoundException("Usuário não encontrado. Peça para a pessoa criar uma conta antes de compartilhar o espaço.");

        if (user.Id == userContext.UserId)
        {
            throw new ValidationException("Não é possível convidar você mesmo.");
        }

        var existingMember = await db.SpaceMembers
            .Include(member => member.User)
            .FirstOrDefaultAsync(member =>
                member.SpaceId == currentMember.SpaceId &&
                member.UserId == user.Id,
                cancellationToken);

        if (existingMember is { IsActive: true })
        {
            throw new ConflictException("Este usuário já participa deste espaço.");
        }

        await commercialPlanService.EnsureCanInviteMemberToSpaceAsync(
            currentMember.SpaceId,
            user.Id,
            cancellationToken);

        var invitation = await db.SpaceInvitations
            .Include(item => item.Space)
            .Include(item => item.InviterUser)
            .FirstOrDefaultAsync(item =>
                item.SpaceId == currentMember.SpaceId &&
                item.InviteeEmail == email,
                cancellationToken);

        if (invitation is null)
        {
            invitation = new SpaceInvitation
            {
                SpaceId = currentMember.SpaceId,
                InviterUserId = userContext.UserId,
                InviteeEmail = email,
                Role = role,
                Status = SpaceInvitationStatus.Pending,
                InvitedAt = timeProvider.GetUtcNow()
            };
            db.SpaceInvitations.Add(invitation);
        }
        else
        {
            invitation.InviterUserId = userContext.UserId;
            invitation.InviteeEmail = email;
            invitation.Role = role;
            invitation.Status = SpaceInvitationStatus.Pending;
            invitation.InvitedAt = timeProvider.GetUtcNow();
            invitation.RespondedAt = null;
        }

        await db.SaveChangesAsync(cancellationToken);
        return ToInvitationDto(invitation, currentEmail: NormalizeEmail(currentUserEmail));
    }

    public async Task<SpaceDto> AcceptInvitationAsync(Guid invitationId, CancellationToken cancellationToken)
    {
        EnsureWritable();
        var currentUser = await db.Users
            .FirstOrDefaultAsync(user => user.Id == userContext.UserId && user.IsActive, cancellationToken)
            ?? throw new ForbiddenException("Usuário não encontrado.");

        var currentEmail = NormalizeEmail(currentUser.Email);
        var invitation = await db.SpaceInvitations
            .Include(item => item.Space)
            .ThenInclude(space => space!.CreatedByUser)
            .FirstOrDefaultAsync(item => item.Id == invitationId, cancellationToken)
            ?? throw new NotFoundException("Convite não encontrado.");

        if (invitation.Status != SpaceInvitationStatus.Pending)
        {
            throw new ValidationException("Este convite já foi respondido.");
        }

        if (!string.Equals(invitation.InviteeEmail, currentEmail, StringComparison.OrdinalIgnoreCase))
        {
            throw new ForbiddenException("Você não tem acesso a este convite.");
        }

        var member = await db.SpaceMembers
            .Include(item => item.Space)
            .FirstOrDefaultAsync(item =>
                item.SpaceId == invitation.SpaceId &&
                item.UserId == currentUser.Id,
                cancellationToken);

        if (member is null)
        {
            member = new SpaceMember
            {
                SpaceId = invitation.SpaceId,
                UserId = currentUser.Id,
                User = currentUser,
                Role = invitation.Role,
                IsActive = true
            };
            db.SpaceMembers.Add(member);
            db.NotificationPreferences.Add(new NotificationPreference
            {
                SpaceId = invitation.SpaceId,
                SpaceMember = member,
                WhatsAppPhoneNumber = currentUser.PhoneNumber
            });
        }
        else
        {
            member.IsActive = true;
            member.Role = invitation.Role;

            if (!await db.NotificationPreferences.AnyAsync(
                preference => preference.SpaceMemberId == member.Id,
                cancellationToken))
            {
                db.NotificationPreferences.Add(new NotificationPreference
                {
                    SpaceId = invitation.SpaceId,
                    SpaceMember = member,
                    WhatsAppPhoneNumber = currentUser.PhoneNumber
                });
            }
        }

        invitation.Status = SpaceInvitationStatus.Accepted;
        invitation.RespondedAt = timeProvider.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);

        return ToSpaceDto(invitation.Space!, invitation.Role);
    }

    public async Task DeclineInvitationAsync(Guid invitationId, CancellationToken cancellationToken)
    {
        EnsureWritable();
        var currentUser = await db.Users
            .FirstOrDefaultAsync(user => user.Id == userContext.UserId && user.IsActive, cancellationToken)
            ?? throw new ForbiddenException("Usuário não encontrado.");

        var currentEmail = NormalizeEmail(currentUser.Email);
        var invitation = await db.SpaceInvitations
            .FirstOrDefaultAsync(item => item.Id == invitationId, cancellationToken)
            ?? throw new NotFoundException("Convite não encontrado.");

        if (invitation.Status != SpaceInvitationStatus.Pending)
        {
            throw new ValidationException("Este convite já foi respondido.");
        }

        if (!string.Equals(invitation.InviteeEmail, currentEmail, StringComparison.OrdinalIgnoreCase))
        {
            throw new ForbiddenException("Você não tem acesso a este convite.");
        }

        invitation.Status = SpaceInvitationStatus.Declined;
        invitation.RespondedAt = timeProvider.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<SpaceMemberDto> UpdateMemberAsync(
        Guid memberId,
        UpdateSpaceMemberRequest request,
        CancellationToken cancellationToken)
    {
        EnsureWritable();
        var currentMember = await ResolveCurrentMembershipAsync(requireManager: false, cancellationToken);
        EnsureOwner(currentMember, "Somente o proprietário pode editar membros.");

        var member = await db.SpaceMembers
            .Include(item => item.User)
            .FirstOrDefaultAsync(item =>
                item.Id == memberId &&
                item.SpaceId == currentMember.SpaceId &&
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

        var member = await db.SpaceMembers
            .Include(item => item.User)
            .FirstOrDefaultAsync(item =>
                item.Id == memberId &&
                item.SpaceId == currentMember.SpaceId &&
                item.IsActive,
                cancellationToken)
            ?? throw new NotFoundException("Membro não encontrado.");

        await EnsureOwnerChangeAllowedAsync(member, nextRole: null, cancellationToken);

        member.IsActive = false;
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<SpaceMember> ResolveCurrentMembershipAsync(
        bool requireManager,
        CancellationToken cancellationToken)
    {
        var query = db.SpaceMembers
            .Include(member => member.Space)
            .Where(member => member.UserId == userContext.UserId && member.IsActive);

        var member = userContext.SpaceId is null
            ? await ResolveSingleMembershipAsync(query, cancellationToken)
            : await query.FirstOrDefaultAsync(item => item.SpaceId == userContext.SpaceId.Value, cancellationToken)
                ?? throw new ForbiddenException("Você não tem acesso a este espaço.");

        if (requireManager && member.Role is not (SpaceRole.Owner or SpaceRole.Admin))
        {
            throw new ForbiddenException("Somente proprietários e administradores podem compartilhar o espaço.");
        }

        return member;
    }

    private async Task<SpaceMember> ResolveMembershipForSpaceAsync(
        Guid spaceId,
        CancellationToken cancellationToken)
    {
        return await db.SpaceMembers
            .Include(member => member.Space)
            .FirstOrDefaultAsync(member =>
                member.SpaceId == spaceId &&
                member.UserId == userContext.UserId &&
                member.IsActive,
                cancellationToken)
            ?? throw new ForbiddenException("Você não tem acesso a este espaço.");
    }

    private static void EnsureOwner(SpaceMember member, string message)
    {
        if (member.Role is not SpaceRole.Owner)
        {
            throw new ForbiddenException(message);
        }
    }

    private static async Task<SpaceMember> ResolveSingleMembershipAsync(
        IQueryable<SpaceMember> query,
        CancellationToken cancellationToken)
    {
        var memberships = await query.Take(2).ToArrayAsync(cancellationToken);
        return memberships.Length switch
        {
            0 => throw new ForbiddenException("Usuário sem espaço vinculado."),
            1 => memberships[0],
            _ => throw new ValidationException("Informe X-Space-Id para escolher o espaço.")
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

    private static SpaceRole NormalizeSharedRole(SpaceRole role)
    {
        return role switch
        {
            SpaceRole.Admin => SpaceRole.Admin,
            SpaceRole.Member => SpaceRole.Member,
            SpaceRole.Owner => throw new ValidationException("Compartilhe o espaço como administrador ou membro."),
            _ => throw new ValidationException("Perfil inválido para compartilhamento.")
        };
    }

    private static SpaceRole NormalizeEditableRole(SpaceRole role)
    {
        return role switch
        {
            SpaceRole.Admin => SpaceRole.Admin,
            SpaceRole.Member => SpaceRole.Member,
            SpaceRole.Owner => throw new ValidationException("Edite membros apenas como administrador ou membro."),
            _ => throw new ValidationException("Perfil inválido para edição.")
        };
    }

    private async Task EnsureOwnerChangeAllowedAsync(
        SpaceMember member,
        SpaceRole? nextRole,
        CancellationToken cancellationToken)
    {
        if (member.Role is not SpaceRole.Owner || nextRole is SpaceRole.Owner)
        {
            return;
        }

        var ownerCount = await db.SpaceMembers.CountAsync(
            item => item.SpaceId == member.SpaceId && item.IsActive && item.Role == SpaceRole.Owner,
            cancellationToken);

        if (ownerCount <= 1)
        {
            throw new ValidationException("O espaço precisa manter ao menos um proprietário ativo.");
        }
    }

    private static SpaceMemberDto ToMemberDto(SpaceMember member, Guid currentUserId)
    {
        return new SpaceMemberDto(
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

    private static SpaceInvitationDto ToInvitationDto(SpaceInvitation invitation, string currentEmail)
    {
        return new SpaceInvitationDto(
            invitation.Id,
            invitation.SpaceId,
            invitation.Space?.Name ?? string.Empty,
            invitation.InviteeEmail,
            invitation.InviterUserId,
            invitation.InviterUser?.DisplayName ?? string.Empty,
            invitation.Role,
            invitation.Status,
            invitation.InvitedAt,
            invitation.RespondedAt,
            invitation.InviteeEmail == currentEmail);
    }

    private SpaceDto ToSpaceDto(Space space, SpaceRole role)
    {
        return new SpaceDto(
            space.Id,
            space.Name,
            role,
            space.CreatedAt,
            space.CreatedByUserId == userContext.UserId);
    }

    private async Task<Guid> ResolveSuperAdminSpaceIdAsync(CancellationToken cancellationToken)
    {
        if (userContext.SpaceId is null)
        {
            var spaceIds = await db.Spaces
                .AsNoTracking()
                .OrderBy(space => space.Name)
                .Select(space => space.Id)
                .Take(2)
                .ToArrayAsync(cancellationToken);

            return spaceIds.Length switch
            {
                0 => throw new NotFoundException("Espaço não encontrada."),
                1 => spaceIds[0],
                _ => throw new ValidationException("Informe X-Space-Id para escolher o espaço.")
            };
        }

        var exists = await db.Spaces
            .AsNoTracking()
            .AnyAsync(space => space.Id == userContext.SpaceId.Value, cancellationToken);

        if (!exists)
        {
            throw new NotFoundException("Espaço não encontrada.");
        }

        return userContext.SpaceId.Value;
    }

    private void EnsureWritable()
    {
        if (userContext.SystemRole == SystemRole.SuperAdmin)
        {
            throw new ForbiddenException(SuperAdminReadOnlyMessage);
        }
    }
}
