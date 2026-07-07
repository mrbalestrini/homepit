using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using HomePit.Application.Auth;
using HomePit.Application.Common;
using HomePit.Application.Storage;
using HomePit.Domain.Finance;
using HomePit.Domain.Households;
using HomePit.Domain.Projects;
using HomePit.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace HomePit.IntegrationTests;

public sealed class FinanceEndpointsTests
{
    [Fact]
    public async Task Finance_entry_and_recurring_template_crud_works_with_household_header()
    {
        await using var factory = new HomePitApiFactory();
        using var client = factory.CreateClient();
        var seed = await SeedAsync(factory);

        var createCategoryResponse = await SendAuthorizedAsync(
            client,
            seed.OwnerAccessToken,
            seed.HouseholdId,
            HttpMethod.Post,
            "/api/finance/categories",
            JsonContent.Create(new { name = "Pets" }));

        Assert.Equal(HttpStatusCode.Created, createCategoryResponse.StatusCode);
        var createdCategory = await createCategoryResponse.Content.ReadFromJsonAsync<FinanceCategoryResponse>(JsonSerializerOptions.Web);
        Assert.NotNull(createdCategory);

        var createTemplateResponse = await SendAuthorizedAsync(
            client,
            seed.OwnerAccessToken,
            seed.HouseholdId,
            HttpMethod.Post,
            "/api/finance/recurring-templates",
            JsonContent.Create(new
            {
                title = "Condominio",
                notes = "Vencimento dia 10",
                type = "Saida",
                defaultAmount = 776.5m,
                recurrence = "Monthly",
                dayOfMonth = 10,
                monthOfYear = (int?)null,
                isActive = true,
                categoryId = createdCategory!.Id,
                universeId = (Guid?)null,
                projectId = seed.ProjectId
            }));

        Assert.Equal(HttpStatusCode.Created, createTemplateResponse.StatusCode);
        var createdTemplate = await createTemplateResponse.Content.ReadFromJsonAsync<FinanceRecurringTemplateResponse>(JsonSerializerOptions.Web);
        Assert.NotNull(createdTemplate);
        Assert.Equal(createdCategory!.Id, createdTemplate!.CategoryId);
        Assert.Equal(seed.ProjectId, createdTemplate!.ProjectId);
        Assert.Equal(seed.UniverseId, createdTemplate.UniverseId);

        var generateResponse = await SendAuthorizedAsync(
            client,
            seed.OwnerAccessToken,
            seed.HouseholdId,
            HttpMethod.Post,
            "/api/finance/periods/2026/7/generate",
            JsonContent.Create(new { mode = "missingOnly" }));

        generateResponse.EnsureSuccessStatusCode();
        var generatedPeriod = await generateResponse.Content.ReadFromJsonAsync<FinancePeriodDetailResponse>(JsonSerializerOptions.Web);
        Assert.NotNull(generatedPeriod);
        Assert.True(generatedPeriod!.Exists);
        Assert.Contains(generatedPeriod.Entries, entry => entry.RecurringTemplateId == createdTemplate.Id);

        var createEntryResponse = await SendAuthorizedAsync(
            client,
            seed.OwnerAccessToken,
            seed.HouseholdId,
            HttpMethod.Post,
            "/api/finance/entries",
            JsonContent.Create(new
            {
                year = 2026,
                month = 7,
                title = "Outros gastos",
                notes = "Compra eventual",
                amount = 95.35m,
                type = "Saida",
                verified = false,
                referenceDate = new DateOnly(2026, 7, 6),
                recurringTemplateId = (Guid?)null,
                categoryId = createdCategory.Id,
                universeId = seed.UniverseId,
                projectId = seed.ProjectId
            }));

        Assert.Equal(HttpStatusCode.Created, createEntryResponse.StatusCode);
        var createdEntry = await createEntryResponse.Content.ReadFromJsonAsync<FinanceEntryResponse>(JsonSerializerOptions.Web);
        Assert.NotNull(createdEntry);
        Assert.Equal("Manual", createdEntry!.Origin);
        Assert.Equal(createdCategory.Id, createdEntry.CategoryId);
        Assert.Equal("Pets", createdEntry.CategoryName);

        var updateEntryResponse = await SendAuthorizedAsync(
            client,
            seed.OwnerAccessToken,
            seed.HouseholdId,
            HttpMethod.Put,
            $"/api/finance/entries/{createdEntry.Id}",
            JsonContent.Create(new
            {
                year = 2026,
                month = 7,
                title = "Outros gastos",
                notes = "Compra validada",
                amount = 95.35m,
                type = "Saida",
                verified = true,
                referenceDate = new DateOnly(2026, 7, 6),
                recurringTemplateId = (Guid?)null,
                categoryId = createdCategory.Id,
                universeId = seed.UniverseId,
                projectId = seed.ProjectId
            }));

        updateEntryResponse.EnsureSuccessStatusCode();
        var updatedEntry = await updateEntryResponse.Content.ReadFromJsonAsync<FinanceEntryResponse>(JsonSerializerOptions.Web);
        Assert.NotNull(updatedEntry);
        Assert.True(updatedEntry!.Verified);

        var listEntriesResponse = await SendAuthorizedAsync(
            client,
            seed.OwnerAccessToken,
            seed.HouseholdId,
            HttpMethod.Get,
            "/api/finance/entries?year=2026&month=7");

        listEntriesResponse.EnsureSuccessStatusCode();
        var listedEntries = await listEntriesResponse.Content.ReadFromJsonAsync<IReadOnlyCollection<FinanceEntryResponse>>(JsonSerializerOptions.Web);
        Assert.NotNull(listedEntries);
        Assert.Equal(2, listedEntries!.Count);

        var deleteEntryResponse = await SendAuthorizedAsync(
            client,
            seed.OwnerAccessToken,
            seed.HouseholdId,
            HttpMethod.Delete,
            $"/api/finance/entries/{createdEntry.Id}");

        Assert.Equal(HttpStatusCode.NoContent, deleteEntryResponse.StatusCode);

        var deleteTemplateResponse = await SendAuthorizedAsync(
            client,
            seed.OwnerAccessToken,
            seed.HouseholdId,
            HttpMethod.Delete,
            $"/api/finance/recurring-templates/{createdTemplate.Id}");

        Assert.Equal(HttpStatusCode.NoContent, deleteTemplateResponse.StatusCode);
    }

