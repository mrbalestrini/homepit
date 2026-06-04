using HomePit.Application.Common;
using HomePit.Application.Storage;
using HomePit.Domain.Households;
using HomePit.Domain.Prompts;
using Microsoft.EntityFrameworkCore;

namespace HomePit.Application.Prompts;

public sealed class PromptService(
    IHomePitDbContext db,
    IUserContext userContext,
    IObjectStorage objectStorage,
    TimeProvider timeProvider)
{
    private const int DefaultPageSize = 12;
    private const int MaxPageSize = 48;
    private const long PromptImageMaxBytes = 5 * 1024 * 1024;

    private static readonly HashSet<string> AllowedPromptImageContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp"
    };

    public async Task<PromptListResponse> ListPromptsAsync(
        string? search,
        Guid? universeId,
        bool withoutUniverse,
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

        if (withoutUniverse && universeId.HasValue)
        {
            throw new ValidationException("Escolha um universo específico ou use o filtro sem universo.");
        }

        var sanitizedPage = Math.Max(1, page);
        var sanitizedPageSize = pageSize <= 0 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);

        var query = db.Prompts
            .AsNoTracking()
            .Include(prompt => prompt.Universe)
            .Include(prompt => prompt.CategoryAssignments)
                .ThenInclude(assignment => assignment.Category)
            .Where(prompt => prompt.HouseholdId == currentMember.HouseholdId);

        if (normalizedSearch is not null)
        {
            query = query.Where(prompt =>
                prompt.Title.ToLower().Contains(normalizedSearch) ||
                (prompt.Description != null && prompt.Description.ToLower().Contains(normalizedSearch)) ||
                prompt.PromptText.ToLower().Contains(normalizedSearch) ||
                (prompt.LinkTitle != null && prompt.LinkTitle.ToLower().Contains(normalizedSearch)) ||
                prompt.CategoryAssignments.Any(assignment => assignment.Category!.Name.ToLower().Contains(normalizedSearch)) ||
                (prompt.Universe != null && prompt.Universe.Name.ToLower().Contains(normalizedSearch)));
        }

        if (withoutUniverse)
        {
            query = query.Where(prompt => prompt.UniverseId == null);
        }
        else if (universeId.HasValue)
        {
            query = query.Where(prompt => prompt.UniverseId == universeId.Value);
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
        var prompt = await FindPromptForOutputAsync(currentMember.HouseholdId, promptId, cancellationToken);
        return ToPromptDetailDto(prompt, currentMember);
    }

    public async Task<PromptDetailDto> CreatePromptAsync(CreatePromptRequest request, CancellationToken cancellationToken)
    {
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var normalizedCategoryIds = await ValidatePromptPayloadAsync(
            currentMember.HouseholdId,
            request.UniverseId,
            request.Title,
            request.Description,
            request.PromptText,
            request.CategoryIds,
            request.LinkUrl,
            request.LinkTitle,
            cancellationToken);

        var prompt = new Prompt
        {
            HouseholdId = currentMember.HouseholdId,
            CreatedByMemberId = currentMember.Id,
            UniverseId = request.UniverseId,
            Title = RequiredText(request.Title, "Informe o título do prompt."),
            Description = NormalizeOptional(request.Description),
            PromptText = RequiredText(request.PromptText, "Informe o texto do prompt."),
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

        var created = await FindPromptForOutputAsync(currentMember.HouseholdId, prompt.Id, cancellationToken);
        return ToPromptDetailDto(created, currentMember);
    }

    public async Task<PromptDetailDto> UpdatePromptAsync(Guid promptId, UpdatePromptRequest request, CancellationToken cancellationToken)
    {
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var prompt = await db.Prompts
            .Include(item => item.CategoryAssignments)
            .FirstOrDefaultAsync(item => item.Id == promptId && item.HouseholdId == currentMember.HouseholdId, cancellationToken)
            ?? throw new NotFoundException("Prompt não encontrado.");

        EnsureCanManageEntity(currentMember, prompt.CreatedByMemberId, "Você não pode editar um prompt criado por outra pessoa.");

        var normalizedCategoryIds = await ValidatePromptPayloadAsync(
            currentMember.HouseholdId,
            request.UniverseId,
            request.Title,
            request.Description,
            request.PromptText,
            request.CategoryIds,
            request.LinkUrl,
            request.LinkTitle,
            cancellationToken);

        prompt.UniverseId = request.UniverseId;
        prompt.Title = RequiredText(request.Title, "Informe o título do prompt.");
        prompt.Description = NormalizeOptional(request.Description);
        prompt.PromptText = RequiredText(request.PromptText, "Informe o texto do prompt.");
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

        var updated = await FindPromptForOutputAsync(currentMember.HouseholdId, prompt.Id, cancellationToken);
        return ToPromptDetailDto(updated, currentMember);
    }

    public async Task DeletePromptAsync(Guid promptId, CancellationToken cancellationToken)
    {
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var prompt = await db.Prompts
            .FirstOrDefaultAsync(item => item.Id == promptId && item.HouseholdId == currentMember.HouseholdId, cancellationToken)
            ?? throw new NotFoundException("Prompt não encontrado.");

        EnsureCanManageEntity(currentMember, prompt.CreatedByMemberId, "Você não pode excluir um prompt criado por outra pessoa.");

        if (!string.IsNullOrWhiteSpace(prompt.ImageObjectKey))
        {
            await objectStorage.DeleteAsync(prompt.ImageObjectKey, cancellationToken);
        }

        db.Prompts.Remove(prompt);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<PromptDetailDto> UploadPromptImageAsync(
        Guid promptId,
        Stream content,
        long contentLength,
        string? contentType,
        CancellationToken cancellationToken)
    {
        if (contentLength <= 0)
        {
            throw new ValidationException("Envie uma imagem com conteúdo para o prompt.");
        }

        if (contentLength > PromptImageMaxBytes)
        {
            throw new ValidationException($"A imagem do prompt deve ter no máximo {FormatMegabytes(PromptImageMaxBytes)} MB.");
        }

        var normalizedContentType = NormalizePromptImageContentType(contentType);
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var prompt = await db.Prompts
            .Include(item => item.CategoryAssignments)
                .ThenInclude(assignment => assignment.Category)
            .Include(item => item.Universe)
            .FirstOrDefaultAsync(item => item.Id == promptId && item.HouseholdId == currentMember.HouseholdId, cancellationToken)
            ?? throw new NotFoundException("Prompt não encontrado.");

        EnsureCanManageEntity(currentMember, prompt.CreatedByMemberId, "Você não pode editar um prompt criado por outra pessoa.");

        var objectKey = ObjectStorageKeys.PromptImage(prompt.Id);
        await objectStorage.PutAsync(
            new ObjectStoragePutRequest(objectKey, content, contentLength, normalizedContentType),
            cancellationToken);

        prompt.ImageObjectKey = objectKey;
        prompt.ImageContentType = normalizedContentType;
        prompt.ImageUpdatedAt = timeProvider.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);

        return ToPromptDetailDto(prompt, currentMember);
    }

    public async Task<StoredObject> GetPromptImageAsync(Guid promptId, CancellationToken cancellationToken)
    {
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var prompt = await db.Prompts
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == promptId && item.HouseholdId == currentMember.HouseholdId, cancellationToken)
            ?? throw new NotFoundException("Prompt não encontrado.");

        if (string.IsNullOrWhiteSpace(prompt.ImageObjectKey))
        {
            throw new NotFoundException("Imagem do prompt não encontrada.");
        }

        return await objectStorage.GetAsync(prompt.ImageObjectKey, cancellationToken);
    }

    public async Task<PromptDetailDto> DeletePromptImageAsync(Guid promptId, CancellationToken cancellationToken)
    {
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var prompt = await db.Prompts
            .Include(item => item.CategoryAssignments)
                .ThenInclude(assignment => assignment.Category)
            .Include(item => item.Universe)
            .FirstOrDefaultAsync(item => item.Id == promptId && item.HouseholdId == currentMember.HouseholdId, cancellationToken)
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

        return ToPromptDetailDto(prompt, currentMember);
    }

    public async Task<IReadOnlyCollection<PromptCategoryDto>> ListCategoriesAsync(CancellationToken cancellationToken)
    {
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var isManager = IsContentManager(currentMember);
        return await db.PromptCategories
            .AsNoTracking()
            .Where(category => category.HouseholdId == currentMember.HouseholdId)
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
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var category = new PromptCategory
        {
            HouseholdId = currentMember.HouseholdId,
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
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var category = await db.PromptCategories
            .Include(item => item.PromptAssignments)
                .ThenInclude(assignment => assignment.Prompt)
                    .ThenInclude(prompt => prompt!.CategoryAssignments)
            .FirstOrDefaultAsync(item => item.Id == categoryId && item.HouseholdId == currentMember.HouseholdId, cancellationToken)
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
        var currentMember = await ResolveCurrentMemberAsync(cancellationToken);
        var category = await db.PromptCategories
            .Include(item => item.PromptAssignments)
                .ThenInclude(assignment => assignment.Prompt)
                    .ThenInclude(prompt => prompt!.CategoryAssignments)
            .FirstOrDefaultAsync(item => item.Id == categoryId && item.HouseholdId == currentMember.HouseholdId, cancellationToken)
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
                .FirstOrDefaultAsync(item => item.Id == replacementCategoryId.Value && item.HouseholdId == currentMember.HouseholdId, cancellationToken)
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

    private async Task<Prompt> FindPromptForOutputAsync(Guid householdId, Guid promptId, CancellationToken cancellationToken)
    {
        return await db.Prompts
            .AsNoTracking()
            .Include(prompt => prompt.Universe)
            .Include(prompt => prompt.CategoryAssignments)
                .ThenInclude(assignment => assignment.Category)
            .FirstOrDefaultAsync(prompt => prompt.Id == promptId && prompt.HouseholdId == householdId, cancellationToken)
            ?? throw new NotFoundException("Prompt não encontrado.");
    }

    private async Task<IReadOnlyCollection<Guid>> ValidatePromptPayloadAsync(
        Guid householdId,
        Guid? universeId,
        string title,
        string? description,
        string promptText,
        IReadOnlyCollection<Guid> categoryIds,
        string? linkUrl,
        string? linkTitle,
        CancellationToken cancellationToken)
    {
        RequiredText(title, "Informe o título do prompt.");
        RequiredText(promptText, "Informe o texto do prompt.");
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

        if (universeId.HasValue)
        {
            var universeExists = await db.Universes
                .AnyAsync(universe => universe.Id == universeId.Value && universe.HouseholdId == householdId, cancellationToken);

            if (!universeExists)
            {
                throw new ValidationException("Universo inválido para esta casa.");
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
            .Where(category => category.HouseholdId == householdId && normalizedCategoryIds.Contains(category.Id))
            .Select(category => category.Id)
            .ToArrayAsync(cancellationToken);

        if (validCategoryIds.Length != normalizedCategoryIds.Length)
        {
            throw new ValidationException("Selecione apenas categorias válidas da casa ativa.");
        }

        return normalizedCategoryIds;
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

    private static PromptListItemDto ToPromptListItemDto(Prompt prompt, HouseholdMember currentMember)
    {
        var canManage = CanManageEntity(currentMember, prompt.CreatedByMemberId);
        return new PromptListItemDto(
            prompt.Id,
            prompt.UniverseId,
            prompt.Universe?.Name,
            prompt.Universe?.ImageUrl,
            !string.IsNullOrWhiteSpace(prompt.Universe?.ImageObjectKey),
            prompt.Universe?.ImageUpdatedAt,
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
            !string.IsNullOrWhiteSpace(prompt.ImageObjectKey),
            prompt.ImageUpdatedAt,
            prompt.UpdatedAt,
            canManage,
            canManage);
    }

    private static PromptDetailDto ToPromptDetailDto(Prompt prompt, HouseholdMember currentMember)
    {
        var canManage = CanManageEntity(currentMember, prompt.CreatedByMemberId);
        return new PromptDetailDto(
            prompt.Id,
            prompt.UniverseId,
            prompt.Universe?.Name,
            prompt.Universe?.ImageUrl,
            !string.IsNullOrWhiteSpace(prompt.Universe?.ImageObjectKey),
            prompt.Universe?.ImageUpdatedAt,
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

    private static string NormalizePromptImageContentType(string? contentType)
    {
        var normalized = NormalizeOptional(contentType);
        if (normalized is null || !AllowedPromptImageContentTypes.Contains(normalized))
        {
            throw new ValidationException("A imagem do prompt deve estar em JPG, PNG ou WEBP.");
        }

        return normalized;
    }

    private static long FormatMegabytes(long bytes)
    {
        return bytes / (1024 * 1024);
    }
}
