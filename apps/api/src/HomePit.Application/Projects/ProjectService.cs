using HomePit.Application.Common;
using HomePit.Application.Images;
using HomePit.Application.Plans;
using HomePit.Application.Storage;
using HomePit.Domain.Households;
using HomePit.Domain.Plans;
using HomePit.Domain.Projects;
using Microsoft.EntityFrameworkCore;

namespace HomePit.Application.Projects;

public sealed class ProjectService(
    IHomePitDbContext db,
    IUserContext userContext,
    IObjectStorage objectStorage,
    IImageUploadProcessor imageUploadProcessor,
    TimeProvider timeProvider,
    CommercialPlanService commercialPlanService,
    ManagedImageQuotaService managedImageQuotaService)
{
    private const string SuperAdminReadOnlyMessage = "O superadmin possui acesso somente leitura nesta etapa.";

    private static readonly ImageUploadValidationMessages UniverseImageMessages = new(
        "Envie uma imagem com conteúdo para o universo.",
        "A imagem do universo deve ter no máximo 5 MB.",
        "A imagem do universo deve estar em JPG, PNG, WEBP, GIF ou BMP.",
        "Envie um arquivo de imagem válido para o universo.",
        "Imagens animadas não são aceitas no universo.");

    private static readonly ImageUploadValidationMessages ActivityImageMessages = new(
        "Envie uma imagem com conteúdo para a atividade.",
        "A imagem da atividade deve ter no máximo 5 MB.",
        "A imagem da atividade deve estar em JPG, PNG, WEBP, GIF ou BMP.",
        "Envie um arquivo de imagem válido para a atividade.",
        "Imagens animadas não são aceitas na atividade.");

    public async Task<IReadOnlyCollection<UniverseDto>> ListUniversesAsync(CancellationToken cancellationToken)
    {
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var universes = await db.Universes
            .AsNoTracking()
            .Where(universe => universe.HouseholdId == currentMember.HouseholdId)
            .Select(universe => new
            {
                universe.Id,
                universe.Name,
                universe.ImageUrl,
                HasImage = universe.ImageObjectKey != null,
                universe.ImageUpdatedAt,
                universe.CreatedByMemberId,
                ProjectCount = universe.Projects.Count,
                CreatedByUserId = universe.CreatedByMember != null ? universe.CreatedByMember.UserId : (Guid?)null,
                universe.CreatedAt
            })
            .ToArrayAsync(cancellationToken);

        var outOfPlanUniverseIds = await ResolveOutOfPlanUniverseIdsAsync(
            universes.Select(universe => new CreatorScopedEntitySnapshot(
                universe.Id,
                universe.CreatedByUserId,
                universe.CreatedAt)),
            cancellationToken);

        return universes
            .OrderBy(universe => universe.Name)
            .Select(universe =>
            {
                var canManage = CanManageEntity(currentMember, universe.CreatedByMemberId);

                return new UniverseDto(
                    universe.Id,
                    universe.Name,
                    universe.ImageUrl,
                    universe.HasImage,
                    universe.ImageUpdatedAt,
                    universe.CreatedByMemberId,
                    universe.ProjectCount,
                    outOfPlanUniverseIds.Contains(universe.Id),
                    canManage,
                    canManage);
            })
            .ToArray();
    }

    public async Task<UniverseDto> CreateUniverseAsync(CreateUniverseRequest request, CancellationToken cancellationToken)
    {
        EnsureWritable();
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        await commercialPlanService.EnsureCanCreateUniverseAsync(userContext.UserId, currentMember.HouseholdId, cancellationToken);
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

        return ToUniverseDto(universe, 0, currentMember, isOutOfPlan: false);
    }

    public async Task<UniverseDto> UpdateUniverseAsync(
        Guid universeId,
        UpdateUniverseRequest request,
        CancellationToken cancellationToken)
    {
        EnsureWritable();
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var universe = await db.Universes
            .FirstOrDefaultAsync(item => item.Id == universeId && item.HouseholdId == currentMember.HouseholdId, cancellationToken)
            ?? throw new NotFoundException("Universo não encontrado.");

        EnsureCanManageEntity(currentMember, universe.CreatedByMemberId, "Você não pode editar um universo criado por outra pessoa.");

        var normalizedImageUrl = NormalizeImageUrl(request.ImageUrl);

        universe.Name = RequiredText(request.Name, "Informe o nome do universo.");
        universe.ImageUrl = normalizedImageUrl;

        if (normalizedImageUrl is not null && !string.IsNullOrWhiteSpace(universe.ImageObjectKey))
        {
            await objectStorage.DeleteAsync(universe.ImageObjectKey, cancellationToken);
            universe.ImageObjectKey = null;
            universe.ImageContentType = null;
            universe.ImageUpdatedAt = null;
        }

        await db.SaveChangesAsync(cancellationToken);

        var projectCount = await db.Projects
            .CountAsync(project => project.HouseholdId == currentMember.HouseholdId && project.UniverseId == universe.Id, cancellationToken);

        return ToUniverseDto(
            universe,
            projectCount,
            currentMember,
            await IsUniverseOutOfPlanAsync(universe.Id, cancellationToken));
    }

    public async Task<UniverseDto> UploadUniverseImageAsync(
        Guid universeId,
        Stream content,
        long contentLength,
        string? contentType,
        CancellationToken cancellationToken)
    {
        EnsureWritable();
        var preparedImage = await imageUploadProcessor.PrepareAsync(
            content,
            contentLength,
            contentType,
            ImageUploadPolicies.Common,
            UniverseImageMessages,
            cancellationToken);
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var universe = await db.Universes
            .FirstOrDefaultAsync(item => item.Id == universeId && item.HouseholdId == currentMember.HouseholdId, cancellationToken)
            ?? throw new NotFoundException("Universo não encontrado.");

        EnsureCanManageEntity(currentMember, universe.CreatedByMemberId, "Você não pode editar um universo criado por outra pessoa.");

        var objectKey = ObjectStorageKeys.UniverseImage(universe.Id);
        await using var uploadStream = new MemoryStream(preparedImage.Content, writable: false);
        await objectStorage.PutAsync(
            new ObjectStoragePutRequest(objectKey, uploadStream, preparedImage.ContentLength, preparedImage.ContentType),
            cancellationToken);

        universe.ImageUrl = null;
        universe.ImageObjectKey = objectKey;
        universe.ImageContentType = preparedImage.ContentType;
        universe.ImageUpdatedAt = timeProvider.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);

        var projectCount = await db.Projects
            .CountAsync(project => project.HouseholdId == currentMember.HouseholdId && project.UniverseId == universe.Id, cancellationToken);

        return ToUniverseDto(
            universe,
            projectCount,
            currentMember,
            await IsUniverseOutOfPlanAsync(universe.Id, cancellationToken));
    }

    public async Task<StoredObject> GetUniverseImageAsync(Guid universeId, CancellationToken cancellationToken)
    {
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var universe = await db.Universes
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == universeId && item.HouseholdId == currentMember.HouseholdId, cancellationToken)
            ?? throw new NotFoundException("Universo não encontrado.");

        if (string.IsNullOrWhiteSpace(universe.ImageObjectKey))
        {
            throw new NotFoundException("Imagem do universo não encontrada.");
        }

        return await objectStorage.GetAsync(universe.ImageObjectKey, cancellationToken);
    }

    public async Task<UniverseDto> DeleteUniverseImageAsync(Guid universeId, CancellationToken cancellationToken)
    {
        EnsureWritable();
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var universe = await db.Universes
            .FirstOrDefaultAsync(item => item.Id == universeId && item.HouseholdId == currentMember.HouseholdId, cancellationToken)
            ?? throw new NotFoundException("Universo não encontrado.");

        EnsureCanManageEntity(currentMember, universe.CreatedByMemberId, "Você não pode editar um universo criado por outra pessoa.");

        if (string.IsNullOrWhiteSpace(universe.ImageObjectKey))
        {
            throw new NotFoundException("Imagem do universo não encontrada.");
        }

        await objectStorage.DeleteAsync(universe.ImageObjectKey, cancellationToken);
        universe.ImageObjectKey = null;
        universe.ImageContentType = null;
        universe.ImageUpdatedAt = null;
        await db.SaveChangesAsync(cancellationToken);

        var projectCount = await db.Projects
            .CountAsync(project => project.HouseholdId == currentMember.HouseholdId && project.UniverseId == universe.Id, cancellationToken);

        return ToUniverseDto(
            universe,
            projectCount,
            currentMember,
            await IsUniverseOutOfPlanAsync(universe.Id, cancellationToken));
    }

    public async Task DeleteUniverseAsync(Guid universeId, CancellationToken cancellationToken)
    {
        EnsureWritable();
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var universe = await db.Universes
            .FirstOrDefaultAsync(item => item.Id == universeId && item.HouseholdId == currentMember.HouseholdId, cancellationToken)
            ?? throw new NotFoundException("Universo não encontrado.");

        EnsureCanManageEntity(currentMember, universe.CreatedByMemberId, "Você não pode excluir um universo criado por outra pessoa.");

        var prompts = await db.Prompts
            .Where(prompt => prompt.HouseholdId == currentMember.HouseholdId && prompt.UniverseId == universe.Id)
            .ToArrayAsync(cancellationToken);

        var activityImageSnapshots = await db.Projects
            .AsNoTracking()
            .Where(project => project.UniverseId == universe.Id)
            .SelectMany(project => project.Activities)
            .Where(activity => !string.IsNullOrWhiteSpace(activity.ImageObjectKey))
            .Select(activity => new { activity.Id, activity.ImageObjectKey })
            .ToArrayAsync(cancellationToken);

        foreach (var prompt in prompts)
        {
            prompt.UniverseId = null;
        }

        await DeleteObjectKeysAsync(activityImageSnapshots.Select(item => item.ImageObjectKey), cancellationToken);
        await managedImageQuotaService.DeleteManagedImagesAsync(
            PlanImageAssetModule.Activity,
            activityImageSnapshots.Select(item => item.Id).ToArray(),
            cancellationToken);
        if (!string.IsNullOrWhiteSpace(universe.ImageObjectKey))
        {
            await objectStorage.DeleteAsync(universe.ImageObjectKey, cancellationToken);
        }

        db.Universes.Remove(universe);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<ProjectDto>> ListProjectsAsync(Guid? universeId, CancellationToken cancellationToken)
    {
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var projects = await db.Projects
            .AsNoTracking()
            .Where(project => project.HouseholdId == currentMember.HouseholdId)
            .Where(project => universeId == null || project.UniverseId == universeId)
            .Select(project => new
            {
                project.Id,
                project.UniverseId,
                UniverseName = project.Universe!.Name,
                UniverseImageUrl = project.Universe!.ImageUrl,
                UniverseHasImage = project.Universe!.ImageObjectKey != null,
                project.Universe!.ImageUpdatedAt,
                project.Name,
                project.CreatedByMemberId,
                ActivityCount = project.Activities.Count(activity => activity.Status != ActivityStatus.Concluido),
                CreatedByUserId = project.CreatedByMember != null ? project.CreatedByMember.UserId : (Guid?)null,
                project.CreatedAt
            })
            .ToArrayAsync(cancellationToken);

        var outOfPlanProjectIds = await ResolveOutOfPlanProjectIdsAsync(
            projects.Select(project => new CreatorScopedEntitySnapshot(
                project.Id,
                project.CreatedByUserId,
                project.CreatedAt)),
            cancellationToken);

        return projects
            .OrderBy(project => project.Name)
            .Select(project =>
            {
                var canManage = CanManageEntity(currentMember, project.CreatedByMemberId);

                return new ProjectDto(
                    project.Id,
                    project.UniverseId,
                    project.UniverseName,
                    project.UniverseImageUrl,
                    project.UniverseHasImage,
                    project.ImageUpdatedAt,
                    project.Name,
                    project.CreatedByMemberId,
                    project.ActivityCount,
                    outOfPlanProjectIds.Contains(project.Id),
                    canManage,
                    canManage);
            })
            .ToArray();
    }

    public async Task<ProjectDto> CreateProjectAsync(CreateProjectRequest request, CancellationToken cancellationToken)
    {
        EnsureWritable();
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        await commercialPlanService.EnsureCanCreateProjectAsync(userContext.UserId, request.UniverseId, cancellationToken);
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
        return ToProjectDto(project, 0, currentMember, isOutOfPlan: false);
    }

    public async Task<ProjectDto> UpdateProjectAsync(
        Guid projectId,
        UpdateProjectRequest request,
        CancellationToken cancellationToken)
    {
        EnsureWritable();
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
            .CountAsync(
                activity =>
                    activity.HouseholdId == currentMember.HouseholdId &&
                    activity.ProjectId == project.Id &&
                    activity.Status != ActivityStatus.Concluido,
                cancellationToken);

        project.Universe = universe;
        return ToProjectDto(
            project,
            activityCount,
            currentMember,
            await IsProjectOutOfPlanAsync(project.Id, cancellationToken));
    }

    public async Task DeleteProjectAsync(Guid projectId, CancellationToken cancellationToken)
    {
        EnsureWritable();
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var project = await db.Projects
            .FirstOrDefaultAsync(item => item.Id == projectId && item.HouseholdId == currentMember.HouseholdId, cancellationToken)
            ?? throw new NotFoundException("Projeto não encontrado.");

        EnsureCanManageEntity(currentMember, project.CreatedByMemberId, "Você não pode excluir um projeto criado por outra pessoa.");

        var activityImageSnapshots = await db.Activities
            .AsNoTracking()
            .Where(activity => activity.ProjectId == project.Id && !string.IsNullOrWhiteSpace(activity.ImageObjectKey))
            .Select(activity => new { activity.Id, activity.ImageObjectKey })
            .ToArrayAsync(cancellationToken);

        await DeleteObjectKeysAsync(activityImageSnapshots.Select(item => item.ImageObjectKey), cancellationToken);
        await managedImageQuotaService.DeleteManagedImagesAsync(
            PlanImageAssetModule.Activity,
            activityImageSnapshots.Select(item => item.Id).ToArray(),
            cancellationToken);
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
        EnsureWritable();
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var project = await db.Projects
            .Include(item => item.Universe)
            .FirstOrDefaultAsync(item => item.Id == request.ProjectId && item.HouseholdId == currentMember.HouseholdId, cancellationToken)
            ?? throw new NotFoundException("Projeto não encontrado.");

        if (request.ResponsibleMemberId.HasValue)
        {
            await EnsureMemberAsync(currentMember.HouseholdId, request.ResponsibleMemberId.Value, cancellationToken);
        }

        ValidateActivitySize(request.Size);

        var activity = new Activity
        {
            HouseholdId = currentMember.HouseholdId,
            ProjectId = project.Id,
            ResponsibleMemberId = request.ResponsibleMemberId,
            CreatedByMemberId = currentMember.Id,
            Title = RequiredText(request.Title, "Informe o nome da atividade."),
            Description = NormalizeOptional(request.Description),
            DueDate = request.DueDate,
            Status = request.Status,
            CompletedAt = request.Status == ActivityStatus.Concluido ? timeProvider.GetUtcNow() : null,
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
        EnsureWritable();
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

        ValidateActivitySize(request.Size);

        activity.ProjectId = project.Id;
        activity.ResponsibleMemberId = request.ResponsibleMemberId;
        activity.Title = RequiredText(request.Title, "Informe o nome da atividade.");
        activity.Description = NormalizeOptional(request.Description);
        activity.DueDate = request.DueDate;
        activity.CompletedAt = ResolveCompletedAt(activity.Status, request.Status, activity.CompletedAt);
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
        EnsureWritable();
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

        activity.CompletedAt = ResolveCompletedAt(activity.Status, request.Status, activity.CompletedAt);
        activity.Status = request.Status;
        await db.SaveChangesAsync(cancellationToken);
        return ToActivityDto(activity, currentMember);
    }

    public async Task DeleteActivityAsync(Guid activityId, CancellationToken cancellationToken)
    {
        EnsureWritable();
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var activity = await db.Activities
            .FirstOrDefaultAsync(item => item.Id == activityId && item.HouseholdId == currentMember.HouseholdId, cancellationToken)
            ?? throw new NotFoundException("Atividade não encontrada.");

        EnsureCanManageEntity(currentMember, activity.CreatedByMemberId, "Você não pode excluir uma atividade criada por outra pessoa.");

        await DeleteObjectKeysAsync([activity.ImageObjectKey], cancellationToken);
        await managedImageQuotaService.DeleteManagedImageAsync(PlanImageAssetModule.Activity, activity.Id, cancellationToken);
        db.Activities.Remove(activity);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<ActivityDto> UploadActivityImageAsync(
        Guid activityId,
        Stream content,
        long contentLength,
        string? contentType,
        CancellationToken cancellationToken)
    {
        EnsureWritable();
        var preparedImage = await imageUploadProcessor.PrepareAsync(
            content,
            contentLength,
            contentType,
            ImageUploadPolicies.Common,
            ActivityImageMessages,
            cancellationToken);
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var activity = await db.Activities
            .FirstOrDefaultAsync(item => item.Id == activityId && item.HouseholdId == currentMember.HouseholdId, cancellationToken)
            ?? throw new NotFoundException("Atividade não encontrada.");

        EnsureCanManageEntity(currentMember, activity.CreatedByMemberId, "Você não pode editar uma atividade criada por outra pessoa.");

        var objectKey = ObjectStorageKeys.ActivityImage(activity.Id);
        await using var uploadStream = new MemoryStream(preparedImage.Content, writable: false);
        await objectStorage.PutAsync(
            new ObjectStoragePutRequest(objectKey, uploadStream, preparedImage.ContentLength, preparedImage.ContentType),
            cancellationToken);

        activity.ImageObjectKey = objectKey;
        activity.ImageContentType = preparedImage.ContentType;
        activity.ImageUpdatedAt = timeProvider.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);
        await managedImageQuotaService.RegisterManagedImageAsync(
            userContext.UserId,
            PlanImageAssetModule.Activity,
            activity.Id,
            objectKey,
            preparedImage.ContentType,
            cancellationToken);

        var updated = await FindActivityForOutputAsync(currentMember.HouseholdId, activity.Id, cancellationToken);
        return ToActivityDto(updated, currentMember);
    }

    public async Task<StoredObject> GetActivityImageAsync(Guid activityId, CancellationToken cancellationToken)
    {
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var activity = await db.Activities
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == activityId && item.HouseholdId == currentMember.HouseholdId, cancellationToken)
            ?? throw new NotFoundException("Atividade não encontrada.");

        if (string.IsNullOrWhiteSpace(activity.ImageObjectKey))
        {
            throw new NotFoundException("Imagem da atividade não encontrada.");
        }

        return await objectStorage.GetAsync(activity.ImageObjectKey, cancellationToken);
    }

    public async Task<ActivityDto> DeleteActivityImageAsync(Guid activityId, CancellationToken cancellationToken)
    {
        EnsureWritable();
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var activity = await db.Activities
            .FirstOrDefaultAsync(item => item.Id == activityId && item.HouseholdId == currentMember.HouseholdId, cancellationToken)
            ?? throw new NotFoundException("Atividade não encontrada.");

        EnsureCanManageEntity(currentMember, activity.CreatedByMemberId, "Você não pode editar uma atividade criada por outra pessoa.");

        if (string.IsNullOrWhiteSpace(activity.ImageObjectKey))
        {
            throw new NotFoundException("Imagem da atividade não encontrada.");
        }

        await DeleteObjectKeysAsync([activity.ImageObjectKey], cancellationToken);
        activity.ImageObjectKey = null;
        activity.ImageContentType = null;
        activity.ImageUpdatedAt = null;
        await db.SaveChangesAsync(cancellationToken);
        await managedImageQuotaService.DeleteManagedImageAsync(PlanImageAssetModule.Activity, activity.Id, cancellationToken);

        var updated = await FindActivityForOutputAsync(currentMember.HouseholdId, activity.Id, cancellationToken);
        return ToActivityDto(updated, currentMember);
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
        EnsureWritable();
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
        EnsureWritable();
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
        EnsureWritable();
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
        EnsureWritable();
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

    private static void ValidateActivitySize(decimal? size)
    {
        if (size < 0)
        {
            throw new ValidationException("O esforço da atividade não pode ser negativo.");
        }
    }

    private DateTimeOffset? ResolveCompletedAt(
        ActivityStatus previousStatus,
        ActivityStatus nextStatus,
        DateTimeOffset? completedAt)
    {
        if (nextStatus != ActivityStatus.Concluido)
        {
            return null;
        }

        return previousStatus == ActivityStatus.Concluido && completedAt.HasValue
            ? completedAt
            : timeProvider.GetUtcNow();
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

    private static UniverseDto ToUniverseDto(
        Universe universe,
        int projectCount,
        HouseholdMember currentMember,
        bool isOutOfPlan)
    {
        var canManage = CanManageEntity(currentMember, universe.CreatedByMemberId);
        return new UniverseDto(
            universe.Id,
            universe.Name,
            universe.ImageUrl,
            !string.IsNullOrWhiteSpace(universe.ImageObjectKey),
            universe.ImageUpdatedAt,
            universe.CreatedByMemberId,
            projectCount,
            isOutOfPlan,
            canManage,
            canManage);
    }

    private static ProjectDto ToProjectDto(
        Project project,
        int activityCount,
        HouseholdMember currentMember,
        bool isOutOfPlan)
    {
        var canManage = CanManageEntity(currentMember, project.CreatedByMemberId);
        return new ProjectDto(
            project.Id,
            project.UniverseId,
            project.Universe?.Name ?? string.Empty,
            project.Universe?.ImageUrl,
            !string.IsNullOrWhiteSpace(project.Universe?.ImageObjectKey),
            project.Universe?.ImageUpdatedAt,
            project.Name,
            project.CreatedByMemberId,
            activityCount,
            isOutOfPlan,
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
            !string.IsNullOrWhiteSpace(activity.Project.Universe.ImageObjectKey),
            activity.Project.Universe.ImageUpdatedAt,
            activity.CreatedByMemberId,
            activity.CreatedAt,
            activity.Title,
            activity.Description,
            !string.IsNullOrWhiteSpace(activity.ImageObjectKey),
            activity.ImageUpdatedAt,
            activity.DueDate,
            activity.CompletedAt,
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

    private async Task DeleteObjectKeysAsync(IEnumerable<string?> objectKeys, CancellationToken cancellationToken)
    {
        foreach (var objectKey in objectKeys
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Distinct(StringComparer.Ordinal))
        {
            await objectStorage.DeleteAsync(objectKey!, cancellationToken);
        }
    }

    private async Task<bool> IsUniverseOutOfPlanAsync(Guid universeId, CancellationToken cancellationToken)
    {
        var outOfPlanUniverseIds = await ResolveOutOfPlanUniverseIdsAsync(
            await db.Universes
                .AsNoTracking()
                .Where(universe => universe.Id == universeId)
                .Select(universe => new CreatorScopedEntitySnapshot(
                    universe.Id,
                    universe.CreatedByMember != null ? universe.CreatedByMember.UserId : (Guid?)null,
                    universe.CreatedAt))
                .ToArrayAsync(cancellationToken),
            cancellationToken);

        return outOfPlanUniverseIds.Contains(universeId);
    }

    private async Task<bool> IsProjectOutOfPlanAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var outOfPlanProjectIds = await ResolveOutOfPlanProjectIdsAsync(
            await db.Projects
                .AsNoTracking()
                .Where(project => project.Id == projectId)
                .Select(project => new CreatorScopedEntitySnapshot(
                    project.Id,
                    project.CreatedByMember != null ? project.CreatedByMember.UserId : (Guid?)null,
                    project.CreatedAt))
                .ToArrayAsync(cancellationToken),
            cancellationToken);

        return outOfPlanProjectIds.Contains(projectId);
    }

    private async Task<HashSet<Guid>> ResolveOutOfPlanUniverseIdsAsync(
        IEnumerable<CreatorScopedEntitySnapshot> visibleUniverses,
        CancellationToken cancellationToken)
    {
        return await ResolveOutOfPlanEntityIdsAsync(
            visibleUniverses,
            userIds => db.Universes
                .AsNoTracking()
                .Where(universe => universe.CreatedByMember != null && userIds.Contains(universe.CreatedByMember.UserId))
                .Select(universe => new CreatorScopedEntitySnapshot(
                    universe.Id,
                    universe.CreatedByMember != null ? universe.CreatedByMember.UserId : (Guid?)null,
                    universe.CreatedAt))
                .ToArrayAsync(cancellationToken),
            plan => plan.MaxUniverses,
            cancellationToken);
    }

    private async Task<HashSet<Guid>> ResolveOutOfPlanProjectIdsAsync(
        IEnumerable<CreatorScopedEntitySnapshot> visibleProjects,
        CancellationToken cancellationToken)
    {
        return await ResolveOutOfPlanEntityIdsAsync(
            visibleProjects,
            userIds => db.Projects
                .AsNoTracking()
                .Where(project => project.CreatedByMember != null && userIds.Contains(project.CreatedByMember.UserId))
                .Select(project => new CreatorScopedEntitySnapshot(
                    project.Id,
                    project.CreatedByMember != null ? project.CreatedByMember.UserId : (Guid?)null,
                    project.CreatedAt))
                .ToArrayAsync(cancellationToken),
            plan => plan.MaxProjects,
            cancellationToken);
    }

    private async Task<HashSet<Guid>> ResolveOutOfPlanEntityIdsAsync(
        IEnumerable<CreatorScopedEntitySnapshot> visibleEntities,
        Func<Guid[], Task<CreatorScopedEntitySnapshot[]>> loadAllEntitiesForCreatorsAsync,
        Func<PlanDefinition, int> resolveLimit,
        CancellationToken cancellationToken)
    {
        var visibleEntityArray = visibleEntities.ToArray();
        var creatorUserIds = visibleEntityArray
            .Where(entity => entity.CreatedByUserId.HasValue)
            .Select(entity => entity.CreatedByUserId!.Value)
            .Distinct()
            .ToArray();

        if (creatorUserIds.Length == 0)
        {
            return [];
        }

        var allEntities = await loadAllEntitiesForCreatorsAsync(creatorUserIds);
        var limitsByUserId = new Dictionary<Guid, int>(creatorUserIds.Length);

        foreach (var creatorUserId in creatorUserIds)
        {
            var plan = await commercialPlanService.ResolveEffectivePlanDefinitionAsync(creatorUserId, cancellationToken);
            limitsByUserId[creatorUserId] = resolveLimit(plan);
        }

        var visibleEntityIds = visibleEntityArray.Select(entity => entity.Id).ToHashSet();
        var outOfPlanIds = new HashSet<Guid>();

        foreach (var creatorGroup in allEntities
            .Where(entity => entity.CreatedByUserId.HasValue)
            .GroupBy(entity => entity.CreatedByUserId!.Value))
        {
            var limit = limitsByUserId.GetValueOrDefault(creatorGroup.Key);

            foreach (var entity in creatorGroup
                .OrderBy(entity => entity.CreatedAt)
                .ThenBy(entity => entity.Id)
                .Skip(limit))
            {
                if (visibleEntityIds.Contains(entity.Id))
                {
                    outOfPlanIds.Add(entity.Id);
                }
            }
        }

        return outOfPlanIds;
    }

    private static ActivityCommentDto ToCommentDto(ActivityComment comment, HouseholdMember currentMember)
    {
        return new ActivityCommentDto(
            comment.Id,
            comment.ActivityId,
            comment.AuthorMemberId,
            comment.AuthorMember?.UserId ?? Guid.Empty,
            comment.AuthorMember?.User?.DisplayName ?? string.Empty,
            !string.IsNullOrWhiteSpace(comment.AuthorMember?.User?.ProfilePhotoObjectKey),
            comment.AuthorMember?.User?.ProfilePhotoUpdatedAt,
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

    private sealed record CreatorScopedEntitySnapshot(
        Guid Id,
        Guid? CreatedByUserId,
        DateTimeOffset CreatedAt);

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