    [Fact]
    public async Task Finance_categories_crud_and_deletion_unlink_records()
    {
        await using var factory = new HomePitApiFactory();
        using var client = factory.CreateClient();
        var seed = await SeedAsync(factory);

        var listDefaultsResponse = await SendAuthorizedAsync(
            client,
            seed.OwnerAccessToken,
            seed.HouseholdId,
            HttpMethod.Get,
            "/api/finance/categories");

        listDefaultsResponse.EnsureSuccessStatusCode();
        var defaultCategories = await listDefaultsResponse.Content.ReadFromJsonAsync<IReadOnlyCollection<FinanceCategoryResponse>>(JsonSerializerOptions.Web);
        Assert.NotNull(defaultCategories);
        Assert.Equal(FinanceCategoryCatalog.DefaultNames.Count, defaultCategories!.Count);
        Assert.Equal("Salário", defaultCategories.First().Name);
        Assert.All(defaultCategories.Take(FinanceCategoryCatalog.DefaultNames.Count), category => Assert.True(category.IsDefault));

        var createCategoryResponse = await SendAuthorizedAsync(
            client,
            seed.OwnerAccessToken,
            seed.HouseholdId,
            HttpMethod.Post,
            "/api/finance/categories",
            JsonContent.Create(new { name = "Pets" }));

        Assert.Equal(HttpStatusCode.Created, createCategoryResponse.StatusCode);
        var createdCategory = await createCategoryResponse.Content.ReadFromJsonAsync<FinanceCategoryResponse>(JsonSerializerOptions.Web);
        Assert.NotNull(createdCategory);

        var updateCategoryResponse = await SendAuthorizedAsync(
            client,
            seed.OwnerAccessToken,
            seed.HouseholdId,
            HttpMethod.Put,
            $"/api/finance/categories/{createdCategory!.Id}",
            JsonContent.Create(new { name = "Assinaturas" }));

        updateCategoryResponse.EnsureSuccessStatusCode();
        var updatedCategory = await updateCategoryResponse.Content.ReadFromJsonAsync<FinanceCategoryResponse>(JsonSerializerOptions.Web);
        Assert.NotNull(updatedCategory);
        Assert.Equal("Assinaturas", updatedCategory!.Name);

        var createEntryResponse = await SendAuthorizedAsync(
            client,
            seed.OwnerAccessToken,
            seed.HouseholdId,
            HttpMethod.Post,
            "/api/finance/entries",
            JsonContent.Create(new
            {
                year = 2026,
                month = 7,
                title = "Streaming",
                notes = "Categoria customizada",
                amount = 39.9m,
                type = "Saida",
                verified = false,
                referenceDate = new DateOnly(2026, 7, 6),
                recurringTemplateId = (Guid?)null,
                categoryId = updatedCategory.Id,
                universeId = (Guid?)null,
                projectId = (Guid?)null
            }));

        createEntryResponse.EnsureSuccessStatusCode();
        var createdEntry = await createEntryResponse.Content.ReadFromJsonAsync<FinanceEntryResponse>(JsonSerializerOptions.Web);
        Assert.NotNull(createdEntry);
        Assert.Equal(updatedCategory.Id, createdEntry!.CategoryId);

        var deleteCategoryResponse = await SendAuthorizedAsync(
            client,
            seed.OwnerAccessToken,
            seed.HouseholdId,
            HttpMethod.Delete,
            $"/api/finance/categories/{updatedCategory.Id}");

        Assert.Equal(HttpStatusCode.NoContent, deleteCategoryResponse.StatusCode);

        var listEntriesResponse = await SendAuthorizedAsync(
            client,
            seed.OwnerAccessToken,
            seed.HouseholdId,
            HttpMethod.Get,
            "/api/finance/entries?year=2026&month=7");

        listEntriesResponse.EnsureSuccessStatusCode();
        var listedEntries = await listEntriesResponse.Content.ReadFromJsonAsync<IReadOnlyCollection<FinanceEntryResponse>>(JsonSerializerOptions.Web);
        Assert.NotNull(listedEntries);
        Assert.Contains(listedEntries!, entry => entry.Id == createdEntry.Id && entry.CategoryId is null && entry.CategoryName is null);
    }

