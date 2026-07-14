using System.ComponentModel;
using System.Text.Json;
using HomePit.Application.Common;
using HomePit.Application.Finance;
using HomePit.Application.Integrations;
using HomePit.Application.Projects;
using HomePit.Domain.Integrations;
using ModelContextProtocol.Server;

namespace HomePit.Api.Mcp;

[McpServerToolType]
public sealed class IntegrationMcpTools
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [McpServerTool, Description("Lista lançamentos financeiros da Casa vinculada à conexão.")]
    public static async Task<string> finance_list_entries(
        [Description("Ano opcional do período.")] int? year,
        [Description("Mês opcional do período (1 a 12). ")] int? month,
        FinanceService finance,
        CancellationToken cancellationToken) =>
        JsonSerializer.Serialize(await finance.ListEntriesAsync(year, month, cancellationToken), Json);

    [McpServerTool, Description("Cria um lançamento financeiro. Exige uma conexão com escrita e uma chave de idempotência.")]
    public static async Task<string> finance_create_entry(
        CreateFinanceEntryRequest request,
        [Description("Chave única para repetir esta criação com segurança.")] string idempotencyKey,
        FinanceService finance,
        IntegrationIdempotencyService idempotency,
        IUserContext userContext,
        CancellationToken cancellationToken)
    {
        EnsureWrite(userContext);
        var result = await idempotency.ExecuteAsync("finance_create_entry", idempotencyKey, request,
            () => finance.CreateEntryAsync(request, cancellationToken), cancellationToken);
        return JsonSerializer.Serialize(result, Json);
    }

    [McpServerTool, Description("Lista atividades de projetos da Casa vinculada à conexão.")]
    public static async Task<string> projects_list_activities(
        [Description("Filtra por projeto, quando informado.")] Guid? projectId,
        ProjectService projects,
        CancellationToken cancellationToken) =>
        JsonSerializer.Serialize(await projects.ListActivitiesAsync(projectId, null, cancellationToken), Json);

    [McpServerTool, Description("Cria uma atividade em um projeto. Exige uma conexão com escrita e uma chave de idempotência.")]
    public static async Task<string> projects_create_activity(
        CreateActivityRequest request,
        [Description("Chave única para repetir esta criação com segurança.")] string idempotencyKey,
        ProjectService projects,
        IntegrationIdempotencyService idempotency,
        IUserContext userContext,
        CancellationToken cancellationToken)
    {
        EnsureWrite(userContext);
        var result = await idempotency.ExecuteAsync("projects_create_activity", idempotencyKey, request,
            () => projects.CreateActivityAsync(request, cancellationToken), cancellationToken);
        return JsonSerializer.Serialize(result, Json);
    }

    [McpServerTool, Description("Lista pendências de uma atividade. Pendências só podem ser listadas ou criadas nesta versão.")]
    public static async Task<string> projects_list_pending_items(
        Guid activityId,
        ProjectService projects,
        CancellationToken cancellationToken) =>
        JsonSerializer.Serialize(await projects.ListPendingItemsAsync(activityId, cancellationToken), Json);

    [McpServerTool, Description("Cria uma pendência para uma atividade. Exige uma conexão com escrita e uma chave de idempotência.")]
    public static async Task<string> projects_create_pending_item(
        Guid activityId,
        CreatePendingItemRequest request,
        [Description("Chave única para repetir esta criação com segurança.")] string idempotencyKey,
        ProjectService projects,
        IntegrationIdempotencyService idempotency,
        IUserContext userContext,
        CancellationToken cancellationToken)
    {
        EnsureWrite(userContext);
        var result = await idempotency.ExecuteAsync("projects_create_pending_item", idempotencyKey, new { activityId, request },
            () => projects.CreatePendingItemAsync(activityId, request, cancellationToken), cancellationToken);
        return JsonSerializer.Serialize(result, Json);
    }

    private static void EnsureWrite(IUserContext userContext)
    {
        if (userContext.IntegrationAccessMode != IntegrationAccessMode.ReadWrite)
        {
            throw new ForbiddenException("Esta conexão permite somente leitura.");
        }
    }
}
