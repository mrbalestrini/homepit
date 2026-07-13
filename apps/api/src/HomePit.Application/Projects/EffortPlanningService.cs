using HomePit.Application.Common;
using HomePit.Domain.Households;
using HomePit.Domain.Projects;
using Microsoft.EntityFrameworkCore;

namespace HomePit.Application.Projects;

public sealed class EffortPlanningService(IHomePitDbContext db, IUserContext userContext)
{
    private const string SuperAdminReadOnlyMessage = "O superadmin possui acesso somente leitura nesta etapa.";

    public async Task<EffortPlanDto> GetPlanAsync(CancellationToken cancellationToken)
    {
        var member = await ResolveCurrentMemberAsync(cancellationToken);
        return await GetPlanForMemberAsync(member, cancellationToken);
    }

    public async Task<EffortPlanDto> UpdatePlanAsync(UpdateEffortPlanRequest request, CancellationToken cancellationToken)
    {
        EnsureWritable();
        var member = await ResolveCurrentMemberAsync(cancellationToken);
        var inputs = request.Allocations ?? [];
        var duplicate = inputs
            .GroupBy(item => (item.ScopeType, item.ScopeId, item.Weekday))
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicate is not null)
        {
            throw new ValidationException("Informe apenas um esforço para cada escopo e dia.");
        }

        if (inputs.Any(item => item.Points < 0))
        {
            throw new ValidationException("O esforço não pode ser negativo.");
        }

        var universeIds = inputs
            .Where(item => item.ScopeType == EffortScopeType.Universe)
            .Select(item => item.ScopeId ?? Guid.Empty)
            .ToArray();
        var projectIds = inputs
            .Where(item => item.ScopeType == EffortScopeType.Project)
            .Select(item => item.ScopeId ?? Guid.Empty)
            .ToArray();

        if (inputs.Any(item =>
                (item.ScopeType == EffortScopeType.Household && item.ScopeId.HasValue) ||
                (item.ScopeType != EffortScopeType.Household && !item.ScopeId.HasValue)))
        {
            throw new ValidationException("O escopo do esforço é inválido.");
        }

        var validUniverseIds = await db.Universes
            .Where(item => item.HouseholdId == member.HouseholdId && universeIds.Contains(item.Id))
            .Select(item => item.Id)
            .ToArrayAsync(cancellationToken);
        var validProjectIds = await db.Projects
            .Where(item => item.HouseholdId == member.HouseholdId && projectIds.Contains(item.Id))
            .Select(item => item.Id)
            .ToArrayAsync(cancellationToken);

        if (validUniverseIds.Length != universeIds.Distinct().Count() || validProjectIds.Length != projectIds.Distinct().Count())
        {
            throw new ValidationException("Escolha apenas Universos e Projetos desta Casa.");
        }

        var replacement = inputs.Select(item => new MemberEffortAllocation
        {
            HouseholdId = member.HouseholdId,
            HouseholdMemberId = member.Id,
            ScopeType = item.ScopeType,
            UniverseId = item.ScopeType == EffortScopeType.Universe ? item.ScopeId : null,
            ProjectId = item.ScopeType == EffortScopeType.Project ? item.ScopeId : null,
            Weekday = item.Weekday,
            Points = item.Points
        }).ToArray();

        var structure = await LoadStructureAsync(member.HouseholdId, cancellationToken);
        BuildPlanState(structure, replacement);

        var current = await db.MemberEffortAllocations
            .Where(item => item.HouseholdMemberId == member.Id)
            .ToArrayAsync(cancellationToken);
        db.MemberEffortAllocations.RemoveRange(current);
        db.MemberEffortAllocations.AddRange(replacement);
        await db.SaveChangesAsync(cancellationToken);