    [Fact]
    public async Task Finance_asset_and_credit_card_crud_keeps_statement_in_sync_with_cash_period()
    {
        await using var factory = new HomePitApiFactory();
        using var client = factory.CreateClient();
        var seed = await SeedAsync(factory);

        var createAssetResponse = await SendAuthorizedAsync(
            client,
            seed.OwnerAccessToken,
            seed.HouseholdId,
            HttpMethod.Post,
            "/api/finance/assets",
            JsonContent.Create(new
            {
                title = "Blue Moon Apto 405",
                type = "Property",
                currentValue = 480000m,
                remainingDebt = 55474.71m,
                isPaidOff = false,
                notes = "Financiamento em andamento",
                propertyDetails = new
                {
                    registryNumber = "282.144",
                    propertyInscription = "50760572",
                    privateAreaSquareMeters = 55.61m,
                    debtCheckOn = new DateOnly(2023, 12, 29)
                },
                vehicleDetails = (object?)null
            }));

        Assert.Equal(HttpStatusCode.Created, createAssetResponse.StatusCode);
        var createdAsset = await createAssetResponse.Content.ReadFromJsonAsync<AssetResponse>(JsonSerializerOptions.Web);
        Assert.NotNull(createdAsset);
        Assert.Equal("Property", createdAsset!.Type);

        var createValuationResponse = await SendAuthorizedAsync(
            client,
            seed.OwnerAccessToken,
            seed.HouseholdId,
            HttpMethod.Post,
            $"/api/finance/assets/{createdAsset.Id}/valuations",
            JsonContent.Create(new
            {
                referenceYear = 2026,
                label = "FIPE 2026",
                amount = 495000m,
                notes = "Revisao anual"
            }));

        Assert.Equal(HttpStatusCode.Created, createValuationResponse.StatusCode);
        var createdValuation = await createValuationResponse.Content.ReadFromJsonAsync<AssetValuationResponse>(JsonSerializerOptions.Web);
        Assert.NotNull(createdValuation);

        var createCardResponse = await SendAuthorizedAsync(
            client,
            seed.OwnerAccessToken,
            seed.HouseholdId,
            HttpMethod.Post,
            "/api/finance/credit-cards",
            JsonContent.Create(new
            {
                name = "Nubank",
                brand = "Mastercard",
                lastFourDigits = "1234",
                closingDay = 20,
                dueDay = 25,
                notes = "Cartao principal",
                isActive = true
            }));

        Assert.Equal(HttpStatusCode.Created, createCardResponse.StatusCode);
        var createdCard = await createCardResponse.Content.ReadFromJsonAsync<CreditCardAccountResponse>(JsonSerializerOptions.Web);
        Assert.NotNull(createdCard);

        var createTransactionResponse = await SendAuthorizedAsync(
            client,
            seed.OwnerAccessToken,
            seed.HouseholdId,
            HttpMethod.Post,
            $"/api/finance/credit-cards/{createdCard!.Id}/transactions",
            JsonContent.Create(new
            {
                title = "Supermercado",
                merchant = "Mercado da esquina",
                amount = 220.9m,
                purchasedOn = new DateOnly(2026, 7, 6),
                notes = "Compra mensal",
                categoryId = seed.DefaultCategoryId,
                universeId = seed.UniverseId,
                projectId = seed.ProjectId,
                externalSource = "SMS",
                externalReference = "sms-001",
                importedAt = (DateTimeOffset?)null
            }));

        Assert.Equal(HttpStatusCode.Created, createTransactionResponse.StatusCode);
        var createdTransaction = await createTransactionResponse.Content.ReadFromJsonAsync<CreditCardTransactionResponse>(JsonSerializerOptions.Web);
        Assert.NotNull(createdTransaction);
        Assert.Equal(seed.DefaultCategoryId, createdTransaction!.CategoryId);

        var createStatementResponse = await SendAuthorizedAsync(
            client,
            seed.OwnerAccessToken,
            seed.HouseholdId,
            HttpMethod.Post,
            $"/api/finance/credit-cards/{createdCard.Id}/statements",
            JsonContent.Create(new
            {
                closingDate = new DateOnly(2026, 7, 20),
                dueDate = new DateOnly(2026, 7, 25),
                notes = "Fatura de julho",
                transactionIds = new[] { createdTransaction!.Id },
                externalSource = "XLS",
                externalReference = "fatura-2026-07",
                importedAt = (DateTimeOffset?)null
            }));

        Assert.Equal(HttpStatusCode.Created, createStatementResponse.StatusCode);
        var createdStatement = await createStatementResponse.Content.ReadFromJsonAsync<CreditCardStatementResponse>(JsonSerializerOptions.Web);
        Assert.NotNull(createdStatement);
        Assert.Equal(220.9m, createdStatement!.TotalAmount);

        var updateStatementResponse = await SendAuthorizedAsync(
            client,
            seed.OwnerAccessToken,
            seed.HouseholdId,
            HttpMethod.Put,
            $"/api/finance/credit-cards/{createdCard.Id}/statements/{createdStatement.Id}",
            JsonContent.Create(new
            {
                closingDate = new DateOnly(2026, 7, 20),
                dueDate = new DateOnly(2026, 8, 5),
                notes = "Fatura reagendada",
                transactionIds = new[] { createdTransaction.Id },
                externalSource = "XLS",
                externalReference = "fatura-2026-08",
                importedAt = (DateTimeOffset?)null
            }));

        updateStatementResponse.EnsureSuccessStatusCode();

        var augustDetailResponse = await SendAuthorizedAsync(
            client,
            seed.OwnerAccessToken,
            seed.HouseholdId,
            HttpMethod.Get,
            "/api/finance/periods/2026/8");

        augustDetailResponse.EnsureSuccessStatusCode();
        var augustDetail = await augustDetailResponse.Content.ReadFromJsonAsync<FinancePeriodDetailResponse>(JsonSerializerOptions.Web);
        Assert.NotNull(augustDetail);
        Assert.Contains(
            augustDetail!.Entries,
            entry => entry.Origin == "CreditCardStatement" && entry.Amount == 220.9m && entry.CreditCardStatementId == createdStatement.Id);

        var deleteStatementResponse = await SendAuthorizedAsync(
            client,
            seed.OwnerAccessToken,
            seed.HouseholdId,
            HttpMethod.Delete,
            $"/api/finance/credit-cards/{createdCard.Id}/statements/{createdStatement.Id}");

        Assert.Equal(HttpStatusCode.NoContent, deleteStatementResponse.StatusCode);

        var deleteTransactionResponse = await SendAuthorizedAsync(
            client,
            seed.OwnerAccessToken,
            seed.HouseholdId,
            HttpMethod.Delete,
            $"/api/finance/credit-cards/{createdCard.Id}/transactions/{createdTransaction.Id}");

        Assert.Equal(HttpStatusCode.NoContent, deleteTransactionResponse.StatusCode);

        var deleteAssetResponse = await SendAuthorizedAsync(
            client,
            seed.OwnerAccessToken,
            seed.HouseholdId,
            HttpMethod.Delete,
            $"/api/finance/assets/{createdAsset.Id}");

        Assert.Equal(HttpStatusCode.NoContent, deleteAssetResponse.StatusCode);
    }

