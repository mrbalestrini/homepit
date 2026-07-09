using HomePit.Application.Common;
using HomePit.Domain.Households;
using HomePit.Domain.Plans;
using Microsoft.EntityFrameworkCore;

namespace HomePit.Application.Plans;

public sealed class CommercialPlanService(
    IHomePitDbContext db,
    IUserContext userContext,
    TimeProvider timeProvider)
{
    public async Task EnsurePlanCatalogAsync(CancellationToken cancellationToken)
    {
        var existingSlugs = await db.PlanDefinitions
            .AsNoTracking()
            .Select(item => item.Slug)
            .ToArrayAsync(cancellationToken);

        var missingPlans = PlanDefinitionCatalog.Defaults
            .Where(seed => !existingSlugs.Contains(seed.Slug, StringComparer.OrdinalIgnoreCase))
            .Select(seed => new PlanDefinition
            {
                Slug = seed.Slug,
                Name = seed.Name,
                CurrencyCode = seed.CurrencyCode,
                MonthlyPrice = seed.MonthlyPrice,
                AnnualPrice = seed.AnnualPrice,
                MaxOwnedHouseholds = seed.MaxOwnedHouseholds,
                MaxUniversesPerHousehold = seed.MaxUniversesPerHousehold,
                MaxProjectsPerUniverse = seed.MaxProjectsPerUniverse,
                MaxOriginalImages = seed.MaxOriginalImages,
                SortOrder = seed.SortOrder
            })
            .ToArray();

        if (missingPlans.Length == 0)
        {
            return;
        }

        db.PlanDefinitions.AddRange(missingPlans);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<PlanDefinitionDto>> ListPlansAsync(CancellationToken cancellationToken)
    {
        EnsureSuperAdmin();
        await EnsurePlanCatalogAsync(cancellationToken);

        var plans = await db.PlanDefinitions
            .AsNoTracking()
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Name)
            .ToArrayAsync(cancellationToken);

        return plans.Select(ToPlanDefinitionDto).ToArray();
    }

    public async Task<PlanDefinitionDto> UpdatePlanAsync(
        Guid planId,
        UpdatePlanDefinitionRequest request,
        CancellationToken cancellationToken)
    {
        EnsureSuperAdmin();
        await EnsurePlanCatalogAsync(cancellationToken);
        ValidatePlanDefinition(request);

        var plan = await db.PlanDefinitions
            .FirstOrDefaultAsync(item => item.Id == planId, cancellationToken)
            ?? throw new NotFoundException("Plano não encontrado.");

        plan.MonthlyPrice = request.MonthlyPrice;
        plan.AnnualPrice = request.AnnualPrice;
        plan.MaxOwnedHouseholds = request.MaxOwnedHouseholds;
        plan.MaxUniversesPerHousehold = request.MaxUniversesPerHousehold;
        plan.MaxProjectsPerUniverse = request.MaxProjectsPerUniverse;
        plan.MaxOriginalImages = request.MaxOriginalImages;

        await db.SaveChangesAsync(cancellationToken);
        return ToPlanDefinitionDto(plan);
    }

    public async Task<IReadOnlyCollection<UserSubscriptionDto>> ListSubscriptionsAsync(CancellationToken cancellationToken)
    {
        EnsureSuperAdmin();
        await EnsurePlanCatalogAsync(cancellationToken);

        var subscriptions = await db.UserSubscriptions
            .AsNoTracking()
            .Include(item => item.User)
            .Include(item => item.PlanDefinition)
            .ToArrayAsync(cancellationToken);

        return subscriptions
            .OrderByDescending(item => item.StartsAt)
            .ThenByDescending(item => item.CreatedAt)
            .Select(item => ToUserSubscriptionDto(item, ResolveEffectiveSubscriptionStatus(item)))
            .ToArray();
    }

    public async Task<UserSubscriptionDto> CreateSubscriptionAsync(
        CreateUserSubscriptionRequest request,
        CancellationToken cancellationToken)
    {
        EnsureSuperAdmin();
        await EnsurePlanCatalogAsync(cancellationToken);

        var user = await db.Users
            .FirstOrDefaultAsync(item => item.Id == request.UserId && item.IsActive, cancellationToken)
            ?? throw new NotFoundException("Usuário não encontrado.");

        var plan = await db.PlanDefinitions
            .FirstOrDefaultAsync(item => item.Id == request.PlanDefinitionId, cancellationToken)
            ?? throw new NotFoundException("Plano não encontrado.");

        var normalized = NormalizeSubscriptionInput(
            request.StartsAt,
            request.EndsAt,
            request.AmountPaid,
            request.CurrencyCode,
            request.Status);

        await EnsureNoSubscriptionOverlapAsync(
            user.Id,
            normalized.StartsAt,
            normalized.EndsAt,
            ignoreSubscriptionId: null,
            normalized.Status,
            cancellationToken);

        var subscription = new UserSubscription
        {
            UserId = user.Id,
            User = user,
            PlanDefinitionId = plan.Id,
            PlanDefinition = plan,
            BillingCycle = request.BillingCycle,
            StartsAt = normalized.StartsAt,
            EndsAt = normalized.EndsAt,
            AmountPaid = normalized.AmountPaid,
            CurrencyCode = normalized.CurrencyCode,
            Status = normalized.Status,
            AdminNote = NormalizeOptional(request.AdminNote)
        };

        db.UserSubscriptions.Add(subscription);
        await db.SaveChangesAsync(cancellationToken);

        return ToUserSubscriptionDto(subscription, ResolveEffectiveSubscriptionStatus(subscription));
    }

    public async Task<UserSubscriptionDto> UpdateSubscriptionAsync(
        Guid subscriptionId,
        UpdateUserSubscriptionRequest request,
        CancellationToken cancellationToken)
    {
        EnsureSuperAdmin();
        await EnsurePlanCatalogAsync(cancellationToken);

        var subscription = await db.UserSubscriptions
            .Include(item => item.User)
            .Include(item => item.PlanDefinition)
            .FirstOrDefaultAsync(item => item.Id == subscriptionId, cancellationToken)
            ?? throw new NotFoundException("Assinatura não encontrada.");

        var user = await db.Users
            .FirstOrDefaultAsync(item => item.Id == request.UserId && item.IsActive, cancellationToken)
            ?? throw new NotFoundException("Usuário não encontrado.");

        var plan = await db.PlanDefinitions
            .FirstOrDefaultAsync(item => item.Id == request.PlanDefinitionId, cancellationToken)
            ?? throw new NotFoundException("Plano não encontrado.");

        var normalized = NormalizeSubscriptionInput(
            request.StartsAt,
            request.EndsAt,
            request.AmountPaid,
            request.CurrencyCode,
            request.Status);

        await EnsureNoSubscriptionOverlapAsync(
            user.Id,
            normalized.StartsAt,
            normalized.EndsAt,
            subscription.Id,
            normalized.Status,
            cancellationToken);

        subscription.UserId = user.Id;
        subscription.User = user;
        subscription.PlanDefinitionId = plan.Id;
        subscription.PlanDefinition = plan;
        subscription.BillingCycle = request.BillingCycle;
        subscription.StartsAt = normalized.StartsAt;
        subscription.EndsAt = normalized.EndsAt;
        subscription.AmountPaid = normalized.AmountPaid;
        subscription.CurrencyCode = normalized.CurrencyCode;
        subscription.Status = normalized.Status;
        subscription.AdminNote = NormalizeOptional(request.AdminNote);

        await db.SaveChangesAsync(cancellationToken);
        return ToUserSubscriptionDto(subscription, ResolveEffectiveSubscriptionStatus(subscription));
    }

    public async Task<CurrentUserPlanSummaryDto> GetCurrentUserPlanAsync(CancellationToken cancellationToken)
    {
        await EnsurePlanCatalogAsync(cancellationToken);

        var plan = await ResolveEffectivePlanDefinitionAsync(userContext.UserId, cancellationToken);
        var subscription = await GetActiveSubscriptionAsync(userContext.UserId, cancellationToken);
        var ownedHouseholdCount = await db.HouseholdMembers.CountAsync(
            item => item.UserId == userContext.UserId && item.IsActive && item.Role == HouseholdRole.Owner,
            cancellationToken);
        var managedOriginalImageCount = await db.UserPlanImageAssets.CountAsync(
            item => item.UserId == userContext.UserId && !item.IsDegraded,
            cancellationToken);
        int? activeHouseholdUniverseCount = userContext.HouseholdId.HasValue
            ? await db.Universes.CountAsync(item => item.HouseholdId == userContext.HouseholdId.Value, cancellationToken)
            : null;

        return new CurrentUserPlanSummaryDto(
            ToPlanDefinitionDto(plan),
            subscription is null ? null : ToUserSubscriptionDto(subscription, ResolveEffectiveSubscriptionStatus(subscription)),
            new PlanUsageSummaryDto(
                ownedHouseholdCount,
                managedOriginalImageCount,
                activeHouseholdUniverseCount));
    }

    public async Task<AdminUserCommercialSummaryDto> GetAdminUserCommercialSummaryAsync(Guid userId, CancellationToken cancellationToken)
    {
        await EnsurePlanCatalogAsync(cancellationToken);

        var plan = await ResolveEffectivePlanDefinitionAsync(userId, cancellationToken);
        var subscription = await GetActiveSubscriptionAsync(userId, cancellationToken);
        UserSubscriptionStatus? status = subscription is null ? null : ResolveEffectiveSubscriptionStatus(subscription);

        return new AdminUserCommercialSummaryDto(
            plan.Slug,
            plan.Name,
            subscription?.Id,
            subscription?.BillingCycle,
            subscription?.StartsAt,
            subscription?.EndsAt,
            subscription?.AmountPaid,
            subscription?.CurrencyCode,
            status);
    }

    public async Task<PlanDefinition> ResolveEffectivePlanDefinitionAsync(Guid userId, CancellationToken cancellationToken)
    {
        await EnsurePlanCatalogAsync(cancellationToken);

        var subscription = await GetActiveSubscriptionAsync(userId, cancellationToken);
        if (subscription?.PlanDefinition is not null)
        {
            return subscription.PlanDefinition;
        }

        return await db.PlanDefinitions
            .FirstAsync(item => item.Slug == PlanDefinitionCatalog.FreeSlug, cancellationToken);
    }

    public async Task EnsureCanCreateHouseholdAsync(Guid userId, CancellationToken cancellationToken)
    {
        var plan = await ResolveEffectivePlanDefinitionAsync(userId, cancellationToken);
        var ownedHouseholdCount = await db.HouseholdMembers.CountAsync(
            item => item.UserId == userId && item.IsActive && item.Role == HouseholdRole.Owner,
            cancellationToken);

        if (ownedHouseholdCount >= plan.MaxOwnedHouseholds)
        {
            throw new ValidationException(BuildOwnedHouseholdLimitMessage(plan));
        }
    }

    public async Task EnsureCanCreateUniverseAsync(Guid userId, Guid householdId, CancellationToken cancellationToken)
    {
        var plan = await ResolveEffectivePlanDefinitionAsync(userId, cancellationToken);
        var universeCount = await db.Universes.CountAsync(item => item.HouseholdId == householdId, cancellationToken);

        if (universeCount >= plan.MaxUniversesPerHousehold)
        {
            throw new ValidationException(
                $"O plano {plan.Name} permite até {plan.MaxUniversesPerHousehold} universo(s) por casa.");
        }
    }

    public async Task EnsureCanCreateProjectAsync(Guid userId, Guid universeId, CancellationToken cancellationToken)
    {
        var plan = await ResolveEffectivePlanDefinitionAsync(userId, cancellationToken);
        var projectCount = await db.Projects.CountAsync(item => item.UniverseId == universeId, cancellationToken);

        if (projectCount >= plan.MaxProjectsPerUniverse)
        {
            throw new ValidationException(
                $"O plano {plan.Name} permite até {plan.MaxProjectsPerUniverse} projeto(s) por universo.");
        }
    }

    public static string BuildImagePolicyDescription(PlanDefinition plan)
    {
        if (plan.MaxOriginalImages <= 0)
        {
            return "Novas imagens privadas governadas por plano podem ser rebaixadas imediatamente para WEBP com até 300 px e qualidade 30%.";
        }

        return
            $"Mantém até {plan.MaxOriginalImages} imagem(ns) privada(s) recente(s) em qualidade original; a partir da imagem {plan.MaxOriginalImages + 1}, a mais antiga é substituída por WEBP com até 300 px e qualidade 30%.";
    }

    private static string BuildOwnedHouseholdLimitMessage(PlanDefinition plan)
    {
        return plan.MaxOwnedHouseholds == 0
            ? $"O plano {plan.Name} não permite criar casas próprias."
            : $"O plano {plan.Name} permite até {plan.MaxOwnedHouseholds} casa(s) própria(s).";
    }

    private async Task<UserSubscription?> GetActiveSubscriptionAsync(Guid userId, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var subscriptions = await db.UserSubscriptions
            .AsNoTracking()
            .Include(item => item.User)
            .Include(item => item.PlanDefinition)
            .Where(item => item.UserId == userId)
            .ToArrayAsync(cancellationToken);

        return subscriptions
            .OrderByDescending(item => item.StartsAt)
            .ThenByDescending(item => item.CreatedAt)
            .FirstOrDefault(item => ResolveEffectiveSubscriptionStatus(item, now) == UserSubscriptionStatus.Active);
    }

    private UserSubscriptionStatus ResolveEffectiveSubscriptionStatus(UserSubscription subscription)
    {
        return ResolveEffectiveSubscriptionStatus(subscription, timeProvider.GetUtcNow());
    }

    private static UserSubscriptionStatus ResolveEffectiveSubscriptionStatus(
        UserSubscription subscription,
        DateTimeOffset now)
    {
        if (subscription.Status == UserSubscriptionStatus.Cancelled)
        {
            return UserSubscriptionStatus.Cancelled;
        }

        if (now < subscription.StartsAt)
        {
            return UserSubscriptionStatus.Scheduled;
        }

        if (subscription.EndsAt < now)
        {
            return UserSubscriptionStatus.Expired;
        }

        return UserSubscriptionStatus.Active;
    }

    private async Task EnsureNoSubscriptionOverlapAsync(
        Guid userId,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt,
        Guid? ignoreSubscriptionId,
        UserSubscriptionStatus normalizedStatus,
        CancellationToken cancellationToken)
    {
        if (normalizedStatus == UserSubscriptionStatus.Cancelled)
        {
            return;
        }

        var overlapping = await db.UserSubscriptions
            .AsNoTracking()
            .Where(item => item.UserId == userId)
            .Where(item => ignoreSubscriptionId == null || item.Id != ignoreSubscriptionId.Value)
            .ToArrayAsync(cancellationToken);

        if (overlapping.Any(item =>
                ResolveEffectiveSubscriptionStatus(item) != UserSubscriptionStatus.Cancelled &&
                startsAt <= item.EndsAt &&
                item.StartsAt <= endsAt))
        {
            throw new ConflictException("Já existe uma assinatura ativa ou agendada sobreposta para este usuário.");
        }
    }

    private (DateTimeOffset StartsAt, DateTimeOffset EndsAt, decimal AmountPaid, string CurrencyCode, UserSubscriptionStatus Status) NormalizeSubscriptionInput(
        DateTimeOffset startsAt,
        DateTimeOffset endsAt,
        decimal amountPaid,
        string currencyCode,
        UserSubscriptionStatus requestedStatus)
    {
        if (endsAt < startsAt)
        {
            throw new ValidationException("A vigência final da assinatura não pode ser anterior ao início.");
        }

        if (amountPaid < 0)
        {
            throw new ValidationException("O valor pago da assinatura não pode ser negativo.");
        }

        var normalizedCurrencyCode = NormalizeCurrencyCode(currencyCode);
        var normalizedStatus = requestedStatus == UserSubscriptionStatus.Cancelled
            ? UserSubscriptionStatus.Cancelled
            : ResolveEffectiveSubscriptionStatus(
                new UserSubscription
                {
                    StartsAt = startsAt,
                    EndsAt = endsAt,
                    AmountPaid = amountPaid,
                    CurrencyCode = normalizedCurrencyCode,
                    Status = requestedStatus
                },
                timeProvider.GetUtcNow());

        return (startsAt, endsAt, amountPaid, normalizedCurrencyCode, normalizedStatus);
    }

    private static string NormalizeCurrencyCode(string value)
    {
        var normalized = NormalizeRequiredText(value, "Informe a moeda da assinatura.").ToUpperInvariant();
        if (normalized.Length != 3)
        {
            throw new ValidationException("A moeda da assinatura deve usar um código de 3 letras.");
        }

        return normalized;
    }

    private static void ValidatePlanDefinition(UpdatePlanDefinitionRequest request)
    {
        if (request.MonthlyPrice < 0 || request.AnnualPrice < 0)
        {
            throw new ValidationException("Os preços do plano não podem ser negativos.");
        }

        if (request.MaxOwnedHouseholds < 0 ||
            request.MaxUniversesPerHousehold < 0 ||
            request.MaxProjectsPerUniverse < 0 ||
            request.MaxOriginalImages < 0)
        {
            throw new ValidationException("Os limites do plano não podem ser negativos.");
        }
    }

    private void EnsureSuperAdmin()
    {
        if (userContext.SystemRole != SystemRole.SuperAdmin)
        {
            throw new ForbiddenException("Somente o superadmin pode gerenciar planos e assinaturas.");
        }
    }

    private static string NormalizeRequiredText(string? value, string message)
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

    private static PlanDefinitionDto ToPlanDefinitionDto(PlanDefinition item)
    {
        return new PlanDefinitionDto(
            item.Id,
            item.Slug,
            item.Name,
            item.CurrencyCode,
            item.MonthlyPrice,
            item.AnnualPrice,
            item.MaxOwnedHouseholds,
            item.MaxUniversesPerHousehold,
            item.MaxProjectsPerUniverse,
            item.MaxOriginalImages,
            BuildImagePolicyDescription(item));
    }

    private static UserSubscriptionDto ToUserSubscriptionDto(UserSubscription item, UserSubscriptionStatus status)
    {
        return new UserSubscriptionDto(
            item.Id,
            item.UserId,
            item.User?.DisplayName ?? string.Empty,
            item.User?.Email ?? string.Empty,
            item.PlanDefinitionId,
            item.PlanDefinition?.Slug ?? string.Empty,
            item.PlanDefinition?.Name ?? string.Empty,
            item.BillingCycle,
            item.StartsAt,
            item.EndsAt,
            item.AmountPaid,
            item.CurrencyCode,
            status,
            item.AdminNote);
    }
}
