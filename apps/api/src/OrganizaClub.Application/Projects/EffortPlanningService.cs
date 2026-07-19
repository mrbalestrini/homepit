using OrganizaClub.Application.Common;
using OrganizaClub.Domain.Spaces;
using OrganizaClub.Domain.Projects;
using Microsoft.EntityFrameworkCore;

namespace OrganizaClub.Application.Projects;

public sealed class EffortPlanningService(IOrganizaClubDbContext db, IUserContext userContext)
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

        var coreIds = inputs
            .Where(item => item.ScopeType == EffortScopeType.Core)
            .Select(item => item.ScopeId ?? Guid.Empty)
            .ToArray();
        var projectIds = inputs
            .Where(item => item.ScopeType == EffortScopeType.Project)
            .Select(item => item.ScopeId ?? Guid.Empty)
            .ToArray();

        if (inputs.Any(item =>
                (item.ScopeType == EffortScopeType.Space && item.ScopeId.HasValue) ||
                (item.ScopeType != EffortScopeType.Space && !item.ScopeId.HasValue)))
        {
            throw new ValidationException("O escopo do esforço é inválido.");
        }

        var validCoreIds = await db.Cores
            .Where(item => item.SpaceId == member.SpaceId && coreIds.Contains(item.Id))
            .Select(item => item.Id)
            .ToArrayAsync(cancellationToken);
        var validProjectIds = await db.Projects
            .Where(item => item.SpaceId == member.SpaceId && projectIds.Contains(item.Id))
            .Select(item => item.Id)
            .ToArrayAsync(cancellationToken);

        if (validCoreIds.Length != coreIds.Distinct().Count() || validProjectIds.Length != projectIds.Distinct().Count())
        {
            throw new ValidationException("Escolha apenas Núcleos e Projetos deste Espaço.");
        }

        var replacement = inputs.Select(item => new MemberEffortAllocation
        {
            SpaceId = member.SpaceId,
            SpaceMemberId = member.Id,
            ScopeType = item.ScopeType,
            CoreId = item.ScopeType == EffortScopeType.Core ? item.ScopeId : null,
            ProjectId = item.ScopeType == EffortScopeType.Project ? item.ScopeId : null,
            Weekday = item.Weekday,
            Points = item.Points
        }).ToArray();

        var structure = await LoadStructureAsync(member.SpaceId, cancellationToken);
        BuildPlanState(structure, replacement);

        var current = await db.MemberEffortAllocations
            .Where(item => item.SpaceMemberId == member.Id)
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
        var structure = await LoadStructureAsync(member.SpaceId, cancellationToken);
        var allocations = await db.MemberEffortAllocations
            .AsNoTracking()
            .Where(item => item.SpaceMemberId == member.Id)
            .ToArrayAsync(cancellationToken);
        var plan = BuildPlanState(structure, allocations);
        var weekday = ToEffortWeekday(date.DayOfWeek);
        var dayPlan = plan.Days[weekday];
        var activities = await db.Activities
            .AsNoTracking()
            .Include(activity => activity.Project)
            .Where(activity =>
                activity.SpaceId == member.SpaceId &&
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

            var bucket = ResolveBucket(candidate.Activity.ProjectId, candidate.Activity.Project!.CoreId, dayPlan);
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
            dayPlan.SpaceEffective,
            scheduled.Sum(item => item.Activity.Size!.Value),
            items);
    }

    private async Task<EffortPlanDto> GetPlanForMemberAsync(SpaceMember member, CancellationToken cancellationToken)
    {
        var structure = await LoadStructureAsync(member.SpaceId, cancellationToken);
        var allocations = await db.MemberEffortAllocations
            .AsNoTracking()
            .Where(item => item.SpaceMemberId == member.Id)
            .ToArrayAsync(cancellationToken);
        var plan = BuildPlanState(structure, allocations);
        var weekdays = Enum.GetValues<EffortWeekday>();
        var scopes = new List<EffortPlanScopeDto>
        {
            new(
                EffortScopeType.Space,
                null,
                null,
                "Espaço",
                weekdays.Select(day => new EffortPlanDayDto(
                    day,
                    plan.Days[day].SpaceExplicit,
                    plan.Days[day].SpaceEffective,
                    plan.Days[day].SpaceShared)).ToArray())
        };

        foreach (var core in structure.Cores.OrderBy(item => item.Name))
        {
            scopes.Add(new EffortPlanScopeDto(
                EffortScopeType.Core,
                core.Id,
                null,
                core.Name,
                weekdays.Select(day => new EffortPlanDayDto(
                    day,
                    plan.Days[day].CoreExplicit[core.Id],
                    plan.Days[day].CoreEffective[core.Id],
                    plan.Days[day].CoreShared[core.Id])).ToArray()));

            foreach (var project in structure.Projects.Where(item => item.CoreId == core.Id).OrderBy(item => item.Name))
            {
                scopes.Add(new EffortPlanScopeDto(
                    EffortScopeType.Project,
                    project.Id,
                    core.Id,
                    project.Name,
                    weekdays.Select(day => new EffortPlanDayDto(
                        day,
                        plan.Days[day].ProjectExplicit[project.Id],
                        plan.Days[day].ProjectExplicit[project.Id] ?? 0,
                        0)).ToArray()));
            }
        }

        return new EffortPlanDto(member.SpaceId, member.Id, scopes);
    }

    private async Task<Structure> LoadStructureAsync(Guid spaceId, CancellationToken cancellationToken)
    {
        var cores = await db.Cores
            .AsNoTracking()
            .Where(item => item.SpaceId == spaceId)
            .Select(item => new CoreScope(item.Id, item.Name))
            .ToArrayAsync(cancellationToken);
        var projects = await db.Projects
            .AsNoTracking()
            .Where(item => item.SpaceId == spaceId)
            .Select(item => new ProjectScope(item.Id, item.CoreId, item.Name))
            .ToArrayAsync(cancellationToken);
        return new Structure(cores, projects);
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
            var coreExplicit = structure.Cores.ToDictionary(core => core.Id, core => Lookup(EffortScopeType.Core, core.Id));
            var coreEffective = new Dictionary<Guid, decimal>();
            var coreShared = new Dictionary<Guid, decimal>();

            foreach (var core in structure.Cores)
            {
                var required = structure.Projects
                    .Where(project => project.CoreId == core.Id)
                    .Sum(project => projectExplicit[project.Id] ?? 0);
                var explicitValue = coreExplicit[core.Id];
                if (explicitValue.HasValue && explicitValue.Value < required)
                {
                    throw new ValidationException($"O esforço de {core.Name} não pode ser menor que as reservas dos Projetos.");
                }

                coreEffective[core.Id] = explicitValue ?? required;
                coreShared[core.Id] = coreEffective[core.Id] - required;
            }

            var spaceExplicit = Lookup(EffortScopeType.Space, null);
            var spaceRequired = coreEffective.Values.Sum();
            if (spaceExplicit.HasValue && spaceExplicit.Value < spaceRequired)
            {
                throw new ValidationException("O esforço do Espaço não pode ser menor que as reservas dos Núcleos.");
            }

            var spaceEffective = spaceExplicit ?? spaceRequired;
            days[weekday] = new DayPlan(
                spaceExplicit,
                spaceEffective,
                spaceEffective - spaceRequired,
                coreExplicit,
                coreEffective,
                coreShared,
                projectExplicit);
        }

        return new PlanState(days);
    }

    private static Dictionary<string, decimal> BuildBuckets(DayPlan plan)
    {
        var buckets = new Dictionary<string, decimal>
        {
            ["space"] = plan.SpaceShared
        };

        foreach (var core in plan.CoreShared)
        {
            buckets[$"core:{core.Key}"] = core.Value;
        }

        foreach (var project in plan.ProjectExplicit.Where(item => item.Value.HasValue))
        {
            buckets[$"project:{project.Key}"] = project.Value!.Value;
        }

        return buckets;
    }

    private static string ResolveBucket(Guid projectId, Guid coreId, DayPlan plan)
    {
        if (plan.ProjectExplicit[projectId].HasValue)
        {
            return $"project:{projectId}";
        }

        if (plan.CoreEffective[coreId] > 0)
        {
            return $"core:{coreId}";
        }

        return "space";
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

    private async Task<SpaceMember> ResolveCurrentMemberAsync(CancellationToken cancellationToken)
    {
        EnsureWritable();
        var spaceId = userContext.SpaceId;
        if (!spaceId.HasValue)
        {
            var spaces = await db.SpaceMembers
                .AsNoTracking()
                .Where(item => item.UserId == userContext.UserId && item.IsActive)
                .Select(item => item.SpaceId)
                .ToArrayAsync(cancellationToken);
            spaceId = spaces.Length == 1
                ? spaces[0]
                : throw new ValidationException("Selecione um espaço para configurar seu esforço.");
        }

        return await db.SpaceMembers
            .FirstOrDefaultAsync(item =>
                item.SpaceId == spaceId &&
                item.UserId == userContext.UserId &&
                item.IsActive,
                cancellationToken)
            ?? throw new ForbiddenException("Você não tem acesso a este espaço.");
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
        EffortScopeType.Core => item.CoreId,
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

    private sealed record CoreScope(Guid Id, string Name);
    private sealed record ProjectScope(Guid Id, Guid CoreId, string Name);
    private sealed record Structure(IReadOnlyCollection<CoreScope> Cores, IReadOnlyCollection<ProjectScope> Projects);
    private sealed record PlanState(IReadOnlyDictionary<EffortWeekday, DayPlan> Days);
    private sealed record DayPlan(
        decimal? SpaceExplicit,
        decimal SpaceEffective,
        decimal SpaceShared,
        IReadOnlyDictionary<Guid, decimal?> CoreExplicit,
        IReadOnlyDictionary<Guid, decimal> CoreEffective,
        IReadOnlyDictionary<Guid, decimal> CoreShared,
        IReadOnlyDictionary<Guid, decimal?> ProjectExplicit);
    private sealed record Candidate(Activity Activity, int PriorityScore, int DueDateScore, int AgeScore, int AssignmentScore)
    {
        public int Score => PriorityScore + DueDateScore + AgeScore + AssignmentScore;
    }
}