    [Fact]
    public async Task Finance_period_generation_keeps_a_single_period_per_month()
    {
        await using var factory = new HomePitApiFactory();
        using var client = factory.CreateClient();
        var seed = await SeedAsync(factory);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HomePitDbContext>();
            db.FinanceRecurringTemplates.Add(new FinanceRecurringTemplate
            {
                HouseholdId = seed.HouseholdId,
                CreatedByMemberId = seed.OwnerMemberId,
                Title = "Salario",
                DefaultAmount = 17852.58m,
                Type = FinanceEntryType.Entrada,
                Recurrence = FinanceRecurrence.Monthly,
                DayOfMonth = 6,
                IsActive = true
            });
            await db.SaveChangesAsync();
        }

        var firstResponse = await SendAuthorizedAsync(
            client,
            seed.OwnerAccessToken,
            seed.HouseholdId,
            HttpMethod.Post,
            "/api/finance/periods/2026/7/generate",
            JsonContent.Create(new { mode = "missingOnly" }));
        var secondResponse = await SendAuthorizedAsync(
            client,
            seed.OwnerAccessToken,
            seed.HouseholdId,
            HttpMethod.Post,
            "/api/finance/periods/2026/7/generate",
            JsonContent.Create(new { mode = "duplicateAll" }));

