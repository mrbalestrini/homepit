using HomePit.Application.Auth;
using HomePit.Application.Common;
using HomePit.Domain.Households;
using HomePit.Domain.Notifications;
using Microsoft.EntityFrameworkCore;

namespace HomePit.Application.Households;

public sealed record CreateHouseholdRequest(string Name);

public sealed record UpdateHouseholdRequest(string Name);

public sealed record ShareHouseholdRequest(string Email, HouseholdRole Role);

public sealed record HouseholdMemberDto(
    Guid Id,
    Guid UserId,
    string DisplayName,
    string Email,
    string? PhoneNumber,
    HouseholdRole Role,
    bool IsCurrentUser);

public sealed class HouseholdService(IHomePitDbContext db, IUserContext userContext)
{
    public async Task<IReadOnlyCollection<HouseholdDto>> ListAsync(CancellationToken cancellationToken)
    {
        return await db.HouseholdMembers
            .AsNoTracking()
            .Where(member => member.UserId == userContext.UserId && member.IsActive)
            .OrderBy(member => member.Household!.Name)
            .Select(member => new HouseholdDto(member.HouseholdId, member.Household!.Name, member.Role))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<HouseholdDto> CreateAsync(CreateHouseholdRequest request, CancellationToken cancellationToken)
    {
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
        db.NotificationPreferences.Add(new NotificationPreference
        {
            Household = household,
            HouseholdMember = member
        });

        await db.SaveChangesAsync(cancellationToken);
        return new HouseholdDto(household.Id, household.Name, member.Role);
    }

    public async Task<HouseholdDto> UpdateAsync(
        Guid householdId,
        UpdateHouseholdRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ValidationException("Informe o nome da casa.");
        }

        var member = await ResolveMembershipForHouseholdAsync(householdId, cancellationToken);
        EnsureOwner(member, "Somente o proprietário pode editar a casa.");

        member.Household!.Name = request.Name.Trim();
        await db.SaveChangesAsync(cancellationToken);

        return new HouseholdDto(member.HouseholdId, member.Household.Name, member.Role);
    }

    public async Task DeleteAsync(Guid householdId, CancellationToken cancellationToken)
    {
        var member = await ResolveMembershipForHouseholdAsync(householdId, cancellationToken);
        EnsureOwner(member, "Somente o proprietário pode excluir a casa.");

        await db.ActivityComments
            .Where(comment => comment.HouseholdId == householdId)
            .ExecuteDeleteAsync(cancellationToken);

        db.Households.Remove(member.Household!);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<HouseholdMemberDto>> ListMembersAsync(CancellationToken cancellationToken)
    {
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
                member.Role,
                member.UserId == userContext.UserId))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<HouseholdMemberDto> ShareAsync(ShareHouseholdRequest request, CancellationToken cancellationToken)
    {
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

    private static HouseholdMemberDto ToMemberDto(HouseholdMember member, Guid currentUserId)
    {
        return new HouseholdMemberDto(
            member.Id,
            member.UserId,
            member.User?.DisplayName ?? string.Empty,
            member.User?.Email ?? string.Empty,
            member.User?.PhoneNumber,
            member.Role,
            member.UserId == currentUserId);
    }
}
