using HomePit.Application.Common;
using HomePit.Domain.Households;
using HomePit.Domain.Projects;
using Microsoft.EntityFrameworkCore;

namespace HomePit.Application.Projects;

public sealed class ProjectService(IHomePitDbContext db, IUserContext userContext)
{
    public async Task<IReadOnlyCollection<UniverseDto>> ListUniversesAsync(CancellationToken cancellationToken)
    {
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var isManager = IsContentManager(currentMember);

        return await db.Universes
            .AsNoTracking()
            .Where(universe => universe.HouseholdId == currentMember.HouseholdId)
            .OrderBy(universe => universe.Name)
            .Select(universe => new UniverseDto(
                universe.Id,
                universe.Name,
                universe.ImageUrl,
                universe.CreatedByMemberId,
                universe.Projects.Count,
                isManager || universe.CreatedByMemberId == currentMember.Id,
                isManager || universe.CreatedByMemberId == currentMember.Id))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<UniverseDto> CreateUniverseAsync(CreateUniverseRequest request, CancellationToken cancellationToken)
    {
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var name = RequiredText(request.Name, "Informe o nome do universo.");

        var universe = new Universe
        {
            HouseholdId = currentMember.HouseholdId,
            CreatedByMemberId = currentMember.Id,
            Name = name,
            ImageUrl = NormalizeImageUrl(request.ImageUrl)
        };
        db.Universes.Add(universe);
        await db.SaveChangesAsync(cancellationToken);

        return ToUniverseDto(universe, 0, currentMember);
    }

    public async Task<UniverseDto> UpdateUniverseAsync(
        Guid universeId,
        UpdateUniverseRequest request,
        CancellationToken cancellationToken)
    {
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var universe = await db.Universes
            .FirstOrDefaultAsync(item => item.Id == universeId && item.HouseholdId == currentMember.HouseholdId, cancellationToken)
            ?? throw new NotFoundException("Universo não encontrado.");

        EnsureCanManageEntity(currentMember, universe.CreatedByMemberId, "Você não pode editar um universo criado por outra pessoa.");

        universe.Name = RequiredText(request.Name, "Informe o nome do universo.");
        universe.ImageUrl = NormalizeImageUrl(request.ImageUrl);
        await db.SaveChangesAsync(cancellationToken);

        var projectCount = await db.Projects
            .CountAsync(project => project.HouseholdId == currentMember.HouseholdId && project.UniverseId == universe.Id, cancellationToken);

        return ToUniverseDto(universe, projectCount, currentMember);
    }

    public async Task DeleteUniverseAsync(Guid universeId, CancellationToken cancellationToken)
    {
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var universe = await db.Universes
            .FirstOrDefaultAsync(item => item.Id == universeId && item.HouseholdId == currentMember.HouseholdId, cancellationToken)
            ?? throw new NotFoundException("Universo não encontrado.");

        EnsureCanManageEntity(currentMember, universe.CreatedByMemberId, "Você não pode excluir um universo criado por outra pessoa.");

        var prompts = await db.Prompts
            .Where(prompt => prompt.HouseholdId == currentMember.HouseholdId && prompt.UniverseId == universe.Id)
            .ToArrayAsync(cancellationToken);

        foreach (var prompt in prompts)
        {
            prompt.UniverseId = null;
        }

        db.Universes.Remove(universe);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<ProjectDto>> ListProjectsAsync(Guid? universeId, CancellationToken cancellationToken)
    {
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var isManager = IsContentManager(currentMember);

        return await db.Projects
            .AsNoTracking()
            .Where(project => project.HouseholdId == currentMember.HouseholdId)
            .Where(project => universeId == null || project.UniverseId == universeId)
            .OrderBy(project => project.Name)
            .Select(project => new ProjectDto(
                project.Id,
                project.UniverseId,
                project.Universe!.Name,
                project.Universe!.ImageUrl,
                project.Name,
                project.CreatedByMemberId,
                project.Activities.Count,
                isManager || project.CreatedByMemberId == currentMember.Id,
                isManager || project.CreatedByMemberId == currentMember.Id))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<ProjectDto> CreateProjectAsync(CreateProjectRequest request, CancellationToken cancellationToken)
    {
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var universe = await db.Universes
            .FirstOrDefaultAsync(item => item.Id == request.UniverseId && item.HouseholdId == currentMember.HouseholdId, cancellationToken)
            ?? throw new NotFoundException("Universo não encontrado.");

        var project = new Project
        {
            HouseholdId = currentMember.HouseholdId,
            UniverseId = universe.Id,
            CreatedByMemberId = currentMember.Id,
            Name = RequiredText(request.Name, "Informe o nome do projeto.")
        };

        db.Projects.Add(project);
        await db.SaveChangesAsync(cancellationToken);

        project.Universe = universe;
        return ToProjectDto(project, 0, currentMember);
    }

    public async Task<ProjectDto> UpdateProjectAsync(
        Guid projectId,
        UpdateProjectRequest request,
        CancellationToken cancellationToken)
    {
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var project = await db.Projects
            .FirstOrDefaultAsync(item => item.Id == projectId && item.HouseholdId == currentMember.HouseholdId, cancellationToken)
            ?? throw new NotFoundException("Projeto não encontrado.");
        var universe = await db.Universes
            .FirstOrDefaultAsync(item => item.Id == request.UniverseId && item.HouseholdId == currentMember.HouseholdId, cancellationToken)
            ?? throw new NotFoundException("Universo não encontrado.");

        EnsureCanManageEntity(currentMember, project.CreatedByMemberId, "Você não pode editar um projeto criado por outra pessoa.");

        project.UniverseId = universe.Id;
        project.Name = RequiredText(request.Name, "Informe o nome do projeto.");
        await db.SaveChangesAsync(cancellationToken);

        var activityCount = await db.Activities
            .CountAsync(activity => activity.HouseholdId == currentMember.HouseholdId && activity.ProjectId == project.Id, cancellationToken);

        project.Universe = universe;
        return ToProjectDto(project, activityCount, currentMember);
    }

    public async Task DeleteProjectAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var project = await db.Projects
            .FirstOrDefaultAsync(item => item.Id == projectId && item.HouseholdId == currentMember.HouseholdId, cancellationToken)
            ?? throw new NotFoundException("Projeto não encontrado.");

        EnsureCanManageEntity(currentMember, project.CreatedByMemberId, "Você não pode excluir um projeto criado por outra pessoa.");

        db.Projects.Remove(project);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<ActivityDto>> ListActivitiesAsync(
        Guid? projectId,
        ActivityStatus? status,
        CancellationToken cancellationToken)
    {
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var activities = await db.Activities
            .AsNoTracking()
            .Include(activity => activity.Project)
                .ThenInclude(project => project!.Universe)
            .Include(activity => activity.ResponsibleMember)
                .ThenInclude(member => member!.User)
            .Include(activity => activity.PendingItems)
            .Include(activity => activity.Comments)
            .Where(activity => activity.HouseholdId == currentMember.HouseholdId)
            .Where(activity => projectId == null || activity.ProjectId == projectId)
            .Where(activity => status == null || activity.Status == status)
            .OrderBy(activity => activity.Status)
            .ThenByDescending(activity => activity.Priority)
            .ThenBy(activity => activity.Title)
            .ToArrayAsync(cancellationToken);

        return activities.Select(activity => ToActivityDto(activity, currentMember)).ToArray();
    }

    public async Task<ActivityDto> CreateActivityAsync(CreateActivityRequest request, CancellationToken cancellationToken)
    {
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var project = await db.Projects
            .Include(item => item.Universe)
            .FirstOrDefaultAsync(item => item.Id == request.ProjectId && item.HouseholdId == currentMember.HouseholdId, cancellationToken)
            ?? throw new NotFoundException("Projeto não encontrado.");

        if (request.ResponsibleMemberId.HasValue)
        {
            await EnsureMemberAsync(currentMember.HouseholdId, request.ResponsibleMemberId.Value, cancellationToken);
        }

        var activity = new Activity
        {
            HouseholdId = currentMember.HouseholdId,
            ProjectId = project.Id,
            ResponsibleMemberId = request.ResponsibleMemberId,
            CreatedByMemberId = currentMember.Id,
            Title = RequiredText(request.Title, "Informe o nome da atividade."),
            Description = NormalizeOptional(request.Description),
            Priority = request.Priority,
            Size = request.Size
        };

        db.Activities.Add(activity);
        await db.SaveChangesAsync(cancellationToken);

        activity.Project = project;
        return ToActivityDto(activity, currentMember);
    }

    public async Task<ActivityDto> UpdateActivityAsync(
        Guid activityId,
        UpdateActivityRequest request,
        CancellationToken cancellationToken)
    {
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var activity = await db.Activities
            .FirstOrDefaultAsync(item => item.Id == activityId && item.HouseholdId == currentMember.HouseholdId, cancellationToken)
            ?? throw new NotFoundException("Atividade não encontrada.");
        var project = await db.Projects
            .FirstOrDefaultAsync(item => item.Id == request.ProjectId && item.HouseholdId == currentMember.HouseholdId, cancellationToken)
            ?? throw new NotFoundException("Projeto não encontrado.");

        EnsureCanManageEntity(currentMember, activity.CreatedByMemberId, "Você não pode editar uma atividade criada por outra pessoa.");

        if (request.ResponsibleMemberId.HasValue)
        {
            await EnsureMemberAsync(currentMember.HouseholdId, request.ResponsibleMemberId.Value, cancellationToken);
        }

        activity.ProjectId = project.Id;
        activity.ResponsibleMemberId = request.ResponsibleMemberId;
        activity.Title = RequiredText(request.Title, "Informe o nome da atividade.");
        activity.Description = NormalizeOptional(request.Description);
        activity.Status = request.Status;
        activity.Priority = request.Priority;
        activity.Size = request.Size;

        await db.SaveChangesAsync(cancellationToken);

        var updated = await FindActivityForOutputAsync(currentMember.HouseholdId, activity.Id, cancellationToken);
        return ToActivityDto(updated, currentMember);
    }

    public async Task<ActivityDto> UpdateActivityStatusAsync(
        Guid activityId,
        UpdateActivityStatusRequest request,
        CancellationToken cancellationToken)
    {
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var activity = await db.Activities
            .Include(item => item.Project)
                .ThenInclude(project => project!.Universe)
            .Include(item => item.ResponsibleMember)
                .ThenInclude(member => member!.User)
            .Include(item => item.PendingItems)
            .Include(item => item.Comments)
            .FirstOrDefaultAsync(item => item.Id == activityId && item.HouseholdId == currentMember.HouseholdId, cancellationToken)
            ?? throw new NotFoundException("Atividade não encontrada.");

        EnsureCanManageEntity(currentMember, activity.CreatedByMemberId, "Você não pode editar uma atividade criada por outra pessoa.");

        activity.Status = request.Status;
        await db.SaveChangesAsync(cancellationToken);
        return ToActivityDto(activity, currentMember);
    }

    public async Task DeleteActivityAsync(Guid activityId, CancellationToken cancellationToken)
    {
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var activity = await db.Activities
            .FirstOrDefaultAsync(item => item.Id == activityId && item.HouseholdId == currentMember.HouseholdId, cancellationToken)
            ?? throw new NotFoundException("Atividade não encontrada.");

        EnsureCanManageEntity(currentMember, activity.CreatedByMemberId, "Você não pode excluir uma atividade criada por outra pessoa.");

        db.Activities.Remove(activity);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<ActivityCommentDto>> ListActivityCommentsAsync(
        Guid activityId,
        CancellationToken cancellationToken)
    {
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        await EnsureActivityAsync(currentMember.HouseholdId, activityId, cancellationToken);

        var comments = await db.ActivityComments
            .AsNoTracking()
            .Include(comment => comment.AuthorMember)
                .ThenInclude(member => member!.User)
            .Where(comment => comment.HouseholdId == currentMember.HouseholdId && comment.ActivityId == activityId)
            .OrderBy(comment => comment.CreatedAt)
            .ToArrayAsync(cancellationToken);

        return comments.Select(comment => ToCommentDto(comment, currentMember)).ToArray();
    }

    public async Task<ActivityCommentDto> CreateActivityCommentAsync(
        Guid activityId,
        CreateActivityCommentRequest request,
        CancellationToken cancellationToken)
    {
        var author = await ResolveCurrentMemberAsync(cancellationToken);
        await EnsureActivityAsync(author.HouseholdId, activityId, cancellationToken);

        var comment = new ActivityComment
        {
            HouseholdId = author.HouseholdId,
            ActivityId = activityId,
            AuthorMemberId = author.Id,
            Body = RequiredText(request.Body, "Escreva um comentário.")
        };

        db.ActivityComments.Add(comment);
        await db.SaveChangesAsync(cancellationToken);

        comment.AuthorMember = author;
        return ToCommentDto(comment, author);
    }

    public async Task<ActivityCommentDto> UpdateActivityCommentAsync(
        Guid activityId,
        Guid commentId,
        UpdateActivityCommentRequest request,
        CancellationToken cancellationToken)
    {
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var comment = await db.ActivityComments
            .Include(item => item.AuthorMember)
                .ThenInclude(member => member!.User)
            .FirstOrDefaultAsync(item =>
                item.Id == commentId &&
                item.ActivityId == activityId &&
                item.HouseholdId == currentMember.HouseholdId,
                cancellationToken)
            ?? throw new NotFoundException("Comentário não encontrado.");

        if (comment.AuthorMemberId != currentMember.Id)
        {
            throw new ForbiddenException("Você só pode editar os seus próprios comentários.");
        }

        comment.Body = RequiredText(request.Body, "Escreva um comentário.");
        await db.SaveChangesAsync(cancellationToken);

        return ToCommentDto(comment, currentMember);
    }

    public async Task DeleteActivityCommentAsync(
        Guid activityId,
        Guid commentId,
        CancellationToken cancellationToken)
    {
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var comment = await db.ActivityComments
            .FirstOrDefaultAsync(item =>
                item.Id == commentId &&
                item.ActivityId == activityId &&
                item.HouseholdId == currentMember.HouseholdId,
                cancellationToken)
            ?? throw new NotFoundException("Comentário não encontrado.");

        if (!CanDeleteComment(currentMember, comment.AuthorMemberId))
        {
            throw new ForbiddenException("Você só pode excluir comentários próprios.");
        }

        db.ActivityComments.Remove(comment);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<PendingItemDto>> ListPendingItemsAsync(Guid activityId, CancellationToken cancellationToken)
    {
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        await EnsureActivityAsync(currentMember.HouseholdId, activityId, cancellationToken);

        return await db.PendingItems
            .AsNoTracking()
            .Where(item => item.HouseholdId == currentMember.HouseholdId && item.ActivityId == activityId)
            .OrderBy(item => item.CompletedAt != null)
            .ThenBy(item => item.DueDate)
            .ThenByDescending(item => item.Priority)
            .Select(item => new PendingItemDto(
                item.Id,
                item.ActivityId,
                item.Title,
                item.Description,
                item.Priority,
                item.DueDate,
                item.SnoozeDays,
                item.CompletedAt))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<PendingItemDto> CreatePendingItemAsync(
        Guid activityId,
        CreatePendingItemRequest request,
        CancellationToken cancellationToken)
    {
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var activity = await db.Activities
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == activityId && item.HouseholdId == currentMember.HouseholdId, cancellationToken)
            ?? throw new NotFoundException("Atividade não encontrada.");

        EnsureCanManageEntity(currentMember, activity.CreatedByMemberId, "Você não pode alterar uma atividade criada por outra pessoa.");

        var pendingItem = new PendingItem
        {
            HouseholdId = currentMember.HouseholdId,
            ActivityId = activityId,
            Title = RequiredText(request.Title, "Informe o nome da pendência."),
            Description = NormalizeOptional(request.Description),
            Priority = request.Priority,
            DueDate = request.DueDate,
            SnoozeDays = request.SnoozeDays
        };

        db.PendingItems.Add(pendingItem);
        await db.SaveChangesAsync(cancellationToken);

        return new PendingItemDto(
            pendingItem.Id,
            pendingItem.ActivityId,
            pendingItem.Title,
            pendingItem.Description,
            pendingItem.Priority,
            pendingItem.DueDate,
            pendingItem.SnoozeDays,
            pendingItem.CompletedAt);
    }

    private async Task<Guid> ResolveHouseholdIdAsync(CancellationToken cancellationToken)
    {
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

    private async Task EnsureMemberAsync(Guid householdId, Guid memberId, CancellationToken cancellationToken)
    {
        var exists = await db.HouseholdMembers
            .AnyAsync(member => member.Id == memberId && member.HouseholdId == householdId && member.IsActive, cancellationToken);

        if (!exists)
        {
            throw new ValidationException("Responsável inválido para esta casa.");
        }
    }

    private async Task<Activity> FindActivityForOutputAsync(
        Guid householdId,
        Guid activityId,
        CancellationToken cancellationToken)
    {
        return await db.Activities
            .AsNoTracking()
            .Include(activity => activity.Project)
                .ThenInclude(project => project!.Universe)
            .Include(activity => activity.ResponsibleMember)
                .ThenInclude(member => member!.User)
            .Include(activity => activity.PendingItems)
            .Include(activity => activity.Comments)
            .FirstOrDefaultAsync(activity => activity.Id == activityId && activity.HouseholdId == householdId, cancellationToken)
            ?? throw new NotFoundException("Atividade não encontrada.");
    }

    private async Task<HouseholdMember> ResolveCurrentMemberAsync(Guid householdId, CancellationToken cancellationToken)
    {
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

    private async Task EnsureActivityAsync(Guid householdId, Guid activityId, CancellationToken cancellationToken)
    {
        var exists = await db.Activities
            .AnyAsync(activity => activity.Id == activityId && activity.HouseholdId == householdId, cancellationToken);

        if (!exists)
        {
            throw new NotFoundException("Atividade não encontrada.");
        }
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

    private static string? NormalizeImageUrl(string? value)
    {
        var normalized = NormalizeOptional(value);
        if (normalized is null)
        {
            return null;
        }

        if (normalized.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
        {
            return normalized;
        }

        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ValidationException("Informe uma URL de imagem válida para o universo.");
        }

        return normalized;
    }

    private static UniverseDto ToUniverseDto(Universe universe, int projectCount, HouseholdMember currentMember)
    {
        var canManage = CanManageEntity(currentMember, universe.CreatedByMemberId);
        return new UniverseDto(
            universe.Id,
            universe.Name,
            universe.ImageUrl,
            universe.CreatedByMemberId,
            projectCount,
            canManage,
            canManage);
    }

    private static ProjectDto ToProjectDto(Project project, int activityCount, HouseholdMember currentMember)
    {
        var canManage = CanManageEntity(currentMember, project.CreatedByMemberId);
        return new ProjectDto(
            project.Id,
            project.UniverseId,
            project.Universe?.Name ?? string.Empty,
            project.Universe?.ImageUrl,
            project.Name,
            project.CreatedByMemberId,
            activityCount,
            canManage,
            canManage);
    }

    private static ActivityDto ToActivityDto(Activity activity, HouseholdMember currentMember)
    {
        var canManage = CanManageEntity(currentMember, activity.CreatedByMemberId);
        return new ActivityDto(
            activity.Id,
            activity.ProjectId,
            activity.Project!.Name,
            activity.Project.UniverseId,
            activity.Project.Universe!.Name,
            activity.Project.Universe.ImageUrl,
            activity.CreatedByMemberId,
            activity.Title,
            activity.Description,
            activity.Status,
            activity.Priority,
            activity.Size,
            activity.ResponsibleMemberId,
            activity.ResponsibleMember?.User?.DisplayName,
            activity.PendingItems.Count,
            activity.Comments.Count,
            canManage,
            canManage);
    }

    private static ActivityCommentDto ToCommentDto(ActivityComment comment, HouseholdMember currentMember)
    {
        return new ActivityCommentDto(
            comment.Id,
            comment.ActivityId,
            comment.AuthorMemberId,
            comment.AuthorMember?.User?.DisplayName ?? string.Empty,
            comment.Body,
            comment.CreatedAt,
            comment.UpdatedAt > comment.CreatedAt,
            comment.AuthorMemberId == currentMember.Id,
            CanDeleteComment(currentMember, comment.AuthorMemberId));
    }

    private static bool IsContentManager(HouseholdMember member)
    {
        return member.Role is HouseholdRole.Owner or HouseholdRole.Admin;
    }

    private static bool CanManageEntity(HouseholdMember member, Guid? createdByMemberId)
    {
        return IsContentManager(member) || createdByMemberId == member.Id;
    }

    private static bool CanDeleteComment(HouseholdMember member, Guid authorMemberId)
    {
        return IsContentManager(member) || authorMemberId == member.Id;
    }

    private static void EnsureCanManageEntity(
        HouseholdMember member,
        Guid? createdByMemberId,
        string message)
    {
        if (!CanManageEntity(member, createdByMemberId))
        {
            throw new ForbiddenException(message);
        }
    }
}
