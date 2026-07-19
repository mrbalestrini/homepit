using OrganizaClub.Application.Common;
using OrganizaClub.Application.Images;
using OrganizaClub.Application.Plans;
using OrganizaClub.Application.Storage;
using OrganizaClub.Domain.Spaces;
using OrganizaClub.Domain.Plans;
using OrganizaClub.Domain.Prompts;
using Microsoft.EntityFrameworkCore;

namespace OrganizaClub.Application.Prompts;

public sealed class PromptService(
    IOrganizaClubDbContext db,
    IUserContext userContext,
    IObjectStorage objectStorage,
    IImageUploadProcessor imageUploadProcessor,
    TimeProvider timeProvider,
    ManagedImageQuotaService managedImageQuotaService)
{
    private const int DefaultPageSize = 12;
    private const int MaxPageSize = 48;
    private const int PromptTextMaxLength = 20000;
    private const string SuperAdminReadOnlyMessage = "O superadmin possui acesso somente leitura nesta etapa.";

    private static readonly ImageUploadValidationMessages PromptImageMessages = new(
        "Envie uma imagem com conteúdo para o prompt.",
        "A imagem do prompt deve ter no máximo 5 MB.",
        "A imagem do prompt deve estar em JPG, PNG, WEBP, GIF ou BMP.",
        "Envie um arquivo de imagem válido para o prompt.",
        "Imagens animadas não são aceitas no prompt.");

    public async Task<PromptListResponse> ListPromptsAsync(
        string? search,
        Guid? coreId,
        bool withoutCore,
        bool archivedOnly,
        IReadOnlyCollection<Guid>? categoryIds,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var normalizedSearch = NormalizeOptional(search)?.ToLowerInvariant();
        var normalizedCategoryIds = (categoryIds ?? Array.Empty<Guid>())
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToArray();

        if (withoutCore && coreId.HasValue)
        {
            throw new ValidationException("Escolha um núcleo específico ou use o filtro sem núcleo.");
        }

        var sanitizedPage = Math.Max(1, page);
        var sanitizedPageSize = pageSize <= 0 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);

        var query = db.Prompts
            .AsNoTracking()
            .Include(prompt => prompt.Core)
            .Include(prompt => prompt.CategoryAssignments)
                .ThenInclude(assignment => assignment.Category)
            .Where(prompt => prompt.SpaceId == currentMember.SpaceId && prompt.IsArchived == archivedOnly);

        if (normalizedSearch is not null)
        {
            query = query.Where(prompt =>
                prompt.Title.ToLower().Contains(normalizedSearch) ||
                (prompt.Description != null && prompt.Description.ToLower().Contains(normalizedSearch)) ||
                prompt.PromptText.ToLower().Contains(normalizedSearch) ||
                (prompt.LinkTitle != null && prompt.LinkTitle.ToLower().Contains(normalizedSearch)) ||
                prompt.CategoryAssignments.Any(assignment => assignment.Category!.Name.ToLower().Contains(normalizedSearch)) ||
                (prompt.Core != null && prompt.Core.Name.ToLower().Contains(normalizedSearch)));
        }

        if (withoutCore)
        {
            query = query.Where(prompt => prompt.CoreId == null);
        }
        else if (coreId.HasValue)
        {
            query = query.Where(prompt => prompt.CoreId == coreId.Value);
        }

        if (normalizedCategoryIds.Length > 0)
        {
            query = query.Where(prompt => prompt.CategoryAssignments.Any(assignment => normalizedCategoryIds.Contains(assignment.CategoryId)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(prompt => prompt.UpdatedAt)
            .ThenBy(prompt => prompt.Title)
            .Skip((sanitizedPage - 1) * sanitizedPageSize)
            .Take(sanitizedPageSize)
            .ToArrayAsync(cancellationToken);

        return new PromptListResponse(
            items.Select(prompt => ToPromptListItemDto(prompt, currentMember)).ToArray(),
            sanitizedPage,
            sanitizedPageSize,
            totalCount);
    }

    public async Task<PromptDetailDto> GetPromptAsync(Guid promptId, CancellationToken cancellationToken)
    {
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var prompt = await FindPromptForOutputAsync(currentMember.SpaceId, promptId, cancellationToken);
        return ToPromptDetailDto(prompt, currentMember);
    }

    public async Task<PromptDetailDto> CreatePromptAsync(CreatePromptRequest request, CancellationToken cancellationToken)
    {
        EnsureWritable();
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var normalizedCategoryIds = await ValidatePromptPayloadAsync(
            currentMember.SpaceId,
            request.CoreId,
            request.Title,
            request.Description,
            request.PromptText,
            request.CategoryIds,
            request.LinkUrl,
            request.LinkTitle,
            cancellationToken);

        var prompt = new Prompt
        {
            SpaceId = currentMember.SpaceId,
            CreatedByMemberId = currentMember.Id,
            CoreId = request.CoreId,
            Title = RequiredText(request.Title, "Informe o título do prompt."),
            Description = NormalizeOptional(request.Description),
            PromptText = RequiredPromptText(request.PromptText),
            LinkUrl = NormalizeUrl(request.LinkUrl, "Informe um link válido."),
            LinkTitle = NormalizeOptional(request.LinkTitle)
        };

        foreach (var categoryId in normalizedCategoryIds)
        {
            prompt.CategoryAssignments.Add(new PromptCategoryAssignment
            {
                Prompt = prompt,
                CategoryId = categoryId
            });
        }

        db.Prompts.Add(prompt);
        await db.SaveChangesAsync(cancellationToken);

        var created = await FindPromptForOutputAsync(currentMember.SpaceId, prompt.Id, cancellationToken);
        return ToPromptDetailDto(created, currentMember);
    }

    public async Task<PromptDetailDto> UpdatePromptAsync(Guid promptId, UpdatePromptRequest request, CancellationToken cancellationToken)
    {
        EnsureWritable();
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var prompt = await db.Prompts
            .Include(item => item.CategoryAssignments)
            .FirstOrDefaultAsync(item => item.Id == promptId && item.SpaceId == currentMember.SpaceId, cancellationToken)
            ?? throw new NotFoundException("Prompt não encontrado.");

        EnsureCanManageEntity(currentMember, prompt.CreatedByMemberId, "Você não pode editar um prompt criado por outra pessoa.");

        var normalizedCategoryIds = await ValidatePromptPayloadAsync(
            currentMember.SpaceId,
            request.CoreId,
            request.Title,
            request.Description,
            request.PromptText,
            request.CategoryIds,
            request.LinkUrl,
            request.LinkTitle,
            cancellationToken);

        prompt.CoreId = request.CoreId;
        prompt.Title = RequiredText(request.Title, "Informe o título do prompt.");
        prompt.Description = NormalizeOptional(request.Description);
        prompt.PromptText = RequiredPromptText(request.PromptText);
        prompt.LinkUrl = NormalizeUrl(request.LinkUrl, "Informe um link válido.");
        prompt.LinkTitle = NormalizeOptional(request.LinkTitle);

        var removedAssignments = prompt.CategoryAssignments
            .Where(assignment => !normalizedCategoryIds.Contains(assignment.CategoryId))
            .ToArray();

        foreach (var assignment in removedAssignments)
        {
            prompt.CategoryAssignments.Remove(assignment);
        }

        var existingCategoryIds = prompt.CategoryAssignments
            .Select(assignment => assignment.CategoryId)
            .ToHashSet();

        foreach (var categoryId in normalizedCategoryIds.Where(categoryId => !existingCategoryIds.Contains(categoryId)))
        {
            prompt.CategoryAssignments.Add(new PromptCategoryAssignment
            {
                PromptId = prompt.Id,
                CategoryId = categoryId
            });
        }

        await db.SaveChangesAsync(cancellationToken);

        var updated = await FindPromptForOutputAsync(currentMember.SpaceId, prompt.Id, cancellationToken);
        return ToPromptDetailDto(updated, currentMember);
    }

    public async Task DeletePromptAsync(Guid promptId, CancellationToken cancellationToken)
    {
        EnsureWritable();
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var prompt = await db.Prompts
            .FirstOrDefaultAsync(item => item.Id == promptId && item.SpaceId == currentMember.SpaceId, cancellationToken)
            ?? throw new NotFoundException("Prompt não encontrado.");

        EnsureCanManageEntity(currentMember, prompt.CreatedByMemberId, "Você não pode excluir um prompt criado por outra pessoa.");

        if (!string.IsNullOrWhiteSpace(prompt.ImageObjectKey))
        {
            await objectStorage.DeleteAsync(prompt.ImageObjectKey, cancellationToken);
        }

        await managedImageQuotaService.DeleteManagedImageAsync(PlanImageAssetModule.Prompt, prompt.Id, cancellationToken);
        db.Prompts.Remove(prompt);
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task<PromptDetailDto> ArchivePromptAsync(Guid promptId, CancellationToken cancellationToken)
    {
        return SetPromptArchivedStateAsync(promptId, true, cancellationToken);
    }

    public Task<PromptDetailDto> UnarchivePromptAsync(Guid promptId, CancellationToken cancellationToken)
    {
        return SetPromptArchivedStateAsync(promptId, false, cancellationToken);
    }

    public async Task<PromptDetailDto> UploadPromptImageAsync(
        Guid promptId,
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
            PromptImageMessages,
            cancellationToken);
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var prompt = await db.Prompts
            .Include(item => item.CategoryAssignments)
                .ThenInclude(assignment => assignment.Category)
            .Include(item => item.Core)
            .FirstOrDefaultAsync(item => item.Id == promptId && item.SpaceId == currentMember.SpaceId, cancellationToken)
            ?? throw new NotFoundException("Prompt não encontrado.");

        EnsureCanManageEntity(currentMember, prompt.CreatedByMemberId, "Você não pode editar um prompt criado por outra pessoa.");

        var objectKey = ObjectStorageKeys.PromptImage(prompt.Id);
        await using var uploadStream = new MemoryStream(preparedImage.Content, writable: false);
        await objectStorage.PutAsync(
            new ObjectStoragePutRequest(objectKey, uploadStream, preparedImage.ContentLength, preparedImage.ContentType),
            cancellationToken);

        prompt.ImageObjectKey = objectKey;
        prompt.ImageContentType = preparedImage.ContentType;
        prompt.ImageUpdatedAt = timeProvider.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);
        await managedImageQuotaService.RegisterManagedImageAsync(
            userContext.UserId,
            PlanImageAssetModule.Prompt,
            prompt.Id,
            objectKey,
            preparedImage.ContentType,
            cancellationToken);

        return ToPromptDetailDto(prompt, currentMember);
    }

    public async Task<StoredObject> GetPromptImageAsync(Guid promptId, CancellationToken cancellationToken)
    {
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var prompt = await db.Prompts
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == promptId && item.SpaceId == currentMember.SpaceId, cancellationToken)
            ?? throw new NotFoundException("Prompt não encontrado.");

        if (string.IsNullOrWhiteSpace(prompt.ImageObjectKey))
        {
            throw new NotFoundException("Imagem do prompt não encontrada.");
        }

        return await objectStorage.GetAsync(prompt.ImageObjectKey, cancellationToken);
    }

    public async Task<PromptDetailDto> DeletePromptImageAsync(Guid promptId, CancellationToken cancellationToken)
    {
        EnsureWritable();
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var prompt = await db.Prompts
            .Include(item => item.CategoryAssignments)
                .ThenInclude(assignment => assignment.Category)
            .Include(item => item.Core)
            .FirstOrDefaultAsync(item => item.Id == promptId && item.SpaceId == currentMember.SpaceId, cancellationToken)
            ?? throw new NotFoundException("Prompt não encontrado.");

        EnsureCanManageEntity(currentMember, prompt.CreatedByMemberId, "Você não pode editar um prompt criado por outra pessoa.");

        if (string.IsNullOrWhiteSpace(prompt.ImageObjectKey))
        {
            throw new NotFoundException("Imagem do prompt não encontrada.");
        }

        await objectStorage.DeleteAsync(prompt.ImageObjectKey, cancellationToken);
        prompt.ImageObjectKey = null;
        prompt.ImageContentType = null;
        prompt.ImageUpdatedAt = null;
        await db.SaveChangesAsync(cancellationToken);
        await managedImageQuotaService.DeleteManagedImageAsync(PlanImageAssetModule.Prompt, prompt.Id, cancellationToken);

        return ToPromptDetailDto(prompt, currentMember);
    }

    public async Task<IReadOnlyCollection<PromptCategoryDto>> ListCategoriesAsync(CancellationToken cancellationToken)
    {
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var isManager = IsContentManager(currentMember);
        return await db.PromptCategories
            .AsNoTracking()
            .Where(category => category.SpaceId == currentMember.SpaceId)
            .OrderBy(category => category.Name)
            .Select(category => new PromptCategoryDto(
                category.Id,
                category.Name,
                category.CreatedByMemberId,
                category.PromptAssignments.Count,
                category.PromptAssignments.Count(assignment => assignment.Prompt != null && assignment.Prompt.CategoryAssignments.Count == 1),
                isManager || category.CreatedByMemberId == currentMember.Id,
                isManager || category.CreatedByMemberId == currentMember.Id))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<PromptCategoryDto> CreateCategoryAsync(CreatePromptCategoryRequest request, CancellationToken cancellationToken)
    {
        EnsureWritable();
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var category = new PromptCategory
        {
            SpaceId = currentMember.SpaceId,
            CreatedByMemberId = currentMember.Id,
            Name = RequiredText(request.Name, "Informe o nome da categoria.")
        };

        db.PromptCategories.Add(category);
        await db.SaveChangesAsync(cancellationToken);

        return new PromptCategoryDto(category.Id, category.Name, category.CreatedByMemberId, 0, 0, true, true);
    }

    public async Task<PromptCategoryDto> UpdateCategoryAsync(
        Guid categoryId,
        UpdatePromptCategoryRequest request,
        CancellationToken cancellationToken)
    {
        EnsureWritable();
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var category = await db.PromptCategories
            .Include(item => item.PromptAssignments)
                .ThenInclude(assignment => assignment.Prompt)
                    .ThenInclude(prompt => prompt!.CategoryAssignments)
            .FirstOrDefaultAsync(item => item.Id == categoryId && item.SpaceId == currentMember.SpaceId, cancellationToken)
            ?? throw new NotFoundException("Categoria não encontrada.");

        EnsureCanManageEntity(currentMember, category.CreatedByMemberId, "Você não pode editar uma categoria criada por outra pessoa.");

        category.Name = RequiredText(request.Name, "Informe o nome da categoria.");
        await db.SaveChangesAsync(cancellationToken);

        var usageCount = category.PromptAssignments.Select(assignment => assignment.PromptId).Distinct().Count();
        var replacementRequiredCount = category.PromptAssignments.Count(assignment => assignment.Prompt?.CategoryAssignments.Count == 1);
        var canManage = CanManageEntity(currentMember, category.CreatedByMemberId);

        return new PromptCategoryDto(
            category.Id,
            category.Name,
            category.CreatedByMemberId,
            usageCount,
            replacementRequiredCount,
            canManage,
            canManage);
    }

    public async Task DeleteCategoryAsync(Guid categoryId, Guid? replacementCategoryId, CancellationToken cancellationToken)
    {
        EnsureWritable();
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var category = await db.PromptCategories
            .Include(item => item.PromptAssignments)
                .ThenInclude(assignment => assignment.Prompt)
                    .ThenInclude(prompt => prompt!.CategoryAssignments)
            .FirstOrDefaultAsync(item => item.Id == categoryId && item.SpaceId == currentMember.SpaceId, cancellationToken)
            ?? throw new NotFoundException("Categoria não encontrada.");

        EnsureCanManageEntity(currentMember, category.CreatedByMemberId, "Você não pode excluir uma categoria criada por outra pessoa.");

        if (replacementCategoryId == categoryId)
        {
            throw new ValidationException("Escolha uma categoria de substituição diferente.");
        }

        PromptCategory? replacementCategory = null;
        if (replacementCategoryId.HasValue)
        {
            replacementCategory = await db.PromptCategories
                .FirstOrDefaultAsync(item => item.Id == replacementCategoryId.Value && item.SpaceId == currentMember.SpaceId, cancellationToken)
                ?? throw new NotFoundException("Categoria de substituição não encontrada.");
        }

        var promptsRequiringReplacement = category.PromptAssignments
            .Where(assignment => assignment.Prompt?.CategoryAssignments.Count == 1)
            .Select(assignment => assignment.Prompt!)
            .DistinctBy(prompt => prompt.Id)
            .ToArray();

        if (promptsRequiringReplacement.Length > 0 && replacementCategory is null)
        {
            throw new ValidationException("Escolha uma categoria de substituição para prompts que ficariam sem categoria.");
        }

        if (replacementCategory is not null)
        {
            var promptIds = promptsRequiringReplacement.Select(prompt => prompt.Id).ToArray();
            var existingAssignments = await db.PromptCategoryAssignments
                .Where(assignment => promptIds.Contains(assignment.PromptId) && assignment.CategoryId == replacementCategory.Id)
                .Select(assignment => assignment.PromptId)
                .ToArrayAsync(cancellationToken);

            foreach (var promptId in promptIds.Where(promptId => !existingAssignments.Contains(promptId)))
            {
                db.PromptCategoryAssignments.Add(new PromptCategoryAssignment
                {
                    PromptId = promptId,
                    CategoryId = replacementCategory.Id
                });
            }
        }

        db.PromptCategories.Remove(category);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<Prompt> FindPromptForOutputAsync(Guid spaceId, Guid promptId, CancellationToken cancellationToken)
    {
        return await db.Prompts
            .AsNoTracking()
            .Include(prompt => prompt.Core)
            .Include(prompt => prompt.CategoryAssignments)
                .ThenInclude(assignment => assignment.Category)
            .FirstOrDefaultAsync(prompt => prompt.Id == promptId && prompt.SpaceId == spaceId, cancellationToken)
            ?? throw new NotFoundException("Prompt não encontrado.");
    }

    private async Task<PromptDetailDto> SetPromptArchivedStateAsync(Guid promptId, bool isArchived, CancellationToken cancellationToken)
    {
        EnsureWritable();
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var prompt = await db.Prompts
            .Include(item => item.CategoryAssignments)
                .ThenInclude(assignment => assignment.Category)
            .Include(item => item.Core)
            .FirstOrDefaultAsync(item => item.Id == promptId && item.SpaceId == currentMember.SpaceId, cancellationToken)
            ?? throw new NotFoundException("Prompt não encontrado.");

        EnsureCanManageEntity(
            currentMember,
            prompt.CreatedByMemberId,
            isArchived
                ? "Você não pode arquivar um prompt criado por outra pessoa."
                : "Você não pode desarquivar um prompt criado por outra pessoa.");

        prompt.IsArchived = isArchived;
        await db.SaveChangesAsync(cancellationToken);

        return ToPromptDetailDto(prompt, currentMember);
    }

    private async Task<IReadOnlyCollection<Guid>> ValidatePromptPayloadAsync(
        Guid spaceId,
        Guid? coreId,
        string title,
        string? description,
        string promptText,
        IReadOnlyCollection<Guid> categoryIds,
        string? linkUrl,
        string? linkTitle,
        CancellationToken cancellationToken)
    {
        RequiredText(title, "Informe o título do prompt.");
        RequiredPromptText(promptText);
        NormalizeOptional(description);

        var normalizedLinkUrl = NormalizeOptional(linkUrl);
        var normalizedLinkTitle = NormalizeOptional(linkTitle);
        if ((normalizedLinkUrl is null) != (normalizedLinkTitle is null))
        {
            throw new ValidationException("Informe título e URL do link juntos.");
        }

        if (normalizedLinkUrl is not null)
        {
            NormalizeUrl(normalizedLinkUrl, "Informe um link válido.");
        }

        if (coreId.HasValue)
        {
            var coreExists = await db.Cores
                .AnyAsync(core => core.Id == coreId.Value && core.SpaceId == spaceId, cancellationToken);

            if (!coreExists)
            {
                throw new ValidationException("Núcleo inválido para este espaço.");
            }
        }

        var normalizedCategoryIds = categoryIds
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToArray();

        if (normalizedCategoryIds.Length == 0)
        {
            throw new ValidationException("Selecione pelo menos uma categoria.");
        }

        var validCategoryIds = await db.PromptCategories
            .Where(category => category.SpaceId == spaceId && normalizedCategoryIds.Contains(category.Id))
            .Select(category => category.Id)
            .ToArrayAsync(cancellationToken);

        if (validCategoryIds.Length != normalizedCategoryIds.Length)
        {
            throw new ValidationException("Selecione apenas categorias válidas do espaço ativo.");
        }

        return normalizedCategoryIds;
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

    private static PromptListItemDto ToPromptListItemDto(Prompt prompt, SpaceMember currentMember)
    {
        var canManage = CanManageEntity(currentMember, prompt.CreatedByMemberId);
        return new PromptListItemDto(
            prompt.Id,
            prompt.CoreId,
            prompt.Core?.Name,
            prompt.Core?.ImageUrl,
            !string.IsNullOrWhiteSpace(prompt.Core?.ImageObjectKey),
            prompt.Core?.ImageUpdatedAt,
            prompt.Title,
            prompt.Description,
            prompt.PromptText,
            prompt.CategoryAssignments
                .Select(assignment => new PromptCategoryReferenceDto(assignment.CategoryId, assignment.Category?.Name ?? string.Empty))
                .OrderBy(category => category.Name)
                .ToArray(),
            prompt.LinkUrl,
            prompt.LinkTitle,
            prompt.CreatedByMemberId,
            prompt.IsArchived,
            !string.IsNullOrWhiteSpace(prompt.ImageObjectKey),
            prompt.ImageUpdatedAt,
            prompt.UpdatedAt,
            canManage,
            canManage);
    }

    private static PromptDetailDto ToPromptDetailDto(Prompt prompt, SpaceMember currentMember)
    {
        var canManage = CanManageEntity(currentMember, prompt.CreatedByMemberId);
        return new PromptDetailDto(
            prompt.Id,
            prompt.CoreId,
            prompt.Core?.Name,
            prompt.Core?.ImageUrl,
            !string.IsNullOrWhiteSpace(prompt.Core?.ImageObjectKey),
            prompt.Core?.ImageUpdatedAt,
            prompt.Title,
            prompt.Description,
            prompt.PromptText,
            prompt.CategoryAssignments
                .Select(assignment => new PromptCategoryReferenceDto(assignment.CategoryId, assignment.Category?.Name ?? string.Empty))
                .OrderBy(category => category.Name)
                .ToArray(),
            prompt.LinkUrl,
            prompt.LinkTitle,
            prompt.CreatedByMemberId,
            prompt.IsArchived,
            !string.IsNullOrWhiteSpace(prompt.ImageObjectKey),
            prompt.ImageUpdatedAt,
            prompt.CreatedAt,
            prompt.UpdatedAt,
            canManage,
            canManage);
    }

    private static string RequiredText(string value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ValidationException(message);
        }

        return value.Trim();
    }

    private static string RequiredPromptText(string value)
    {
        var normalized = RequiredText(value, "Informe o texto do prompt.");
        if (normalized.Length > PromptTextMaxLength)
        {
            throw new ValidationException($"O texto do prompt deve ter no máximo {PromptTextMaxLength} caracteres.");
        }

        return normalized;
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? NormalizeUrl(string? value, string message)
    {
        var normalized = NormalizeOptional(value);
        if (normalized is null)
        {
            return null;
        }

        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ValidationException(message);
        }

        return normalized;
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
