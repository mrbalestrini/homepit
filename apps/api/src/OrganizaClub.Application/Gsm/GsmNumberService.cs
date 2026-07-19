using OrganizaClub.Application.Common;
using OrganizaClub.Domain.Gsm;
using OrganizaClub.Domain.Spaces;
using Microsoft.EntityFrameworkCore;

namespace OrganizaClub.Application.Gsm;

public sealed class GsmNumberService(
    IOrganizaClubDbContext db,
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
            .Where(item => item.SpaceId == currentMember.SpaceId)
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
        ValidateAcquiredOn(request.AcquiredOn);
        ValidateMonthlyCost(request.MonthlyCost);
        ValidateDaysWithoutRecharge(request.DaysWithoutRecharge);
        await EnsureUniqueNumberAsync(currentMember.SpaceId, normalizedNumber, null, cancellationToken);

        var gsmNumber = new GsmNumber
        {
            SpaceId = currentMember.SpaceId,
            CreatedByMemberId = currentMember.Id,
            Title = RequiredTitle(request.Title),
            NormalizedNumber = normalizedNumber,
            Description = NormalizeDescription(request.Description),
            Plan = request.Plan,
            MonthlyCost = request.MonthlyCost,
            DaysWithoutRecharge = request.DaysWithoutRecharge,
            AcquiredOn = request.AcquiredOn,
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
            .FirstOrDefaultAsync(item => item.Id == gsmNumberId && item.SpaceId == currentMember.SpaceId, cancellationToken)
            ?? throw new NotFoundException("Número GSM não encontrado.");

        EnsureCanManageEntity(currentMember, gsmNumber.CreatedByMemberId, "Você não pode adicionar recargas a um número GSM criado por outra pessoa.");

        var normalizedNumber = NormalizeNumber(request.Number);
        ValidateAcquiredOn(request.AcquiredOn);
        ValidateMonthlyCost(request.MonthlyCost);
        ValidateDaysWithoutRecharge(request.DaysWithoutRecharge);
        await EnsureUniqueNumberAsync(currentMember.SpaceId, normalizedNumber, gsmNumber.Id, cancellationToken);

        gsmNumber.Title = RequiredTitle(request.Title);
        gsmNumber.NormalizedNumber = normalizedNumber;
        gsmNumber.Description = NormalizeDescription(request.Description);
        gsmNumber.Plan = request.Plan;
        gsmNumber.MonthlyCost = request.MonthlyCost;
        gsmNumber.DaysWithoutRecharge = request.DaysWithoutRecharge;
        gsmNumber.AcquiredOn = request.AcquiredOn;
        gsmNumber.Status = request.Status;

        await db.SaveChangesAsync(cancellationToken);
        return ToDto(gsmNumber, currentMember);
    }

    public async Task DeleteAsync(Guid gsmNumberId, CancellationToken cancellationToken)
    {
        EnsureWritable();
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var gsmNumber = await db.GsmNumbers
            .FirstOrDefaultAsync(item => item.Id == gsmNumberId && item.SpaceId == currentMember.SpaceId, cancellationToken)
            ?? throw new NotFoundException("Número GSM não encontrado.");

        EnsureCanManageEntity(currentMember, gsmNumber.CreatedByMemberId, "Você não pode excluir um número GSM criado por outra pessoa.");

        db.GsmNumbers.Remove(gsmNumber);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<GsmRechargeDto>> ListRechargesAsync(Guid gsmNumberId, CancellationToken cancellationToken)
    {
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        await EnsureGsmNumberExistsAsync(currentMember.SpaceId, gsmNumberId, cancellationToken);

        var recharges = await db.GsmRecharges
            .AsNoTracking()
            .Where(item => item.SpaceId == currentMember.SpaceId && item.GsmNumberId == gsmNumberId)
            .OrderByDescending(item => item.RechargedOn)
            .ThenByDescending(item => item.CreatedAt)
            .ToArrayAsync(cancellationToken);

        return recharges
            .Select(item => ToRechargeDto(item, currentMember))
            .ToArray();
    }

    public async Task<GsmRechargeDto> CreateRechargeAsync(
        Guid gsmNumberId,
        CreateGsmRechargeRequest request,
        CancellationToken cancellationToken)
    {
        EnsureWritable();
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var gsmNumber = await db.GsmNumbers
            .FirstOrDefaultAsync(item => item.Id == gsmNumberId && item.SpaceId == currentMember.SpaceId, cancellationToken)
            ?? throw new NotFoundException("Número GSM não encontrado.");

        EnsureCanManageEntity(currentMember, gsmNumber.CreatedByMemberId, "Você não pode editar um número GSM criado por outra pessoa.");

        ValidateRechargeDate(request.RechargedOn, gsmNumber.AcquiredOn);
        ValidateRechargeAmount(request.Amount);

        var recharge = new GsmRecharge
        {
            SpaceId = gsmNumber.SpaceId,
            GsmNumberId = gsmNumber.Id,
            CreatedByMemberId = currentMember.Id,
            RechargedOn = request.RechargedOn,
            Amount = request.Amount,
            Note = NormalizeRechargeNote(request.Note)
        };

        db.GsmRecharges.Add(recharge);
        await db.SaveChangesAsync(cancellationToken);
        await RefreshLastRechargeOnAsync(gsmNumber, cancellationToken);

        return ToRechargeDto(recharge, currentMember);
    }

    public async Task<GsmRechargeDto> UpdateRechargeAsync(
        Guid gsmNumberId,
        Guid rechargeId,
        UpdateGsmRechargeRequest request,
        CancellationToken cancellationToken)
    {
        EnsureWritable();
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var gsmNumber = await db.GsmNumbers
            .FirstOrDefaultAsync(item => item.Id == gsmNumberId && item.SpaceId == currentMember.SpaceId, cancellationToken)
            ?? throw new NotFoundException("Número GSM não encontrado.");
        var recharge = await db.GsmRecharges
            .FirstOrDefaultAsync(item =>
                item.Id == rechargeId &&
                item.GsmNumberId == gsmNumberId &&
                item.SpaceId == currentMember.SpaceId,
                cancellationToken)
            ?? throw new NotFoundException("Recarga não encontrada.");

        EnsureCanManageEntity(currentMember, gsmNumber.CreatedByMemberId, "Você não pode editar recargas de um número GSM criado por outra pessoa.");
        EnsureCanManageEntity(currentMember, recharge.CreatedByMemberId, "Você não pode editar uma recarga criada por outra pessoa.");

        ValidateRechargeDate(request.RechargedOn, gsmNumber.AcquiredOn);
        ValidateRechargeAmount(request.Amount);

        recharge.RechargedOn = request.RechargedOn;
        recharge.Amount = request.Amount;
        recharge.Note = NormalizeRechargeNote(request.Note);

        await db.SaveChangesAsync(cancellationToken);
        await RefreshLastRechargeOnAsync(gsmNumber, cancellationToken);

        return ToRechargeDto(recharge, currentMember);
    }

    public async Task DeleteRechargeAsync(Guid gsmNumberId, Guid rechargeId, CancellationToken cancellationToken)
    {
        EnsureWritable();
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var gsmNumber = await db.GsmNumbers
            .FirstOrDefaultAsync(item => item.Id == gsmNumberId && item.SpaceId == currentMember.SpaceId, cancellationToken)
            ?? throw new NotFoundException("Número GSM não encontrado.");
        var recharge = await db.GsmRecharges
            .FirstOrDefaultAsync(item =>
                item.Id == rechargeId &&
                item.GsmNumberId == gsmNumberId &&
                item.SpaceId == currentMember.SpaceId,
                cancellationToken)
            ?? throw new NotFoundException("Recarga não encontrada.");

        EnsureCanManageEntity(currentMember, gsmNumber.CreatedByMemberId, "Você não pode excluir recargas de um número GSM criado por outra pessoa.");
        EnsureCanManageEntity(currentMember, recharge.CreatedByMemberId, "Você não pode excluir uma recarga criada por outra pessoa.");

        db.GsmRecharges.Remove(recharge);
        await db.SaveChangesAsync(cancellationToken);
        await RefreshLastRechargeOnAsync(gsmNumber, cancellationToken);
    }

    private async Task EnsureUniqueNumberAsync(
        Guid spaceId,
        string normalizedNumber,
        Guid? currentId,
        CancellationToken cancellationToken)
    {
        var exists = await db.GsmNumbers.AnyAsync(
            item =>
                item.SpaceId == spaceId &&
                item.NormalizedNumber == normalizedNumber &&
                item.Id != currentId,
            cancellationToken);

        if (exists)
        {
            throw new ValidationException("Este número GSM já está cadastrado neste espaço.");
        }
    }

    private async Task<Guid> ResolveSpaceIdAsync(CancellationToken cancellationToken)
    {
        if (userContext.SystemRole == SystemRole.SuperAdmin)
        {
            return await ResolveSuperAdminSpaceIdAsync(cancellationToken);
        }

        var memberships = await db.SpaceMembers
            .AsNoTracking()
            .Where(member => member.UserId == userContext.UserId && member.IsActive)
            .Select(member => member.SpaceId)
            .ToArrayAsync(cancellationToken);

        if (memberships.Length == 0)
        {
            throw new ForbiddenException("Usuário sem espaço vinculado.");
        }

        if (userContext.SpaceId is null)
        {
            if (memberships.Length == 1)
            {
                return memberships[0];
            }

            throw new ValidationException("Informe X-Space-Id para escolher o espaço.");
        }

        if (!memberships.Contains(userContext.SpaceId.Value))
        {
            throw new ForbiddenException("Você não tem acesso a este espaço.");
        }

        return userContext.SpaceId.Value;
    }

    private async Task<SpaceMember> ResolveCurrentMemberAsync(Guid spaceId, CancellationToken cancellationToken)
    {
        if (userContext.SystemRole == SystemRole.SuperAdmin)
        {
            return new SpaceMember
            {
                SpaceId = spaceId,
                UserId = userContext.UserId,
                Role = SpaceRole.Member
            };
        }

        return await db.SpaceMembers
            .Include(member => member.User)
            .FirstOrDefaultAsync(member =>
                member.SpaceId == spaceId &&
                member.UserId == userContext.UserId &&
                member.IsActive,
                cancellationToken)
            ?? throw new ForbiddenException("Você não tem acesso a este espaço.");
    }

    private async Task<SpaceMember> ResolveCurrentMemberAsync(CancellationToken cancellationToken)
    {
        var spaceId = await ResolveSpaceIdAsync(cancellationToken);
        return await ResolveCurrentMemberAsync(spaceId, cancellationToken);
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

    private void ValidateAcquiredOn(DateOnly acquiredOn)
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
    }

    private static void ValidateMonthlyCost(decimal? monthlyCost)
    {
        if (monthlyCost is null)
        {
            return;
        }

        if (monthlyCost < 0)
        {
            throw new ValidationException("O custo mensal da linha não pode ser negativo.");
        }
    }

    private static void ValidateDaysWithoutRecharge(int? daysWithoutRecharge)
    {
        if (!daysWithoutRecharge.HasValue)
        {
            return;
        }

        if (daysWithoutRecharge.Value <= 0)
        {
            throw new ValidationException("Os dias possíveis sem recarga devem ser um inteiro positivo.");
        }
    }

    private void ValidateRechargeDate(DateOnly rechargedOn, DateOnly acquiredOn)
    {
        if (rechargedOn == default)
        {
            throw new ValidationException("Informe a data da recarga.");
        }

        var today = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);

        if (rechargedOn > today)
        {
            throw new ValidationException("A data da recarga não pode estar no futuro.");
        }

        if (rechargedOn < acquiredOn)
        {
            throw new ValidationException("A data da recarga não pode ser anterior à data de aquisição.");
        }
    }

    private static void ValidateRechargeAmount(decimal? amount)
    {
        if (!amount.HasValue)
        {
            throw new ValidationException("Informe o valor da recarga.");
        }

        if (amount.Value <= 0)
        {
            throw new ValidationException("O valor da recarga deve ser maior que zero.");
        }
    }

    private static GsmNumberDto ToDto(GsmNumber gsmNumber, SpaceMember currentMember)
    {
        var canManage = CanManageEntity(currentMember, gsmNumber.CreatedByMemberId);
        return new GsmNumberDto(
            gsmNumber.Id,
            gsmNumber.Title,
            gsmNumber.NormalizedNumber,
            gsmNumber.Description,
            gsmNumber.Plan,
            gsmNumber.MonthlyCost,
            gsmNumber.DaysWithoutRecharge,
            gsmNumber.AcquiredOn,
            gsmNumber.LastRechargeOn,
            gsmNumber.Status,
            gsmNumber.CreatedByMemberId,
            gsmNumber.CreatedAt,
            gsmNumber.UpdatedAt,
            canManage,
            canManage);
    }

    private static GsmRechargeDto ToRechargeDto(GsmRecharge recharge, SpaceMember currentMember)
    {
        var canManage = CanManageEntity(currentMember, recharge.CreatedByMemberId);
        return new GsmRechargeDto(
            recharge.Id,
            recharge.GsmNumberId,
            recharge.RechargedOn,
            recharge.Amount,
            recharge.Note,
            recharge.CreatedByMemberId,
            recharge.CreatedAt,
            recharge.UpdatedAt,
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

    private static string? NormalizeRechargeNote(string? value)
    {
        var normalized = NormalizeOptional(value);
        if (normalized is not null && normalized.Length > DescriptionMaxLength)
        {
            throw new ValidationException($"A observação da recarga deve ter no máximo {DescriptionMaxLength} caracteres.");
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

    private async Task RefreshLastRechargeOnAsync(GsmNumber gsmNumber, CancellationToken cancellationToken)
    {
        var lastRechargeOn = await db.GsmRecharges
            .AsNoTracking()
            .Where(item => item.GsmNumberId == gsmNumber.Id)
            .MaxAsync(item => (DateOnly?)item.RechargedOn, cancellationToken);

        gsmNumber.LastRechargeOn = lastRechargeOn;
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureGsmNumberExistsAsync(Guid spaceId, Guid gsmNumberId, CancellationToken cancellationToken)
    {
        var exists = await db.GsmNumbers
            .AsNoTracking()
            .AnyAsync(item => item.Id == gsmNumberId && item.SpaceId == spaceId, cancellationToken);

        if (!exists)
        {
            throw new NotFoundException("Número GSM não encontrado.");
        }
    }

    private static bool IsContentManager(SpaceMember member)
    {
        return member.Role is SpaceRole.Owner or SpaceRole.Admin;
    }

    private static bool CanManageEntity(SpaceMember member, Guid? createdByMemberId)
    {
        return IsContentManager(member) || createdByMemberId == member.Id;
    }

    private static void EnsureCanManageEntity(SpaceMember member, Guid? createdByMemberId, string message)
    {
        if (!CanManageEntity(member, createdByMemberId))
        {
            throw new ForbiddenException(message);
        }
    }
}