        firstResponse.EnsureSuccessStatusCode();
        secondResponse.EnsureSuccessStatusCode();

        await using var verificationScope = factory.Services.CreateAsyncScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<HomePitDbContext>();
        var julyPeriods = await verificationDb.FinancePeriods
            .CountAsync(period => period.HouseholdId == seed.HouseholdId && period.Year == 2026 && period.Month == 7);

        Assert.Equal(1, julyPeriods);
    }

    [Fact]
    public async Task Deleting_project_and_universe_nulls_finance_links()
    {
        await using var factory = new HomePitApiFactory();
        using var client = factory.CreateClient();
        var seed = await SeedAsync(factory);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<HomePitDbContext>();
            var period = new FinancePeriod
            {
                HouseholdId = seed.HouseholdId,
                Year = 2026,
                Month = 7
            };
            var template = new FinanceRecurringTemplate
            {
                HouseholdId = seed.HouseholdId,
                CreatedByMemberId = seed.OwnerMemberId,
                UniverseId = seed.UniverseId,
                ProjectId = seed.ProjectId,
                Title = "Condominio",
                DefaultAmount = 776.5m,
                Type = FinanceEntryType.Saida,
                Recurrence = FinanceRecurrence.Monthly,
                DayOfMonth = 10,
                IsActive = true
            };
            var entry = new FinanceEntry
            {
                HouseholdId = seed.HouseholdId,
                FinancePeriod = period,
                CreatedByMemberId = seed.OwnerMemberId,
                UniverseId = seed.UniverseId,
                ProjectId = seed.ProjectId,
                Title = "Compra classificada",
                Amount = 90m,
                Type = FinanceEntryType.Saida,
                Verified = false,
                ReferenceDate = new DateOnly(2026, 7, 6),
                Origin = FinanceEntryOrigin.Manual
            };
            var card = new CreditCardAccount
            {
                HouseholdId = seed.HouseholdId,
                CreatedByMemberId = seed.OwnerMemberId,
                Name = "Cartao da casa",
                ClosingDay = 20,
                DueDay = 25,
                IsActive = true
            };
            var transaction = new CreditCardTransaction
            {
                HouseholdId = seed.HouseholdId,
                CreditCardAccount = card,
                CreatedByMemberId = seed.OwnerMemberId,
                UniverseId = seed.UniverseId,
                ProjectId = seed.ProjectId,
                Title = "Compra no cartao",
                Amount = 120m,
                PurchasedOn = new DateOnly(2026, 7, 7)
            };

            db.FinancePeriods.Add(period);
            db.FinanceRecurringTemplates.Add(template);
            db.FinanceEntries.Add(entry);
            db.CreditCardAccounts.Add(card);
            db.CreditCardTransactions.Add(transaction);
            await db.SaveChangesAsync();
        }

        var deleteProjectResponse = await SendAuthorizedAsync(
            client,
            seed.OwnerAccessToken,
            seed.HouseholdId,
            HttpMethod.Delete,
            $"/api/projects/{seed.ProjectId}");

        Assert.Equal(HttpStatusCode.NoContent, deleteProjectResponse.StatusCode);

        await using (var afterProjectScope = factory.Services.CreateAsyncScope())
        {
            var db = afterProjectScope.ServiceProvider.GetRequiredService<HomePitDbContext>();
            Assert.All(await db.FinanceEntries.Where(item => item.HouseholdId == seed.HouseholdId).ToArrayAsync(), item => Assert.Null(item.ProjectId));
            Assert.All(await db.FinanceRecurringTemplates.Where(item => item.HouseholdId == seed.HouseholdId).ToArrayAsync(), item => Assert.Null(item.ProjectId));
            Assert.All(await db.CreditCardTransactions.Where(item => item.HouseholdId == seed.HouseholdId).ToArrayAsync(), item => Assert.Null(item.ProjectId));
            Assert.All(await db.FinanceEntries.Where(item => item.HouseholdId == seed.HouseholdId).ToArrayAsync(), item => Assert.Equal(seed.UniverseId, item.UniverseId));
        }

        var deleteUniverseResponse = await SendAuthorizedAsync(
            client,
            seed.OwnerAccessToken,
            seed.HouseholdId,
            HttpMethod.Delete,
            $"/api/universes/{seed.UniverseId}");

        Assert.Equal(HttpStatusCode.NoContent, deleteUniverseResponse.StatusCode);

        await using var afterUniverseScope = factory.Services.CreateAsyncScope();
        var verificationDb = afterUniverseScope.ServiceProvider.GetRequiredService<HomePitDbContext>();
        Assert.All(await verificationDb.FinanceEntries.Where(item => item.HouseholdId == seed.HouseholdId).ToArrayAsync(), item => Assert.Null(item.UniverseId));
        Assert.All(await verificationDb.FinanceRecurringTemplates.Where(item => item.HouseholdId == seed.HouseholdId).ToArrayAsync(), item => Assert.Null(item.UniverseId));
        Assert.All(await verificationDb.CreditCardTransactions.Where(item => item.HouseholdId == seed.HouseholdId).ToArrayAsync(), item => Assert.Null(item.UniverseId));
    }

    [Fact]
    public async Task New_household_starts_with_default_finance_categories()
    {
        await using var factory = new HomePitApiFactory();
        using var client = factory.CreateClient();
        var seed = await SeedAsync(factory);

        var createHouseholdResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/households")
        {
            Headers =
            {
                Authorization = new AuthenticationHeaderValue("Bearer", seed.OwnerAccessToken)
            },
            Content = JsonContent.Create(new { name = "Casa Nova" })
        });

        createHouseholdResponse.EnsureSuccessStatusCode();
        var createdHousehold = await createHouseholdResponse.Content.ReadFromJsonAsync<HouseholdResponse>(JsonSerializerOptions.Web);
        Assert.NotNull(createdHousehold);

        var listCategoriesResponse = await SendAuthorizedAsync(
            client,
            seed.OwnerAccessToken,
            createdHousehold!.Id,
            HttpMethod.Get,
            "/api/finance/categories");

        listCategoriesResponse.EnsureSuccessStatusCode();
        var categories = await listCategoriesResponse.Content.ReadFromJsonAsync<IReadOnlyCollection<FinanceCategoryResponse>>(JsonSerializerOptions.Web);
        Assert.NotNull(categories);
        Assert.Equal(FinanceCategoryCatalog.DefaultNames, categories!.Select(item => item.Name).ToArray());
    }

    private static async Task<SeedResult> SeedAsync(HomePitApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HomePitDbContext>();
        var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();

        var ownerUser = new AppUser
        {
            Email = $"finance-owner-{Guid.NewGuid():N}@homepit.dev",
            PasswordHash = "hash",
            DisplayName = "Owner",
            SystemRole = SystemRole.User
        };
        var memberUser = new AppUser
        {
            Email = $"finance-member-{Guid.NewGuid():N}@homepit.dev",
            PasswordHash = "hash",
            DisplayName = "Member",
            SystemRole = SystemRole.User
        };
        var household = new Household
        {
            Name = "Casa Financeira"
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

        db.Users.AddRange(ownerUser, memberUser);
        db.Households.Add(household);
        db.HouseholdMembers.AddRange(ownerMember, member);
        await db.SaveChangesAsync();

        db.FinanceCategories.AddRange(FinanceCategoryCatalog.CreateDefaults(household.Id, ownerMember.Id));
        await db.SaveChangesAsync();

        var universe = new Universe
        {
            HouseholdId = household.Id,
            CreatedByMemberId = ownerMember.Id,
            Name = "Casa"
        };
        db.Universes.Add(universe);
        await db.SaveChangesAsync();

        var project = new Project
        {
            HouseholdId = household.Id,
            UniverseId = universe.Id,
            CreatedByMemberId = ownerMember.Id,
            Name = "Reforma"
        };
        db.Projects.Add(project);
        await db.SaveChangesAsync();

        return new SeedResult(
            tokenService.CreateAccessToken(ownerUser, [ownerMember]),
            tokenService.CreateAccessToken(memberUser, [member]),
            household.Id,
            ownerMember.Id,
            db.FinanceCategories.First(item => item.HouseholdId == household.Id && item.Name == "Salário").Id,
            universe.Id,
            project.Id);
    }

    private static HttpRequestMessage CreateAuthorizedRequest(
        string accessToken,
        Guid householdId,
        HttpMethod method,
        string path)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Add("X-Household-Id", householdId.ToString());
        return request;
    }

    private static async Task<HttpResponseMessage> SendAuthorizedAsync(
        HttpClient client,
        string accessToken,
        Guid householdId,
        HttpMethod method,
        string path,
        HttpContent? content = null)
    {
        using var request = CreateAuthorizedRequest(accessToken, householdId, method, path);
        request.Content = content;
        return await client.SendAsync(request);
    }

    private sealed record SeedResult(
        string OwnerAccessToken,
        string MemberAccessToken,
        Guid HouseholdId,
        Guid OwnerMemberId,
        Guid DefaultCategoryId,
        Guid UniverseId,
        Guid ProjectId);

    private sealed record FinancePeriodDetailResponse(
        Guid? Id,
        int Year,
        int Month,
        bool Exists,
        FinancePeriodSummaryResponse Summary,
        IReadOnlyCollection<FinanceEntryResponse> Entries,
        IReadOnlyCollection<CreditCardTransactionResponse> CardTransactions,
        IReadOnlyCollection<CreditCardStatementResponse> Statements);

    private sealed record FinancePeriodSummaryResponse(
        decimal TotalIncome,
        decimal TotalExpense,
        decimal CashBalance,
        decimal AnalyticalExpenseTotal,
        int VerifiedEntries,
        int PendingVerificationEntries,
        int CardPurchaseCount);

    private sealed record FinanceCategoryResponse(Guid Id, string Name, bool IsDefault, int UsageCount);

    private sealed record HouseholdResponse(Guid Id, string Name, string Role, DateTimeOffset CreatedAt);

    private sealed record FinanceRecurringTemplateResponse(Guid Id, Guid? CategoryId, Guid? UniverseId, Guid? ProjectId);

    private sealed record FinanceEntryResponse(
        Guid Id,
        decimal Amount,
        bool Verified,
        string Origin,
        Guid? RecurringTemplateId,
        Guid? CreditCardStatementId,
        Guid? CategoryId,
        string? CategoryName,
        Guid? UniverseId,
        Guid? ProjectId);

    private sealed record AssetResponse(Guid Id, string Type);

    private sealed record AssetValuationResponse(Guid Id, int ReferenceYear, string Label, decimal Amount);

    private sealed record CreditCardAccountResponse(Guid Id, string Name, int OpenTransactionCount, decimal OpenTransactionTotal);

    private sealed record CreditCardTransactionResponse(Guid Id, Guid? CreditCardStatementId, Guid? CategoryId);

    private sealed record CreditCardStatementResponse(Guid Id, Guid? FinanceEntryId, DateOnly DueDate, decimal TotalAmount, int TransactionCount);

    private sealed class HomePitApiFactory : WebApplicationFactory<Program>, IAsyncDisposable
    {
        private readonly SqliteConnection connection = new("Data Source=:memory:");

        public HomePitApiFactory()
        {
            connection.Open();
        }

        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            builder.UseSetting(Microsoft.AspNetCore.Hosting.WebHostDefaults.EnvironmentKey, "Development");
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Database:ApplyMigrationsOnStartup"] = "false",
                    ["Notifications:DailyDigestEnabled"] = "false",
                    ["ObjectStorage:CreateBucketOnStartup"] = "false"
                });
            });

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<HomePitDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<HomePitDbContext>>();
                services.RemoveAll<HomePitDbContext>();
                services.RemoveAll<IHomePitDbContext>();
                services.RemoveAll<IObjectStorage>();

                services.AddDbContext<HomePitDbContext>(options => options.UseSqlite(connection));
                services.AddScoped<IHomePitDbContext>(provider => provider.GetRequiredService<HomePitDbContext>());
                services.AddSingleton<IObjectStorage, FakeObjectStorage>();

                using var provider = services.BuildServiceProvider();
                using var scope = provider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<HomePitDbContext>();
                db.Database.EnsureCreated();
                SeedMigrationHistory(db);
            });
        }

        private static void SeedMigrationHistory(HomePitDbContext db)
        {
            db.Database.ExecuteSqlRaw("""
                CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
                    "MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
                    "ProductVersion" TEXT NOT NULL
                );
                """);

            var productVersion =
                typeof(DbContext).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
                ?? typeof(DbContext).Assembly.GetName().Version?.ToString()
                ?? "10.0.0";

            foreach (var migrationId in db.Database.GetMigrations())
            {
                db.Database.ExecuteSqlInterpolated(
                    $"""
                    INSERT OR IGNORE INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
                    VALUES ({migrationId}, {productVersion});
                    """);
            }
        }

        public new async ValueTask DisposeAsync()
        {
            await connection.DisposeAsync();
            await base.DisposeAsync();
        }
    }

    private sealed class FakeObjectStorage : IObjectStorage
    {
        public Task EnsureBucketExistsAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task PutAsync(ObjectStoragePutRequest request, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<StoredObject> GetAsync(string objectKey, CancellationToken cancellationToken) =>
            throw new NotFoundException("Arquivo não encontrado.");

        public Task DeleteAsync(string objectKey, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