        return await GetPlanForMemberAsync(member, cancellationToken);
    }

    public async Task<ActivityRelevanceResponse> GetRelevanceAsync(
        DateOnly date,
        int utcOffsetMinutes,
        CancellationToken cancellationToken)
    {
        if (utcOffsetMinutes is < -840 or > 840)
        {
            throw new ValidationException("O fuso horário informado é inválido.");
        }

        var member = await ResolveCurrentMemberAsync(cancellationToken);
        var structure = await LoadStructureAsync(member.HouseholdId, cancellationToken);
        var allocations = await db.MemberEffortAllocations
            .AsNoTracking()
            .Where(item => item.HouseholdMemberId == member.Id)
            .ToArrayAsync(cancellationToken);
        var plan = BuildPlanState(structure, allocations);
        var weekday = ToEffortWeekday(date.DayOfWeek);
        var dayPlan = plan.Days[weekday];
        var activities = await db.Activities
            .AsNoTracking()
            .Include(activity => activity.Project)
            .Where(activity =>
                activity.HouseholdId == member.HouseholdId &&
                activity.Status != ActivityStatus.Concluido &&
                (activity.ResponsibleMemberId == null || activity.ResponsibleMemberId == member.Id))
            .ToArrayAsync(cancellationToken);

        var offset = TimeSpan.FromMinutes(utcOffsetMinutes);
        var candidates = activities
            .Select(activity => CreateCandidate(activity, member.Id, date, offset))
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Activity.DueDate ?? DateOnly.MaxValue)
            .ThenBy(item => item.Activity.CreatedAt)
            .ThenBy(item => item.Activity.Size ?? decimal.MaxValue)
            .ThenBy(item => item.Activity.Title, StringComparer.Ordinal)
            .ThenBy(item => item.Activity.Id)
            .ToArray();

        var remaining = BuildBuckets(dayPlan);
        var scheduled = new List<Candidate>();
        var overflow = new List<Candidate>();
        var missing = new List<Candidate>();

        foreach (var candidate in candidates)
        {
            if (candidate.Activity.Size is not > 0)
            {
                missing.Add(candidate);
                continue;
            }

            var bucket = ResolveBucket(candidate.Activity.ProjectId, candidate.Activity.Project!.UniverseId, dayPlan);
            if (candidate.Activity.Size <= remaining.GetValueOrDefault(bucket))
            {
                remaining[bucket] -= candidate.Activity.Size.Value;
                scheduled.Add(candidate);
            }
            else
            {
                overflow.Add(candidate);
            }
        }

        var ordered = scheduled
            .Select(item => (Item: item, State: ActivityRelevanceQueueState.Scheduled))
            .Concat(overflow.Select(item => (Item: item, State: ActivityRelevanceQueueState.Overflow)))
            .Concat(missing.Select(item => (Item: item, State: ActivityRelevanceQueueState.MissingEstimate)))
            .ToArray();

        var items = ordered.Select((item, index) => new ActivityRelevanceDto(
            item.Item.Activity.Id,
            index + 1,
            item.Item.Score,
            item.Item.PriorityScore,
            item.Item.DueDateScore,
            item.Item.AgeScore,
            item.Item.AssignmentScore,
            item.State)).ToArray();

        return new ActivityRelevanceResponse(
            date,
            weekday,
            dayPlan.HouseholdEffective,
            scheduled.Sum(item => item.Activity.Size!.Value),
            items);
    }

    private async Task<EffortPlanDto> GetPlanForMemberAsync(HouseholdMember member, CancellationToken cancellationToken)
    {
        var structure = await LoadStructureAsync(member.HouseholdId, cancellationToken);
        var allocations = await db.MemberEffortAllocations
            .AsNoTracking()
            .Where(item => item.HouseholdMemberId == member.Id)
            .ToArrayAsync(cancellationToken);
        var plan = BuildPlanState(structure, allocations);
        var weekdays = Enum.GetValues<EffortWeekday>();
        var scopes = new List<EffortPlanScopeDto>
        {
            new(
                EffortScopeType.Household,
                null,
                null,
                "Casa",
                weekdays.Select(day => new EffortPlanDayDto(
                    day,
                    plan.Days[day].HouseholdExplicit,
                    plan.Days[day].HouseholdEffective,
                    plan.Days[day].HouseholdShared)).ToArray())
        };

        foreach (var universe in structure.Universes.OrderBy(item => item.Name))
        {
            scopes.Add(new EffortPlanScopeDto(
                EffortScopeType.Universe,
                universe.Id,
                null,
                universe.Name,
                weekdays.Select(day => new EffortPlanDayDto(
                    day,
                    plan.Days[day].UniverseExplicit[universe.Id],
                    plan.Days[day].UniverseEffective[universe.Id],
                    plan.Days[day].UniverseShared[universe.Id])).ToArray()));

            foreach (var project in structure.Projects.Where(item => item.UniverseId == universe.Id).OrderBy(item => item.Name))
            {
                scopes.Add(new EffortPlanScopeDto(
                    EffortScopeType.Project,
                    project.Id,
                    universe.Id,
                    project.Name,
                    weekdays.Select(day => new EffortPlanDayDto(
                        day,
                        plan.Days[day].ProjectExplicit[project.Id],
                        plan.Days[day].ProjectExplicit[project.Id] ?? 0,
                        0)).ToArray()));
            }
        }

        return new EffortPlanDto(member.HouseholdId, member.Id, scopes);
    }

    private async Task<Structure> LoadStructureAsync(Guid householdId, CancellationToken cancellationToken)
    {
        var universes = await db.Universes
            .AsNoTracking()
            .Where(item => item.HouseholdId == householdId)
            .Select(item => new UniverseScope(item.Id, item.Name))
            .ToArrayAsync(cancellationToken);
        var projects = await db.Projects
            .AsNoTracking()
            .Where(item => item.HouseholdId == householdId)
            .Select(item => new ProjectScope(item.Id, item.UniverseId, item.Name))
            .ToArrayAsync(cancellationToken);
        return new Structure(universes, projects);
    }

    private static PlanState BuildPlanState(Structure structure, IReadOnlyCollection<MemberEffortAllocation> allocations)
    {
        var explicitPoints = allocations.ToDictionary(
            item => (item.ScopeType, ScopeId(item), item.Weekday),
            item => (decimal?)item.Points);
        var days = new Dictionary<EffortWeekday, DayPlan>();

        foreach (var weekday in Enum.GetValues<EffortWeekday>())
        {
            decimal? Lookup(EffortScopeType scopeType, Guid? scopeId) =>
                explicitPoints.GetValueOrDefault((scopeType, scopeId, weekday));

            var projectExplicit = structure.Projects.ToDictionary(project => project.Id, project => Lookup(EffortScopeType.Project, project.Id));
            var universeExplicit = structure.Universes.ToDictionary(universe => universe.Id, universe => Lookup(EffortScopeType.Universe, universe.Id));
            var universeEffective = new Dictionary<Guid, decimal>();
            var universeShared = new Dictionary<Guid, decimal>();

            foreach (var universe in structure.Universes)
            {
                var required = structure.Projects
                    .Where(project => project.UniverseId == universe.Id)
                    .Sum(project => projectExplicit[project.Id] ?? 0);
                var explicitValue = universeExplicit[universe.Id];
                if (explicitValue.HasValue && explicitValue.Value < required)
                {
                    throw new ValidationException($"O esforço de {universe.Name} não pode ser menor que as reservas dos Projetos.");
                }

                universeEffective[universe.Id] = explicitValue ?? required;
                universeShared[universe.Id] = universeEffective[universe.Id] - required;
            }

            var householdExplicit = Lookup(EffortScopeType.Household, null);
            var householdRequired = universeEffective.Values.Sum();
            if (householdExplicit.HasValue && householdExplicit.Value < householdRequired)
            {
                throw new ValidationException("O esforço da Casa não pode ser menor que as reservas dos Universos.");
            }

            var householdEffective = householdExplicit ?? householdRequired;
            days[weekday] = new DayPlan(
                householdExplicit,
                householdEffective,
                householdEffective - householdRequired,
                universeExplicit,
                universeEffective,
                universeShared,
                projectExplicit);
        }

        return new PlanState(days);
    }

    private static Dictionary<string, decimal> BuildBuckets(DayPlan plan)
    {
        var buckets = new Dictionary<string, decimal>
        {
            ["household"] = plan.HouseholdShared
        };

        foreach (var universe in plan.UniverseShared)
        {
            buckets[$"universe:{universe.Key}"] = universe.Value;
        }

        foreach (var project in plan.ProjectExplicit.Where(item => item.Value.HasValue))
        {
            buckets[$"project:{project.Key}"] = project.Value!.Value;
        }

        return buckets;
    }

    private static string ResolveBucket(Guid projectId, Guid universeId, DayPlan plan)
    {
        if (plan.ProjectExplicit[projectId].HasValue)
        {
            return $"project:{projectId}";
        }

        if (plan.UniverseEffective[universeId] > 0)
        {
            return $"universe:{universeId}";
        }

        return "household";
    }

    private static Candidate CreateCandidate(Activity activity, Guid memberId, DateOnly date, TimeSpan offset)
    {
        var priorityScore = activity.Priority switch
        {
            Priority.Baixa => 100,
            Priority.Media => 200,
            Priority.Alta => 300,
            Priority.Urgente => 400,
            _ => 0
        };
        var dueDateScore = activity.DueDate switch
        {
            null => 0,
            var due when due < date => 200,
            var due when due == date => 180,
            var due when due <= date.AddDays(3) => 120,
            var due when due <= date.AddDays(7) => 60,
            _ => 0
        };
        var createdDate = DateOnly.FromDateTime(activity.CreatedAt.ToOffset(offset).DateTime);
        var ageScore = Math.Clamp(date.DayNumber - createdDate.DayNumber, 0, 120);
        var assignmentScore = activity.ResponsibleMemberId == memberId ? 25 : 0;
        return new Candidate(activity, priorityScore, dueDateScore, ageScore, assignmentScore);
    }

    private async Task<HouseholdMember> ResolveCurrentMemberAsync(CancellationToken cancellationToken)
    {
        EnsureWritable();
        var householdId = userContext.HouseholdId;
        if (!householdId.HasValue)
        {
            var households = await db.HouseholdMembers
                .AsNoTracking()
                .Where(item => item.UserId == userContext.UserId && item.IsActive)
                .Select(item => item.HouseholdId)
                .ToArrayAsync(cancellationToken);
            householdId = households.Length == 1
                ? households[0]
                : throw new ValidationException("Selecione uma casa para configurar seu esforço.");
        }

        return await db.HouseholdMembers
            .FirstOrDefaultAsync(item =>
                item.HouseholdId == householdId &&
                item.UserId == userContext.UserId &&
                item.IsActive,
                cancellationToken)
            ?? throw new ForbiddenException("Você não tem acesso a esta casa.");
    }

    private void EnsureWritable()
    {
        if (userContext.SystemRole == SystemRole.SuperAdmin)
        {
            throw new ForbiddenException(SuperAdminReadOnlyMessage);
        }
    }

    private static Guid? ScopeId(MemberEffortAllocation item) => item.ScopeType switch
    {
        EffortScopeType.Universe => item.UniverseId,
        EffortScopeType.Project => item.ProjectId,
        _ => null
    };

    private static EffortWeekday ToEffortWeekday(DayOfWeek dayOfWeek) => dayOfWeek switch
    {
        DayOfWeek.Monday => EffortWeekday.Monday,
        DayOfWeek.Tuesday => EffortWeekday.Tuesday,
        DayOfWeek.Wednesday => EffortWeekday.Wednesday,
        DayOfWeek.Thursday => EffortWeekday.Thursday,
        DayOfWeek.Friday => EffortWeekday.Friday,
        DayOfWeek.Saturday => EffortWeekday.Saturday,
        _ => EffortWeekday.Sunday
    };

    private sealed record UniverseScope(Guid Id, string Name);
    private sealed record ProjectScope(Guid Id, Guid UniverseId, string Name);
    private sealed record Structure(IReadOnlyCollection<UniverseScope> Universes, IReadOnlyCollection<ProjectScope> Projects);
    private sealed record PlanState(IReadOnlyDictionary<EffortWeekday, DayPlan> Days);
    private sealed record DayPlan(
        decimal? HouseholdExplicit,
        decimal HouseholdEffective,
        decimal HouseholdShared,
        IReadOnlyDictionary<Guid, decimal?> UniverseExplicit,
        IReadOnlyDictionary<Guid, decimal> UniverseEffective,
        IReadOnlyDictionary<Guid, decimal> UniverseShared,
        IReadOnlyDictionary<Guid, decimal?> ProjectExplicit);
    private sealed record Candidate(Activity Activity, int PriorityScore, int DueDateScore, int AgeScore, int AssignmentScore)
    {
        public int Score => PriorityScore + DueDateScore + AgeScore + AssignmentScore;
    }
}
