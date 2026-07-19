using OrganizaClub.Application.Common;
using OrganizaClub.Domain.Common;
using OrganizaClub.Application.Images;
using OrganizaClub.Application.Plans;
using OrganizaClub.Application.Storage;
using OrganizaClub.Domain.Spaces;
using OrganizaClub.Domain.Plans;
using OrganizaClub.Domain.Projects;
using Microsoft.EntityFrameworkCore;

namespace OrganizaClub.Application.Projects;

public sealed class ProjectService(
    IOrganizaClubDbContext db,
    IUserContext userContext,
    IObjectStorage objectStorage,
    IImageUploadProcessor imageUploadProcessor,
    TimeProvider timeProvider,
    CommercialPlanService commercialPlanService,
    ManagedImageQuotaService managedImageQuotaService)
{
    private const string SuperAdminReadOnlyMessage = "O superadmin possui acesso somente leitura nesta etapa.";

    private static readonly ImageUploadValidationMessages CoreImageMessages = new(
        "Envie uma imagem com conteúdo para o núcleo.",
        "A imagem do núcleo deve ter no máximo 5 MB.",
        "A imagem do núcleo deve estar em JPG, PNG, WEBP, GIF ou BMP.",
        "Envie um arquivo de imagem válido para o núcleo.",
        "Imagens animadas não são aceitas no núcleo.");

    private static readonly ImageUploadValidationMessages ActivityImageMessages = new(
        "Envie uma imagem com conteúdo para a atividade.",
        "A imagem da atividade deve ter no máximo 5 MB.",
        "A imagem da atividade deve estar em JPG, PNG, WEBP, GIF ou BMP.",
        "Envie um arquivo de imagem válido para a atividade.",
        "Imagens animadas não são aceitas na atividade.");

    public async Task<IReadOnlyCollection<CoreDto>> ListCoresAsync(CancellationToken cancellationToken)
    {
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var cores = await db.Cores
            .AsNoTracking()
            .Where(core => core.SpaceId == currentMember.SpaceId)
            .Select(core => new
            {
                core.Id,
                core.Name,
                core.ImageUrl,
                HasImage = core.ImageObjectKey != null,
                core.ImageUpdatedAt,
                core.CreatedByMemberId,
                ProjectCount = core.Projects.Count,
                CreatedByUserId = core.CreatedByMember != null ? core.CreatedByMember.UserId : (Guid?)null,
                core.CreatedAt
            })
            .ToArrayAsync(cancellationToken);

        var outOfPlanCoreIds = await ResolveOutOfPlanCoreIdsAsync(
            cores.Select(core => new CreatorScopedEntitySnapshot(
                core.Id,
                core.CreatedByUserId,
                core.CreatedAt)),
            cancellationToken);

        return cores
            .OrderBy(core => core.Name)
            .Select(core =>
            {
                var canManage = CanManageEntity(currentMember, core.CreatedByMemberId);

                return new CoreDto(
                    core.Id,
                    core.Name,
                    core.ImageUrl,
                    core.HasImage,
                    core.ImageUpdatedAt,
                    core.CreatedByMemberId,
                    core.ProjectCount,
                    outOfPlanCoreIds.Contains(core.Id),
                    canManage,
                    canManage);
            })
            .ToArray();
    }

    public async Task<CoreDto> CreateCoreAsync(CreateCoreRequest request, CancellationToken cancellationToken)
    {
        EnsureWritable();
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        await commercialPlanService.EnsureCanCreateCoreAsync(userContext.UserId, currentMember.SpaceId, cancellationToken);
        var name = RequiredText(request.Name, "Informe o nome do núcleo.");

        var core = new Core
        {
            SpaceId = currentMember.SpaceId,
            CreatedByMemberId = currentMember.Id,
            Name = name,
            ImageUrl = NormalizeImageUrl(request.ImageUrl)
        };
        db.Cores.Add(core);
        await db.SaveChangesAsync(cancellationToken);

        return ToCoreDto(core, 0, currentMember, isOutOfPlan: false);
    }

    public async Task<CoreDto> UpdateCoreAsync(
        Guid coreId,
        UpdateCoreRequest request,
        CancellationToken cancellationToken,
        DateTimeOffset? expectedUpdatedAt = null)
    {
        EnsureWritable();
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var core = await db.Cores
            .FirstOrDefaultAsync(item => item.Id == coreId && item.SpaceId == currentMember.SpaceId, cancellationToken)
            ?? throw new NotFoundException("Núcleo não encontrado.");
        ApplyExpectedVersion(core, expectedUpdatedAt);

        EnsureCanManageEntity(currentMember, core.CreatedByMemberId, "Você não pode editar um núcleo criado por outra pessoa.");

        var normalizedImageUrl = NormalizeImageUrl(request.ImageUrl);

        core.Name = RequiredText(request.Name, "Informe o nome do núcleo.");
        core.ImageUrl = normalizedImageUrl;

        if (normalizedImageUrl is not null && !string.IsNullOrWhiteSpace(core.ImageObjectKey))
        {
            await objectStorage.DeleteAsync(core.ImageObjectKey, cancellationToken);
            core.ImageObjectKey = null;
            core.ImageContentType = null;
            core.ImageUpdatedAt = null;
        }

        await db.SaveChangesAsync(cancellationToken);

        var projectCount = await db.Projects
            .CountAsync(project => project.SpaceId == currentMember.SpaceId && project.CoreId == core.Id, cancellationToken);

        return ToCoreDto(
            core,
            projectCount,
            currentMember,
            await IsCoreOutOfPlanAsync(core.Id, cancellationToken));
    }

    public async Task<CoreDto> UploadCoreImageAsync(
        Guid coreId,
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
            CoreImageMessages,
            cancellationToken);
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var core = await db.Cores
            .FirstOrDefaultAsync(item => item.Id == coreId && item.SpaceId == currentMember.SpaceId, cancellationToken)
            ?? throw new NotFoundException("Núcleo não encontrado.");

        EnsureCanManageEntity(currentMember, core.CreatedByMemberId, "Você não pode editar um núcleo criado por outra pessoa.");

        var objectKey = ObjectStorageKeys.CoreImage(core.Id);
        await using var uploadStream = new MemoryStream(preparedImage.Content, writable: false);
        await objectStorage.PutAsync(
            new ObjectStoragePutRequest(objectKey, uploadStream, preparedImage.ContentLength, preparedImage.ContentType),
            cancellationToken);

        core.ImageUrl = null;
        core.ImageObjectKey = objectKey;
        core.ImageContentType = preparedImage.ContentType;
        core.ImageUpdatedAt = timeProvider.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);

        var projectCount = await db.Projects
            .CountAsync(project => project.SpaceId == currentMember.SpaceId && project.CoreId == core.Id, cancellationToken);

        return ToCoreDto(
            core,
            projectCount,
            currentMember,
            await IsCoreOutOfPlanAsync(core.Id, cancellationToken));
    }

    public async Task<StoredObject> GetCoreImageAsync(Guid coreId, CancellationToken cancellationToken)
    {
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var core = await db.Cores
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == coreId && item.SpaceId == currentMember.SpaceId, cancellationToken)
            ?? throw new NotFoundException("Núcleo não encontrado.");

        if (string.IsNullOrWhiteSpace(core.ImageObjectKey))
        {
            throw new NotFoundException("Imagem do núcleo não encontrada.");
        }

        return await objectStorage.GetAsync(core.ImageObjectKey, cancellationToken);
    }

    public async Task<CoreDto> DeleteCoreImageAsync(Guid coreId, CancellationToken cancellationToken)
    {
        EnsureWritable();
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var core = await db.Cores
            .FirstOrDefaultAsync(item => item.Id == coreId && item.SpaceId == currentMember.SpaceId, cancellationToken)
            ?? throw new NotFoundException("Núcleo não encontrado.");

        EnsureCanManageEntity(currentMember, core.CreatedByMemberId, "Você não pode editar um núcleo criado por outra pessoa.");

        if (string.IsNullOrWhiteSpace(core.ImageObjectKey))
        {
            throw new NotFoundException("Imagem do núcleo não encontrada.");
        }

        await objectStorage.DeleteAsync(core.ImageObjectKey, cancellationToken);
        core.ImageObjectKey = null;
        core.ImageContentType = null;
        core.ImageUpdatedAt = null;
        await db.SaveChangesAsync(cancellationToken);

        var projectCount = await db.Projects
            .CountAsync(project => project.SpaceId == currentMember.SpaceId && project.CoreId == core.Id, cancellationToken);

        return ToCoreDto(
            core,
            projectCount,
            currentMember,
            await IsCoreOutOfPlanAsync(core.Id, cancellationToken));
    }

    public async Task DeleteCoreAsync(Guid coreId, CancellationToken cancellationToken, DateTimeOffset? expectedUpdatedAt = null)
    {
        EnsureWritable();
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var core = await db.Cores
            .FirstOrDefaultAsync(item => item.Id == coreId && item.SpaceId == currentMember.SpaceId, cancellationToken)
            ?? throw new NotFoundException("Núcleo não encontrado.");
        ApplyExpectedVersion(core, expectedUpdatedAt);

        EnsureCanManageEntity(currentMember, core.CreatedByMemberId, "Você não pode excluir um núcleo criado por outra pessoa.");

        var prompts = await db.Prompts
            .Where(prompt => prompt.SpaceId == currentMember.SpaceId && prompt.CoreId == core.Id)
            .ToArrayAsync(cancellationToken);

        var activityImageSnapshots = await db.Projects
            .AsNoTracking()
            .Where(project => project.CoreId == core.Id)
            .SelectMany(project => project.Activities)
            .Where(activity => !string.IsNullOrWhiteSpace(activity.ImageObjectKey))
            .Select(activity => new { activity.Id, activity.ImageObjectKey })
            .ToArrayAsync(cancellationToken);

        foreach (var prompt in prompts)
        {
            prompt.CoreId = null;
        }

        await DeleteObjectKeysAsync(activityImageSnapshots.Select(item => item.ImageObjectKey), cancellationToken);
        await managedImageQuotaService.DeleteManagedImagesAsync(
            PlanImageAssetModule.Activity,
            activityImageSnapshots.Select(item => item.Id).ToArray(),
            cancellationToken);
        if (!string.IsNullOrWhiteSpace(core.ImageObjectKey))
        {
            await objectStorage.DeleteAsync(core.ImageObjectKey, cancellationToken);
        }

        db.Cores.Remove(core);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<ProjectDto>> ListProjectsAsync(Guid? coreId, CancellationToken cancellationToken)
    {
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var projects = await db.Projects
            .AsNoTracking()
            .Where(project => project.SpaceId == currentMember.SpaceId)
            .Where(project => coreId == null || project.CoreId == coreId)
            .Select(project => new
            {
                project.Id,
                project.CoreId,
                CoreName = project.Core!.Name,
                CoreImageUrl = project.Core!.ImageUrl,
                CoreHasImage = project.Core!.ImageObjectKey != null,
                project.Core!.ImageUpdatedAt,
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
                    project.CoreId,
                    project.CoreName,
                    project.CoreImageUrl,
                    project.CoreHasImage,
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
        await commercialPlanService.EnsureCanCreateProjectAsync(userContext.UserId, request.CoreId, cancellationToken);
        var core = await db.Cores
            .FirstOrDefaultAsync(item => item.Id == request.CoreId && item.SpaceId == currentMember.SpaceId, cancellationToken)
            ?? throw new NotFoundException("Núcleo não encontrado.");

        var project = new Project
        {
            SpaceId = currentMember.SpaceId,
            CoreId = core.Id,
            CreatedByMemberId = currentMember.Id,
            Name = RequiredText(request.Name, "Informe o nome do projeto.")
        };

        db.Projects.Add(project);
        await db.SaveChangesAsync(cancellationToken);

        project.Core = core;
        return ToProjectDto(project, 0, currentMember, isOutOfPlan: false);
    }

    public async Task<ProjectDto> UpdateProjectAsync(
        Guid projectId,
        UpdateProjectRequest request,
        CancellationToken cancellationToken,
        DateTimeOffset? expectedUpdatedAt = null)
    {
        EnsureWritable();
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var project = await db.Projects
            .FirstOrDefaultAsync(item => item.Id == projectId && item.SpaceId == currentMember.SpaceId, cancellationToken)
            ?? throw new NotFoundException("Projeto não encontrado.");
        ApplyExpectedVersion(project, expectedUpdatedAt);
        var core = await db.Cores
            .FirstOrDefaultAsync(item => item.Id == request.CoreId && item.SpaceId == currentMember.SpaceId, cancellationToken)
            ?? throw new NotFoundException("Núcleo não encontrado.");

        EnsureCanManageEntity(currentMember, project.CreatedByMemberId, "Você não pode editar um projeto criado por outra pessoa.");

        project.CoreId = core.Id;
        project.Name = RequiredText(request.Name, "Informe o nome do projeto.");
        await db.SaveChangesAsync(cancellationToken);

        var activityCount = await db.Activities
            .CountAsync(
                activity =>
                    activity.SpaceId == currentMember.SpaceId &&
                    activity.ProjectId == project.Id &&
                    activity.Status != ActivityStatus.Concluido,
                cancellationToken);

        project.Core = core;
        return ToProjectDto(
            project,
            activityCount,
            currentMember,
            await IsProjectOutOfPlanAsync(project.Id, cancellationToken));
    }

    public async Task DeleteProjectAsync(Guid projectId, CancellationToken cancellationToken, DateTimeOffset? expectedUpdatedAt = null)
    {
        EnsureWritable();
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var project = await db.Projects
            .FirstOrDefaultAsync(item => item.Id == projectId && item.SpaceId == currentMember.SpaceId, cancellationToken)
            ?? throw new NotFoundException("Projeto não encontrado.");
        ApplyExpectedVersion(project, expectedUpdatedAt);

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
                .ThenInclude(project => project!.Core)
            .Include(activity => activity.ResponsibleMember)
                .ThenInclude(member => member!.User)
            .Include(activity => activity.PendingItems)
            .Include(activity => activity.Comments)
            .Where(activity => activity.SpaceId == currentMember.SpaceId)
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
            .Include(item => item.Core)
            .FirstOrDefaultAsync(item => item.Id == request.ProjectId && item.SpaceId == currentMember.SpaceId, cancellationToken)
            ?? throw new NotFoundException("Projeto não encontrado.");

        if (request.ResponsibleMemberId.HasValue)
        {
            await EnsureMemberAsync(currentMember.SpaceId, request.ResponsibleMemberId.Value, cancellationToken);
        }

        ValidateActivitySize(request.Size);

        var activity = new Activity
        {
            SpaceId = currentMember.SpaceId,
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
        CancellationToken cancellationToken,
        DateTimeOffset? expectedUpdatedAt = null)
    {
        EnsureWritable();
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var activity = await db.Activities
            .FirstOrDefaultAsync(item => item.Id == activityId && item.SpaceId == currentMember.SpaceId, cancellationToken)
            ?? throw new NotFoundException("Atividade não encontrada.");
        ApplyExpectedVersion(activity, expectedUpdatedAt);
        var project = await db.Projects
            .FirstOrDefaultAsync(item => item.Id == request.ProjectId && item.SpaceId == currentMember.SpaceId, cancellationToken)
            ?? throw new NotFoundException("Projeto não encontrado.");

        EnsureCanManageEntity(currentMember, activity.CreatedByMemberId, "Você não pode editar uma atividade criada por outra pessoa.");

        if (request.ResponsibleMemberId.HasValue)
        {
            await EnsureMemberAsync(currentMember.SpaceId, request.ResponsibleMemberId.Value, cancellationToken);
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

        var updated = await FindActivityForOutputAsync(currentMember.SpaceId, activity.Id, cancellationToken);
        return ToActivityDto(updated, currentMember);
    }

    public async Task<ActivityDto> UpdateActivityStatusAsync(
        Guid activityId,
        UpdateActivityStatusRequest request,
        CancellationToken cancellationToken,
        DateTimeOffset? expectedUpdatedAt = null)
    {
        EnsureWritable();
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var activity = await db.Activities
            .Include(item => item.Project)
                .ThenInclude(project => project!.Core)
            .Include(item => item.ResponsibleMember)
                .ThenInclude(member => member!.User)
            .Include(item => item.PendingItems)
            .Include(item => item.Comments)
            .FirstOrDefaultAsync(item => item.Id == activityId && item.SpaceId == currentMember.SpaceId, cancellationToken)
            ?? throw new NotFoundException("Atividade não encontrada.");
        ApplyExpectedVersion(activity, expectedUpdatedAt);

        EnsureCanManageEntity(currentMember, activity.CreatedByMemberId, "Você não pode editar uma atividade criada por outra pessoa.");

        activity.CompletedAt = ResolveCompletedAt(activity.Status, request.Status, activity.CompletedAt);
        activity.Status = request.Status;
        await db.SaveChangesAsync(cancellationToken);
        return ToActivityDto(activity, currentMember);
    }

    public async Task DeleteActivityAsync(Guid activityId, CancellationToken cancellationToken, DateTimeOffset? expectedUpdatedAt = null)
    {
        EnsureWritable();
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var activity = await db.Activities
            .FirstOrDefaultAsync(item => item.Id == activityId && item.SpaceId == currentMember.SpaceId, cancellationToken)
            ?? throw new NotFoundException("Atividade não encontrada.");
        ApplyExpectedVersion(activity, expectedUpdatedAt);

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
            .FirstOrDefaultAsync(item => item.Id == activityId && item.SpaceId == currentMember.SpaceId, cancellationToken)
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

        var updated = await FindActivityForOutputAsync(currentMember.SpaceId, activity.Id, cancellationToken);
        return ToActivityDto(updated, currentMember);
    }

    public async Task<StoredObject> GetActivityImageAsync(Guid activityId, CancellationToken cancellationToken)
    {
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var activity = await db.Activities
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == activityId && item.SpaceId == currentMember.SpaceId, cancellationToken)
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
            .FirstOrDefaultAsync(item => item.Id == activityId && item.SpaceId == currentMember.SpaceId, cancellationToken)
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

        var updated = await FindActivityForOutputAsync(currentMember.SpaceId, activity.Id, cancellationToken);
        return ToActivityDto(updated, currentMember);
    }

    public async Task<IReadOnlyCollection<ActivityCommentDto>> ListActivityCommentsAsync(
        Guid activityId,
        CancellationToken cancellationToken)
    {
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        await EnsureActivityAsync(currentMember.SpaceId, activityId, cancellationToken);

        var comments = await db.ActivityComments
            .AsNoTracking()
            .Include(comment => comment.AuthorMember)
                .ThenInclude(member => member!.User)
            .Where(comment => comment.SpaceId == currentMember.SpaceId && comment.ActivityId == activityId)
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
        await EnsureActivityAsync(author.SpaceId, activityId, cancellationToken);

        var comment = new ActivityComment
        {
            SpaceId = author.SpaceId,
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
        CancellationToken cancellationToken,
        DateTimeOffset? expectedUpdatedAt = null)
    {
        EnsureWritable();
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var comment = await db.ActivityComments
            .Include(item => item.AuthorMember)
                .ThenInclude(member => member!.User)
            .FirstOrDefaultAsync(item =>
                item.Id == commentId &&
                item.ActivityId == activityId &&
                item.SpaceId == currentMember.SpaceId,
                cancellationToken)
            ?? throw new NotFoundException("Comentário não encontrado.");
        ApplyExpectedVersion(comment, expectedUpdatedAt);

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
        CancellationToken cancellationToken,
        DateTimeOffset? expectedUpdatedAt = null)
    {
        EnsureWritable();
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var comment = await db.ActivityComments
            .FirstOrDefaultAsync(item =>
                item.Id == commentId &&
                item.ActivityId == activityId &&
                item.SpaceId == currentMember.SpaceId,
                cancellationToken)
            ?? throw new NotFoundException("Comentário não encontrado.");
        ApplyExpectedVersion(comment, expectedUpdatedAt);

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
        await EnsureActivityAsync(currentMember.SpaceId, activityId, cancellationToken);

        return await db.PendingItems
            .AsNoTracking()
            .Where(item => item.SpaceId == currentMember.SpaceId && item.ActivityId == activityId)
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
            .FirstOrDefaultAsync(item => item.Id == activityId && item.SpaceId == currentMember.SpaceId, cancellationToken)
            ?? throw new NotFoundException("Atividade não encontrada.");

        EnsureCanManageEntity(currentMember, activity.CreatedByMemberId, "Você não pode alterar uma atividade criada por outra pessoa.");

        var pendingItem = new PendingItem
        {
            SpaceId = currentMember.SpaceId,
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

    private async Task EnsureMemberAsync(Guid spaceId, Guid memberId, CancellationToken cancellationToken)
    {
        var exists = await db.SpaceMembers
            .AnyAsync(member => member.Id == memberId && member.SpaceId == spaceId && member.IsActive, cancellationToken);

        if (!exists)
        {
            throw new ValidationException("Responsável inválido para este espaço.");
        }
    }

    private async Task<Activity> FindActivityForOutputAsync(
        Guid spaceId,
        Guid activityId,
        CancellationToken cancellationToken)
    {
        return await db.Activities
            .AsNoTracking()
            .Include(activity => activity.Project)
                .ThenInclude(project => project!.Core)
            .Include(activity => activity.ResponsibleMember)
                .ThenInclude(member => member!.User)
            .Include(activity => activity.PendingItems)
            .Include(activity => activity.Comments)
            .FirstOrDefaultAsync(activity => activity.Id == activityId && activity.SpaceId == spaceId, cancellationToken)
            ?? throw new NotFoundException("Atividade não encontrada.");
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

    private async Task EnsureActivityAsync(Guid spaceId, Guid activityId, CancellationToken cancellationToken)
    {
        var exists = await db.Activities
            .AnyAsync(activity => activity.Id == activityId && activity.SpaceId == spaceId, cancellationToken);

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
            throw new ValidationException("Informe uma URL de imagem válida para o núcleo.");
        }

        return normalized;
    }

    private static CoreDto ToCoreDto(
        Core core,
        int projectCount,
        SpaceMember currentMember,
        bool isOutOfPlan)
    {
        var canManage = CanManageEntity(currentMember, core.CreatedByMemberId);
        return new CoreDto(
            core.Id,
            core.Name,
            core.ImageUrl,
            !string.IsNullOrWhiteSpace(core.ImageObjectKey),
            core.ImageUpdatedAt,
            core.CreatedByMemberId,
            projectCount,
            isOutOfPlan,
            canManage,
            canManage);
    }

    private static ProjectDto ToProjectDto(
        Project project,
        int activityCount,
        SpaceMember currentMember,
        bool isOutOfPlan)
    {
        var canManage = CanManageEntity(currentMember, project.CreatedByMemberId);
        return new ProjectDto(
            project.Id,
            project.CoreId,
            project.Core?.Name ?? string.Empty,
            project.Core?.ImageUrl,
            !string.IsNullOrWhiteSpace(project.Core?.ImageObjectKey),
            project.Core?.ImageUpdatedAt,
            project.Name,
            project.CreatedByMemberId,
            activityCount,
            isOutOfPlan,
            canManage,
            canManage);
    }

    private static ActivityDto ToActivityDto(Activity activity, SpaceMember currentMember)
    {
        var canManage = CanManageEntity(currentMember, activity.CreatedByMemberId);
        return new ActivityDto(
            activity.Id,
            activity.ProjectId,
            activity.Project!.Name,
            activity.Project.CoreId,
            activity.Project.Core!.Name,
            activity.Project.Core.ImageUrl,
            !string.IsNullOrWhiteSpace(activity.Project.Core.ImageObjectKey),
            activity.Project.Core.ImageUpdatedAt,
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

    private async Task<bool> IsCoreOutOfPlanAsync(Guid coreId, CancellationToken cancellationToken)
    {
        var outOfPlanCoreIds = await ResolveOutOfPlanCoreIdsAsync(
            await db.Cores
                .AsNoTracking()
                .Where(core => core.Id == coreId)
                .Select(core => new CreatorScopedEntitySnapshot(
                    core.Id,
                    core.CreatedByMember != null ? core.CreatedByMember.UserId : (Guid?)null,
                    core.CreatedAt))
                .ToArrayAsync(cancellationToken),
            cancellationToken);

        return outOfPlanCoreIds.Contains(coreId);
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

    private async Task<HashSet<Guid>> ResolveOutOfPlanCoreIdsAsync(
        IEnumerable<CreatorScopedEntitySnapshot> visibleCores,
        CancellationToken cancellationToken)
    {
        return await ResolveOutOfPlanEntityIdsAsync(
            visibleCores,
            userIds => db.Cores
                .AsNoTracking()
                .Where(core => core.CreatedByMember != null && userIds.Contains(core.CreatedByMember.UserId))
                .Select(core => new CreatorScopedEntitySnapshot(
                    core.Id,
                    core.CreatedByMember != null ? core.CreatedByMember.UserId : (Guid?)null,
                    core.CreatedAt))
                .ToArrayAsync(cancellationToken),
            plan => plan.MaxCores,
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

    private static ActivityCommentDto ToCommentDto(ActivityComment comment, SpaceMember currentMember)
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

    private static bool IsContentManager(SpaceMember member)
    {
        return member.Role is SpaceRole.Owner or SpaceRole.Admin;
    }

    private static bool CanManageEntity(SpaceMember member, Guid? createdByMemberId)
    {
        return IsContentManager(member) || createdByMemberId == member.Id;
    }

    private static bool CanDeleteComment(SpaceMember member, Guid authorMemberId)
    {
        return IsContentManager(member) || authorMemberId == member.Id;
    }

    private sealed record CreatorScopedEntitySnapshot(
        Guid Id,
        Guid? CreatedByUserId,
        DateTimeOffset CreatedAt);

    private static void EnsureCanManageEntity(
        SpaceMember member,
        Guid? createdByMemberId,
        string message)
    {
        if (!CanManageEntity(member, createdByMemberId))
        {
            throw new ForbiddenException(message);
        }
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

    private void ApplyExpectedVersion(AuditableEntity entity, DateTimeOffset? expectedUpdatedAt)
    {
        if (expectedUpdatedAt.HasValue)
        {
            db.SetExpectedUpdatedAt(entity, expectedUpdatedAt.Value);
        }
    }
}
