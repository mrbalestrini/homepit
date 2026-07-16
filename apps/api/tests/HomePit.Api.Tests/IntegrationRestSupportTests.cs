using HomePit.Api.Integrations;
using HomePit.Application.Common;
using HomePit.Domain.Finance;
using HomePit.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HomePit.Api.Tests;

public sealed class IntegrationRestSupportTests
{
    [Fact]
    public async Task Page_uses_opaque_cursor_and_preserves_versions()
    {
        await using var db = CreateDbContext();
        var householdId = Guid.NewGuid();
        var categories = Enumerable.Range(1, 3)
            .Select(index => new FinanceCategory { HouseholdId = householdId, Name = $"Categoria {index}", SortOrder = index })
            .ToArray();
        db.FinanceCategories.AddRange(categories);
        await db.SaveChangesAsync();

        var rest = new IntegrationRestSupport();
        var first = await rest.PageAsync(categories.Select(item => new Item(item.Id)).ToArray(), item => item.Id,
            db.FinanceCategories, "finance.categories", null, 2, CancellationToken.None);

        Assert.Equal(2, first.Items.Count);
        Assert.NotNull(first.NextCursor);
        Assert.All(first.Items, item => Assert.StartsWith("\"", item.Etag));

        var second = await rest.PageAsync(categories.Select(item => new Item(item.Id)).ToArray(), item => item.Id,
            db.FinanceCategories, "finance.categories", first.NextCursor, 2, CancellationToken.None);

        Assert.Single(second.Items);
        Assert.Null(second.NextCursor);
    }

    [Fact]
    public async Task Expected_version_rejects_missing_and_stale_etags()
    {
        await using var db = CreateDbContext();
        var category = new FinanceCategory { HouseholdId = Guid.NewGuid(), Name = "Automação", SortOrder = 1 };
        db.FinanceCategories.Add(category);
        await db.SaveChangesAsync();
        var rest = new IntegrationRestSupport();
        var resource = await rest.ResourceAsync(category, category.Id, db.FinanceCategories, CancellationToken.None);

        await Assert.ThrowsAsync<PreconditionRequiredException>(() => rest.ReadExpectedVersionAsync(category.Id, null, db.FinanceCategories, CancellationToken.None));

        category.Name = "Atualizada";
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<PreconditionFailedException>(() => rest.ReadExpectedVersionAsync(category.Id, resource.Etag, db.FinanceCategories, CancellationToken.None));
    }

    private static HomePitDbContext CreateDbContext() => new(new DbContextOptionsBuilder<HomePitDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
        .Options);

    private sealed record Item(Guid Id);
}
