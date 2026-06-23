using HomePit.Application.Common;
using HomePit.Domain.Gsm;
using HomePit.Domain.Households;
using Microsoft.EntityFrameworkCore;

namespace HomePit.Application.Gsm;

public sealed class GsmNumberService(
    IHomePitDbContext db,
    IUserContext userContext,
    TimeProvider timeProvider)
{
    private const string SuperAdminReadOnlyMessage = "O superadmin possui acesso somente leitura nesta etapa.";
    private const int TitleMaxLength = 160;
    private const int DescriptionMaxLength = 4000;

    public async Task<IReadOnlyCollection<GsmNumberDto>> ListAsync(CancellationToken cancellationToken)
    {
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);

        var numbers = await db.GsmNumbers
            .AsNoTracking()
            .Where(item => item.HouseholdId == currentMember.HouseholdId)
            .ToArrayAsync(cancellationToken);

        return numbers
            .Select(item => ToDto(item, currentMember))
            .OrderBy(item => item.LastRechargeOn.HasValue ? 1 : 0)
            .ThenBy(item => item.LastRechargeOn)
            .ThenBy(item => item.Title)
            .ToArray();
    }

    public async Task<GsmNumberDto> CreateAsync(CreateGsmNumberRequest request, CancellationToken cancellationToken)
    {
        EnsureWritable();
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var normalizedNumber = NormalizeNumber(request.Number);
        ValidateDates(request.AcquiredOn, request.LastRechargeOn);
        await EnsureUniqueNumberAsync(currentMember.HouseholdId, normalizedNumber, null, cancellationToken);

        var gsmNumber = new GsmNumber
        {
            HouseholdId = currentMember.HouseholdId,
            CreatedByMemberId = currentMember.Id,
            Title = RequiredTitle(request.Title),
            NormalizedNumber = normalizedNumber,
            Description = NormalizeDescription(request.Description),
            AcquiredOn = request.AcquiredOn,
            LastRechargeOn = request.LastRechargeOn,
            Status = request.Status
        };

        db.GsmNumbers.Add(gsmNumber);
        await db.SaveChangesAsync(cancellationToken);

        return ToDto(gsmNumber, currentMember);
    }

    public async Task<GsmNumberDto> UpdateAsync(Guid gsmNumberId, UpdateGsmNumberRequest request, CancellationToken cancellationToken)
    {
        EnsureWritable();
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var gsmNumber = await db.GsmNumbers
            .FirstOrDefaultAsync(item => item.Id == gsmNumberId && item.HouseholdId == currentMember.HouseholdId, cancellationToken)
            ?? throw new NotFoundException("Número GSM não encontrado.");

        EnsureCanManageEntity(currentMember, gsmNumber.CreatedByMemberId, "Você não pode editar um número GSM criado por outra pessoa.");

        var normalizedNumber = NormalizeNumber(request.Number);
        ValidateDates(request.AcquiredOn, request.LastRechargeOn);
        await EnsureUniqueNumberAsync(currentMember.HouseholdId, normalizedNumber, gsmNumber.Id, cancellationToken);

        gsmNumber.Title = RequiredTitle(request.Title);
        gsmNumber.NormalizedNumber = normalizedNumber;
        gsmNumber.Description = NormalizeDescription(request.Description);
        gsmNumber.AcquiredOn = request.AcquiredOn;
        gsmNumber.LastRechargeOn = request.LastRechargeOn;
        gsmNumber.Status = request.Status;

        await db.SaveChangesAsync(cancellationToken);
        return ToDto(gsmNumber, currentMember);
    }

    public async Task DeleteAsync(Guid gsmNumberId, CancellationToken cancellationToken)
    {
        EnsureWritable();
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var gsmNumber = await db.GsmNumbers
            .FirstOrDefaultAsync(item => item.Id == gsmNumberId && item.HouseholdId == currentMember.HouseholdId, cancellationToken)
            ?? throw new NotFoundException("Número GSM não encontrado.");

        EnsureCanManageEntity(currentMember, gsmNumber.CreatedByMemberId, "Você não pode excluir um número GSM criado por outra pessoa.");

        db.GsmNumbers.Remove(gsmNumber);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureUniqueNumberAsync(
        Guid householdId,
        string normalizedNumber,
        Guid? currentId,
        CancellationToken cancellationToken)
    {
        var exists = await db.GsmNumbers.AnyAsync(
            item =>
                item.HouseholdId == householdId &&
                item.NormalizedNumber == normalizedNumber &&
                item.Id != currentId,
            cancellationToken);

        if (exists)
        {
            throw new ValidationException("Este número GSM já está cadastrado nesta casa.");
        }
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

    private async Task<HouseholdMember> ResolveCurrentMemberAsync(Guid householdId, CancellationToken cancellationToken)
    {
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

    private async Task<HouseholdMember> ResolveCurrentMemberAsync(CancellationToken cancellationToken)
    {
        var householdId = await ResolveHouseholdIdAsync(cancellationToken);
        return await ResolveCurrentMemberAsync(householdId, cancellationToken);
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

    private void ValidateDates(DateOnly acquiredOn, DateOnly? lastRechargeOn)
    {
        if (acquiredOn == default)
        {
            throw new ValidationException("Informe a data de aquisição.");
        }

        var today = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);

        if (acquiredOn > today)
        {
            throw new ValidationException("A data de aquisição não pode estar no futuro.");
        }

        if (lastRechargeOn is null)
        {
            return;
        }

        if (lastRechargeOn > today)
        {
            throw new ValidationException("A data da última recarga não pode estar no futuro.");
        }

        if (lastRechargeOn < acquiredOn)
        {
            throw new ValidationException("A data da última recarga não pode ser anterior à data de aquisição.");
        }
    }

    private static GsmNumberDto ToDto(GsmNumber gsmNumber, HouseholdMember currentMember)
    {
        var canManage = CanManageEntity(currentMember, gsmNumber.CreatedByMemberId);
        return new GsmNumberDto(
            gsmNumber.Id,
            gsmNumber.Title,
            gsmNumber.NormalizedNumber,
            gsmNumber.Description,
            gsmNumber.AcquiredOn,
            gsmNumber.LastRechargeOn,
            gsmNumber.Status,
            gsmNumber.CreatedByMemberId,
            gsmNumber.CreatedAt,
            gsmNumber.UpdatedAt,
            canManage,
            canManage);
    }

    private static string RequiredTitle(string value)
    {
        var normalized = RequiredText(value, "Informe o título do número GSM.");
        if (normalized.Length > TitleMaxLength)
        {
            throw new ValidationException($"O título do número GSM deve ter no máximo {TitleMaxLength} caracteres.");
        }

        return normalized;
    }

    private static string? NormalizeDescription(string? value)
    {
        var normalized = NormalizeOptional(value);
        if (normalized is not null && normalized.Length > DescriptionMaxLength)
        {
            throw new ValidationException($"A descrição do número GSM deve ter no máximo {DescriptionMaxLength} caracteres.");
        }

        return normalized;
    }

    private static string RequiredText(string value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ValidationException(message);
        }

        return value.Trim();
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string NormalizeNumber(string value)
    {
        var digits = new string((value ?? string.Empty).Where(char.IsDigit).ToArray());
        return digits.Length switch
        {
            11 => $"55{digits}",
            13 => digits,
            _ => throw new ValidationException("Informe um número GSM válido com DDI opcional e DDD obrigatório.")
        };
    }

    private static bool IsContentManager(HouseholdMember member)
    {
        return member.Role is HouseholdRole.Owner or HouseholdRole.Admin;
    }

    private static bool CanManageEntity(HouseholdMember member, Guid? createdByMemberId)
    {
        return IsContentManager(member) || createdByMemberId == member.Id;
    }

    private static void EnsureCanManageEntity(HouseholdMember member, Guid? createdByMemberId, string message)
    {
        if (!CanManageEntity(member, createdByMemberId))
        {
            throw new ForbiddenException(message);
        }
    }
}
