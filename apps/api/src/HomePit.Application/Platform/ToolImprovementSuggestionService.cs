using HomePit.Application.Common;
using HomePit.Domain.Households;
using HomePit.Domain.Platform;
using Microsoft.EntityFrameworkCore;

namespace HomePit.Application.Platform;

public sealed class ToolImprovementSuggestionService(
    IHomePitDbContext db,
    IUserContext userContext,
    TimeProvider timeProvider)
{
    private const int SuggestionTextMaxLength = 8000;
    private const int InternalCommentMaxLength = 4000;

    public async Task<ToolImprovementSuggestionDto> SubmitAsync(
        CreateToolImprovementSuggestionRequest request,
        CancellationToken cancellationToken)
    {
        var suggestion = new ToolImprovementSuggestion
        {
            UserId = userContext.UserId,
            SubmittedAt = timeProvider.GetUtcNow(),
            SuggestionText = NormalizeRequiredText(request.SuggestionText, SuggestionTextMaxLength, "Descreva a sugestão de melhoria."),
            Status = ToolImprovementSuggestionStatus.NaoLido,
            Priority = ToolImprovementSuggestionPriority.Media,
            InternalComment = null,
            LastReviewedAt = null,
            LastReviewedByUserId = null
        };

        db.ToolImprovementSuggestions.Add(suggestion);
        await db.SaveChangesAsync(cancellationToken);

        return await FindDtoAsync(suggestion.Id, cancellationToken);
    }

    public async Task<IReadOnlyCollection<ToolImprovementSuggestionDto>> ListAdminAsync(CancellationToken cancellationToken)
    {
        EnsureSuperAdmin();

        return await db.ToolImprovementSuggestions
            .AsNoTracking()
            .Include(item => item.User)
            .Include(item => item.LastReviewedByUser)
            .OrderByDescending(item => item.SubmittedAt)
            .ThenByDescending(item => item.CreatedAt)
            .Select(item => ToDto(item))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<ToolImprovementSuggestionDto> UpdateAsync(
        Guid id,
        UpdateToolImprovementSuggestionRequest request,
        CancellationToken cancellationToken)
    {
        EnsureSuperAdmin();

        var suggestion = await db.ToolImprovementSuggestions
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new NotFoundException("Sugestão não encontrada.");

        var normalizedComment = NormalizeOptionalText(request.InternalComment, InternalCommentMaxLength);
        var hasChanges =
            suggestion.Status != request.Status ||
            suggestion.Priority != request.Priority ||
            suggestion.InternalComment != normalizedComment;

        suggestion.Status = request.Status;
        suggestion.Priority = request.Priority;
        suggestion.InternalComment = normalizedComment;

        if (hasChanges)
        {
            suggestion.LastReviewedAt = timeProvider.GetUtcNow();
            suggestion.LastReviewedByUserId = userContext.UserId;
        }

        await db.SaveChangesAsync(cancellationToken);
        return await FindDtoAsync(suggestion.Id, cancellationToken);
    }

    public async Task<IReadOnlyCollection<ToolImprovementSuggestionDto>> BulkUpdateAsync(
        BulkUpdateToolImprovementSuggestionsRequest request,
        CancellationToken cancellationToken)
    {
        EnsureSuperAdmin();

        var suggestionIds = request.SuggestionIds
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToArray();

        if (suggestionIds.Length == 0)
        {
            throw new ValidationException("Selecione ao menos uma sugestão para atualizar.");
        }

        if (request.Status is null && request.Priority is null)
        {
            throw new ValidationException("Informe pelo menos um campo para atualização em massa.");
        }

        var suggestions = await db.ToolImprovementSuggestions
            .Where(item => suggestionIds.Contains(item.Id))
            .ToArrayAsync(cancellationToken);

        if (suggestions.Length != suggestionIds.Length)
        {
            throw new NotFoundException("Uma ou mais sugestões não foram encontradas.");
        }

        var reviewedAt = timeProvider.GetUtcNow();
        foreach (var suggestion in suggestions)
        {
            var hasChanges = false;

            if (request.Status is not null && suggestion.Status != request.Status.Value)
            {
                suggestion.Status = request.Status.Value;
                hasChanges = true;
            }

            if (request.Priority is not null && suggestion.Priority != request.Priority.Value)
            {
                suggestion.Priority = request.Priority.Value;
                hasChanges = true;
            }

            if (hasChanges)
            {
                suggestion.LastReviewedAt = reviewedAt;
                suggestion.LastReviewedByUserId = userContext.UserId;
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        return await db.ToolImprovementSuggestions
            .AsNoTracking()
            .Include(item => item.User)
            .Include(item => item.LastReviewedByUser)
            .Where(item => suggestionIds.Contains(item.Id))
            .OrderByDescending(item => item.SubmittedAt)
            .ThenByDescending(item => item.CreatedAt)
            .Select(item => ToDto(item))
            .ToArrayAsync(cancellationToken);
    }

    private async Task<ToolImprovementSuggestionDto> FindDtoAsync(Guid id, CancellationToken cancellationToken)
    {
        var suggestion = await db.ToolImprovementSuggestions
            .AsNoTracking()
            .Include(item => item.User)
            .Include(item => item.LastReviewedByUser)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new NotFoundException("Sugestão não encontrada.");

        return ToDto(suggestion);
    }

    private void EnsureSuperAdmin()
    {
        if (userContext.SystemRole != SystemRole.SuperAdmin)
        {
            throw new ForbiddenException("Somente o superadmin pode gerenciar as sugestões de melhoria.");
        }
    }

    private static ToolImprovementSuggestionDto ToDto(ToolImprovementSuggestion item)
    {
        return new ToolImprovementSuggestionDto(
            item.Id,
            item.UserId,
            item.User?.DisplayName ?? string.Empty,
            item.User?.Email ?? string.Empty,
            item.SubmittedAt,
            item.SuggestionText,
            item.Status,
            item.Priority,
            item.InternalComment,
            item.LastReviewedAt,
            item.LastReviewedByUserId,
            item.LastReviewedByUser?.DisplayName);
    }

    private static string NormalizeRequiredText(string? value, int maxLength, string emptyMessage)
    {
        var normalized = NormalizeOptionalText(value, maxLength);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ValidationException(emptyMessage);
        }

        return normalized;
    }

    private static string? NormalizeOptionalText(string? value, int maxLength)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        if (normalized is not null && normalized.Length > maxLength)
        {
            throw new ValidationException($"O texto deve ter no máximo {maxLength} caracteres.");
        }

        return normalized;
    }
}
