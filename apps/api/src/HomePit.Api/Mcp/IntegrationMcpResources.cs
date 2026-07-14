using System.ComponentModel;
using System.Text.Json;
using HomePit.Application.Finance;
using HomePit.Application.Integrations;
using HomePit.Application.Projects;
using ModelContextProtocol.Server;

namespace HomePit.Api.Mcp;

[McpServerResourceType]
public sealed class IntegrationMcpResources
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [McpServerResource(UriTemplate = "homepit://space", Name = "Espaço HomePit", MimeType = "application/json")]
    [Description("Casa, papel efetivo, validade e modo da conexão atual.")]
    public static async Task<string> Space(IntegrationConnectionService connections, CancellationToken cancellationToken) =>
        JsonSerializer.Serialize(await connections.GetCurrentSpaceAsync(cancellationToken), Json);

    [McpServerResource(UriTemplate = "homepit://finance/catalog", Name = "Catálogo financeiro", MimeType = "application/json")]
    [Description("Categorias financeiras disponíveis na Casa atual.")]
    public static async Task<string> FinanceCatalog(FinanceService finance, CancellationToken cancellationToken) =>
        JsonSerializer.Serialize(new { categories = await finance.ListCategoriesAsync(cancellationToken) }, Json);

    [McpServerResource(UriTemplate = "homepit://projects/catalog", Name = "Catálogo de projetos", MimeType = "application/json")]
    [Description("Universos e projetos disponíveis na Casa atual. Não inclui arquivos de imagem.")]
    public static async Task<string> ProjectsCatalog(ProjectService projects, CancellationToken cancellationToken) =>
        JsonSerializer.Serialize(new
        {
            universes = await projects.ListUniversesAsync(cancellationToken),
            projects = await projects.ListProjectsAsync(null, cancellationToken)
        }, Json);

    [McpServerResource(UriTemplate = "homepit://docs/agent-guide", Name = "Guia para agentes", MimeType = "text/markdown")]
    [Description("Orientações de segurança e uso da integração HomePit.")]
    public static string AgentGuide() => """
        # HomePit integrations
        A conexão já determina a Casa: nunca envie `X-Household-Id`.
        Use ISO 8601 para datas e uma `idempotencyKey` nova para cada criação.
        Conexões ReadOnly não podem executar operações de escrita.
        Imagens não são entregues por MCP; use somente seus metadados.
        """;
}
