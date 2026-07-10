using HomePit.Application.Common;
using HomePit.Application.Platform;
using HomePit.Domain.Households;
using HomePit.Domain.Platform;
using HomePit.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HomePit.Api.Tests;

public sealed class ToolImprovementSuggestionServiceTests
{
    [Fact]
    public async Task Submit_records_author_submission_date_and_default_triage()
    {
        await using var db = CreateDbContext();
        var user = new AppUser
        {
            Email = "user@homepit.dev",
            PasswordHash = "hash",
            DisplayName = "Usuário"
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var service = new ToolImprovementSuggestionService(
            db,
            new FakeUserContext(user.Id, SystemRole.User),
            new FakeTimeProvider(DateTimeOffset.Parse("2026-07-10T12:00:00Z")));

        var result = await service.SubmitAsync(
            new CreateToolImprovementSuggestionRequest("Melhorar o filtro do módulo financeiro."),
            CancellationToken.None);

        var saved = await db.ToolImprovementSuggestions.SingleAsync();
        Assert.Equal(user.Id, saved.UserId);
        Assert.Equal(DateTimeOffset.Parse("2026-07-10T12:00:00Z"), saved.SubmittedAt);
        Assert.Equal(ToolImprovementSuggestionStatus.NaoLido, saved.Status);
        Assert.Equal(ToolImprovementSuggestionPriority.Media, saved.Priority);
        Assert.Equal("Usuário", result.UserDisplayName);
        Assert.Equal("user@homepit.dev", result.UserEmail);
    }

    [Theory]
    [InlineData(SystemRole.User)]
    [InlineData(SystemRole.Admin)]
    public async Task Non_superadmin_cannot_manage_suggestions(SystemRole role)
    {
        await using var db = CreateDbContext();
        var service = new ToolImprovementSuggestionService(
            db,
            new FakeUserContext(Guid.NewGuid(), role),
            new FakeTimeProvider(DateTimeOffset.Parse("2026-07-10T12:00:00Z")));

        await Assert.ThrowsAsync<ForbiddenException>(() => service.ListAdminAsync(CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.UpdateAsync(
                Guid.NewGuid(),
                new UpdateToolImprovementSuggestionRequest(
                    ToolImprovementSuggestionStatus.EmExecucao,
                    ToolImprovementSuggestionPriority.Alta,
                    "Comentário"),
                CancellationToken.None));
    }

    [Fact]
    public async Task Superadmin_can_update_individual_and_bulk_triage_fields()
    {
        await using var db = CreateDbContext();
        var author = new AppUser
        {
            Email = "author@homepit.dev",
            PasswordHash = "hash",
            DisplayName = "Autor"
        };
        var reviewer = new AppUser
        {
            Email = "superadmin@homepit.dev",
            PasswordHash = "hash",
            DisplayName = "SuperAdmin",
            SystemRole = SystemRole.SuperAdmin
        };
        var first = new ToolImprovementSuggestion
        {
            User = author,
            SubmittedAt = DateTimeOffset.Parse("2026-07-10T09:00:00Z"),
            SuggestionText = "Melhorar a aba de projetos."
        };
        var second = new ToolImprovementSuggestion
        {
            User = author,
            SubmittedAt = DateTimeOffset.Parse("2026-07-10T10:00:00Z"),
            SuggestionText = "Adicionar resumo no perfil."
        };

        db.Users.AddRange(author, reviewer);
        db.ToolImprovementSuggestions.AddRange(first, second);
        await db.SaveChangesAsync();

        var service = new ToolImprovementSuggestionService(
            db,
            new FakeUserContext(reviewer.Id, SystemRole.SuperAdmin),
            new FakeTimeProvider(DateTimeOffset.Parse("2026-07-10T15:00:00Z")));

        var updated = await service.UpdateAsync(
            first.Id,
            new UpdateToolImprovementSuggestionRequest(
                ToolImprovementSuggestionStatus.EmExecucao,
                ToolImprovementSuggestionPriority.Alta,
                "Validar com o time de projetos."),
            CancellationToken.None);

        var bulkUpdated = await service.BulkUpdateAsync(
            new BulkUpdateToolImprovementSuggestionsRequest(
                [first.Id, second.Id],
                ToolImprovementSuggestionStatus.Feito,
                ToolImprovementSuggestionPriority.Urgente),
            CancellationToken.None);

        Assert.Equal(ToolImprovementSuggestionStatus.EmExecucao, updated.Status);
        Assert.Equal(ToolImprovementSuggestionPriority.Alta, updated.Priority);
        Assert.Equal("Validar com o time de projetos.", updated.InternalComment);
        Assert.Equal(reviewer.Id, updated.LastReviewedByUserId);
        Assert.Equal(2, bulkUpdated.Count);

        var saved = await db.ToolImprovementSuggestions.OrderBy(item => item.SubmittedAt).ToArrayAsync();
        Assert.All(saved, item => Assert.Equal(ToolImprovementSuggestionStatus.Feito, item.Status));
        Assert.All(saved, item => Assert.Equal(ToolImprovementSuggestionPriority.Urgente, item.Priority));
        Assert.All(saved, item => Assert.Equal(reviewer.Id, item.LastReviewedByUserId));
    }

    private static HomePitDbContext CreateDbContext()
    {
        return new HomePitDbContext(
            new DbContextOptionsBuilder<HomePitDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options);
    }

    private sealed class FakeUserContext(Guid userId, SystemRole role) : IUserContext
    {
        public Guid UserId { get; } = userId;
        public SystemRole SystemRole { get; } = role;
        public Guid? HouseholdId => null;
    }

    private sealed class FakeTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
