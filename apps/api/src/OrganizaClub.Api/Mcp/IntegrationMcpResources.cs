using System.ComponentModel;
using System.Text.Json;
using OrganizaClub.Application.Finance;
using OrganizaClub.Application.Integrations;
using OrganizaClub.Application.Projects;
using ModelContextProtocol.Server;

namespace OrganizaClub.Api.Mcp;

[McpServerResourceType]
public sealed class IntegrationMcpResources
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [McpServerResource(UriTemplate = "organiza://space", Name = "Espaço Organiza Club", MimeType = "application/json")]
    [Description("Espaço, papel efetivo, validade e modo da conexão atual.")]
    public static async Task<string> Space(IntegrationConnectionService connections, CancellationToken cancellationToken) =>
        JsonSerializer.Serialize(await connections.GetCurrentSpaceAsync(cancellationToken), Json);

    [McpServerResource(UriTemplate = "organiza://finance/catalog", Name = "Catálogo financeiro", MimeType = "application/json")]
    [Description("Categorias financeiras disponíveis no Espaço atual.")]
    public static async Task<string> FinanceCatalog(FinanceService finance, CancellationToken cancellationToken) =>
        JsonSerializer.Serialize(new { categories = await finance.ListCategoriesAsync(cancellationToken) }, Json);

    [McpServerResource(UriTemplate = "organiza://projects/catalog", Name = "Catálogo de projetos", MimeType = "application/json")]
    [Description("Núcleos e projetos disponíveis no Espaço atual. Não inclui arquivos de imagem.")]
    public static async Task<string> ProjectsCatalog(ProjectService projects, CancellationToken cancellationToken) =>
        JsonSerializer.Serialize(new
        {
            cores = await projects.ListCoresAsync(cancellationToken),
            projects = await projects.ListProjectsAsync(null, cancellationToken)
        }, Json);

    [McpServerResource(UriTemplate = "organiza://docs/agent-guide", Name = "Guia para agentes", MimeType = "text/markdown")]
    [Description("Orientações de segurança e uso da integração Organiza Club.")]
    public static string AgentGuide() => """
        # Organiza Club integrations
        A conexão já determina o Espaço: nunca envie `X-Space-Id`.
        Use ISO 8601 para datas e uma `idempotencyKey` nova para cada criação.
        Conexões ReadOnly não podem executar operações de escrita.
        Imagens não são entregues por MCP; use somente seus metadados.
        """;
}
