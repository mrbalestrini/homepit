using HomePit.Application.Common;
using HomePit.Application.Prompts;
using HomePit.Application.Storage;
using HomePit.Domain.Households;
using HomePit.Domain.Prompts;
using HomePit.Domain.Projects;
using HomePit.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HomePit.Api.Tests;

public sealed class PromptServiceTests
{
    [Fact]
    public async Task Create_prompt_requires_at_least_one_category()
    {
        await using var context = CreateDbContext();
        var fixture = await SeedFixtureAsync(context);
        var service = CreateService(context, fixture.OwnerUserId, fixture.HouseholdId);

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            service.CreatePromptAsync(
                new CreatePromptRequest(
                    fixture.UniverseId,
                    "Prompt sem categoria",
                    null,
                    "Conteúdo do prompt",
                    Array.Empty<Guid>(),
                    null,
                    null),
                CancellationToken.None));

        Assert.Equal("Selecione pelo menos uma categoria.", exception.Message);
    }

    [Fact]
    public async Task Create_prompt_rejects_text_over_the_limit()
    {
        await using var context = CreateDbContext();
        var fixture = await SeedFixtureAsync(context);
        var service = CreateService(context, fixture.OwnerUserId, fixture.HouseholdId);

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            service.CreatePromptAsync(
                new CreatePromptRequest(
                    fixture.UniverseId,
                    "Prompt muito grande",
                    null,
                    new string('a', 20001),
                    [fixture.CategoryAId],
                    null,
                    null),
                CancellationToken.None));

        Assert.Equal("O texto do prompt deve ter no máximo 20000 caracteres.", exception.Message);
    }

    [Fact]
    public async Task Archive_and_unarchive_prompt_preserve_categories_and_image()
    {
        await using var context = CreateDbContext();
        var fixture = await SeedFixtureAsync(context);
        var service = CreateService(context, fixture.OwnerUserId, fixture.HouseholdId);

        var created = await service.CreatePromptAsync(
            new CreatePromptRequest(
                fixture.UniverseId,
                "Prompt arquivável",
                "Descrição",
                "Conteúdo do prompt",
                [fixture.CategoryAId, fixture.CategoryBId],
                null,
                null),
            CancellationToken.None);

        var prompt = await context.Prompts
            .Include(item => item.CategoryAssignments)
            .FirstAsync(item => item.Id == created.Id);

        prompt.ImageObjectKey = "prompts/prompt-1.png";
        prompt.ImageContentType = "image/png";
        prompt.ImageUpdatedAt = new DateTimeOffset(2026, 6, 24, 9, 30, 0, TimeSpan.Zero);
        await context.SaveChangesAsync();

        var archived = await service.ArchivePromptAsync(created.Id, CancellationToken.None);

        Assert.True(archived.IsArchived);
        Assert.True(archived.HasImage);
        Assert.Equal(2, archived.Categories.Count);
        Assert.Contains(archived.Categories, category => category.Id == fixture.CategoryAId);
        Assert.Contains(archived.Categories, category => category.Id == fixture.CategoryBId);

        var archivedEntity = await context.Prompts
            .Include(item => item.CategoryAssignments)
            .FirstAsync(item => item.Id == created.Id);

        Assert.True(archivedEntity.IsArchived);
        Assert.Equal("prompts/prompt-1.png", archivedEntity.ImageObjectKey);
        Assert.Equal("image/png", archivedEntity.ImageContentType);
        Assert.Equal(new DateTimeOffset(2026, 6, 24, 9, 30, 0, TimeSpan.Zero), archivedEntity.ImageUpdatedAt);
        Assert.Equal(2, archivedEntity.CategoryAssignments.Count);

        var unarchived = await service.UnarchivePromptAsync(created.Id, CancellationToken.None);

        Assert.False(unarchived.IsArchived);
        Assert.True(unarchived.HasImage);
        Assert.Equal(2, unarchived.Categories.Count);

        var unarchivedEntity = await context.Prompts
            .Include(item => item.CategoryAssignments)
            .FirstAsync(item => item.Id == created.Id);

        Assert.False(unarchivedEntity.IsArchived);
        Assert.Equal("prompts/prompt-1.png", unarchivedEntity.ImageObjectKey);
        Assert.Equal("image/png", unarchivedEntity.ImageContentType);
        Assert.Equal(2, unarchivedEntity.CategoryAssignments.Count);
    }

    [Fact]
    public async Task List_prompts_separates_active_and_archived_items()
    {
        await using var context = CreateDbContext();
        var fixture = await SeedFixtureAsync(context);
        var service = CreateService(context, fixture.OwnerUserId, fixture.HouseholdId);

        var active = await service.CreatePromptAsync(
            new CreatePromptRequest(
                fixture.UniverseId,
                "Prompt ativo",
                null,
                "Conteúdo ativo",
                [fixture.CategoryAId],
                null,
                null),
            CancellationToken.None);

        var archived = await service.CreatePromptAsync(
            new CreatePromptRequest(
                fixture.UniverseId,
                "Prompt arquivado",
                null,
                "Conteúdo arquivado",
                [fixture.CategoryBId],
                null,
                null),
            CancellationToken.None);

        await service.ArchivePromptAsync(archived.Id, CancellationToken.None);

        var activeList = await service.ListPromptsAsync(null, null, false, false, null, 1, 12, CancellationToken.None);
        var archivedList = await service.ListPromptsAsync(null, null, false, true, null, 1, 12, CancellationToken.None);

        Assert.Equal(1, activeList.TotalCount);
        Assert.Single(activeList.Items, item => item.Id == active.Id);
        Assert.DoesNotContain(activeList.Items, item => item.Id == archived.Id);
        Assert.All(activeList.Items, item => Assert.False(item.IsArchived));

        Assert.Equal(1, archivedList.TotalCount);
        Assert.Single(archivedList.Items, item => item.Id == archived.Id);
        Assert.DoesNotContain(archivedList.Items, item => item.Id == active.Id);
        Assert.All(archivedList.Items, item => Assert.True(item.IsArchived));
    }

    [Fact]
    public async Task Superadmin_cannot_archive_prompt()
    {
        await using var context = CreateDbContext();
        var service = CreateService(context, Guid.NewGuid(), null, SystemRole.SuperAdmin);

        var exception = await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.ArchivePromptAsync(Guid.NewGuid(), CancellationToken.None));

        Assert.Equal("O superadmin possui acesso somente leitura nesta etapa.", exception.Message);
    }

    [Fact]
    public async Task Create_prompt_requires_link_title_when_url_is_informed()
    {
        await using var context = CreateDbContext();
        var fixture = await SeedFixtureAsync(context);
        var service = CreateService(context, fixture.OwnerUserId, fixture.HouseholdId);

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            service.CreatePromptAsync(
                new CreatePromptRequest(
                    fixture.UniverseId,
                    "Prompt com link inválido",
                    null,
                    "Conteúdo do prompt",
                    [fixture.CategoryAId],
                    "https://homepit.dev/referencia",
                    null),
                CancellationToken.None));

        Assert.Equal("Informe título e URL do link juntos.", exception.Message);
    }

    [Fact]
    public async Task Delete_category_requires_replacement_when_prompt_would_be_left_without_category()
    {
        await using var context = CreateDbContext();
        var fixture = await SeedFixtureAsync(context);
        var service = CreateService(context, fixture.OwnerUserId, fixture.HouseholdId);

        var prompt = new Prompt
        {
            HouseholdId = fixture.HouseholdId,
            CreatedByMemberId = fixture.OwnerMemberId,
            UniverseId = fixture.UniverseId,
            Title = "Prompt único",
            PromptText = "Conteúdo"
        };
        prompt.CategoryAssignments.Add(new PromptCategoryAssignment
        {
            Prompt = prompt,
            CategoryId = fixture.CategoryAId
        });
        context.Prompts.Add(prompt);
        await context.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            service.DeleteCategoryAsync(fixture.CategoryAId, null, CancellationToken.None));

        Assert.Equal("Escolha uma categoria de substituição para prompts que ficariam sem categoria.", exception.Message);
    }

    [Fact]
    public async Task Delete_category_reassigns_prompts_when_replacement_is_informed()
    {
        await using var context = CreateDbContext();
        var fixture = await SeedFixtureAsync(context);
        var service = CreateService(context, fixture.OwnerUserId, fixture.HouseholdId);

        var prompt = new Prompt
        {
            HouseholdId = fixture.HouseholdId,
            CreatedByMemberId = fixture.OwnerMemberId,
            UniverseId = fixture.UniverseId,
            Title = "Prompt único",
            PromptText = "Conteúdo"
        };
        prompt.CategoryAssignments.Add(new PromptCategoryAssignment
        {
            Prompt = prompt,
            CategoryId = fixture.CategoryAId
        });
        context.Prompts.Add(prompt);
        await context.SaveChangesAsync();

        await service.DeleteCategoryAsync(fixture.CategoryAId, fixture.CategoryBId, CancellationToken.None);

        var updatedPrompt = await context.Prompts
            .Include(item => item.CategoryAssignments)
            .FirstAsync(item => item.Id == prompt.Id);

        Assert.DoesNotContain(updatedPrompt.CategoryAssignments, assignment => assignment.CategoryId == fixture.CategoryAId);
        Assert.Contains(updatedPrompt.CategoryAssignments, assignment => assignment.CategoryId == fixture.CategoryBId);
    }

    [Fact]
    public async Task Member_cannot_edit_prompt_created_by_another_member()
    {
        await using var context = CreateDbContext();
        var fixture = await SeedFixtureAsync(context);
        var ownerService = CreateService(context, fixture.OwnerUserId, fixture.HouseholdId);

        var created = await ownerService.CreatePromptAsync(
            new CreatePromptRequest(
                fixture.UniverseId,
                "Prompt do dono",
                "Descrição",
                "Conteúdo do prompt",
                [fixture.CategoryAId],
                null,
                null),
            CancellationToken.None);

        var memberService = CreateService(context, fixture.MemberUserId, fixture.HouseholdId);

        var exception = await Assert.ThrowsAsync<ForbiddenException>(() =>
            memberService.UpdatePromptAsync(
                created.Id,
                new UpdatePromptRequest(
                    fixture.UniverseId,
                    "Tentativa indevida",
                    "Descrição",
                    "Novo conteúdo",
                    [fixture.CategoryAId],
                    null,
                    null),
                CancellationToken.None));

        Assert.Equal("Você não pode editar um prompt criado por outra pessoa.", exception.Message);
    }

    private static HomePitDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<HomePitDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new HomePitDbContext(options);
    }

    private static PromptService CreateService(HomePitDbContext context, Guid userId, Guid? householdId, SystemRole systemRole = SystemRole.User)
    {
        return new PromptService(
            context,
            new TestUserContext(userId, householdId, systemRole),
            new InMemoryObjectStorage(),
            TimeProvider.System);
    }

    private static async Task<PromptFixture> SeedFixtureAsync(HomePitDbContext context)
    {
        var ownerUser = new AppUser
        {
            Email = "owner@homepit.dev",
            PasswordHash = "hash",
            DisplayName = "Owner",
            SystemRole = SystemRole.User
        };
        var memberUser = new AppUser
        {
            Email = "member@homepit.dev",
            PasswordHash = "hash",
            DisplayName = "Member",
            SystemRole = SystemRole.User
        };
        var household = new Household
        {
            Name = "Casa Teste"
        };
        var ownerMember = new HouseholdMember
        {
            Household = household,
            User = ownerUser,
            Role = HouseholdRole.Owner
        };
        var member = new HouseholdMember
        {
            Household = household,
            User = memberUser,
            Role = HouseholdRole.Member
        };
        var universe = new Universe
        {
            Household = household,
            CreatedByMember = ownerMember,
            Name = "Universo"
        };
        var categoryA = new PromptCategory
        {
            Household = household,
            CreatedByMember = ownerMember,
            Name = "Categoria A"
        };
        var categoryB = new PromptCategory
        {
            Household = household,
            CreatedByMember = ownerMember,
            Name = "Categoria B"
        };

        context.Users.AddRange(ownerUser, memberUser);
        context.Households.Add(household);
        context.HouseholdMembers.AddRange(ownerMember, member);
        context.Universes.Add(universe);
        context.PromptCategories.AddRange(categoryA, categoryB);
        await context.SaveChangesAsync();

        return new PromptFixture(
            household.Id,
            ownerUser.Id,
            ownerMember.Id,
            memberUser.Id,
            member.Id,
            universe.Id,
            categoryA.Id,
            categoryB.Id);
    }

    private sealed record PromptFixture(
        Guid HouseholdId,
        Guid OwnerUserId,
        Guid OwnerMemberId,
        Guid MemberUserId,
        Guid MemberId,
        Guid UniverseId,
        Guid CategoryAId,
        Guid CategoryBId);

    private sealed class TestUserContext(Guid userId, Guid? householdId, SystemRole systemRole) : IUserContext
    {
        public Guid UserId { get; } = userId;
        public SystemRole SystemRole { get; } = systemRole;
        public Guid? HouseholdId { get; } = householdId;
    }

    private sealed class InMemoryObjectStorage : IObjectStorage
    {
        public Task EnsureBucketExistsAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task PutAsync(ObjectStoragePutRequest request, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<StoredObject> GetAsync(string objectKey, CancellationToken cancellationToken) =>
            throw new NotFoundException("Arquivo não encontrado.");

        public Task DeleteAsync(string objectKey, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
