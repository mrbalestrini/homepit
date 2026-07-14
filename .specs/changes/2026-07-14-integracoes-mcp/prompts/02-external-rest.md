# Prompt 02 — API externa REST

Implemente a API externa planejada em `.specs/changes/2026-07-14-integracoes-mcp/` sobre a fundação de conexões já existente. Leia a skill `backend`, os serviços de Financeiro/Projetos, o contrato externo e a documentação em `docs/integrations/`.

Crie `contracts/openapi/homepit.integrations.v1.yaml` como OpenAPI 3.1 versão 1.0.0, base `/api/integrations/v1`, com `operationId`, schemas, exemplos, paginação por cursor (50 padrão, 200 máximo) e erros estáveis. Exponha `GET /space` e operações de Financeiro e Projetos cuja regra já exista. A API deve usar a Casa da conexão, suportar `Idempotency-Key` por 90 dias para mutações, devolver ETag nos recursos mutáveis e exigir `If-Match` em update/delete.

Não reutilize a autenticação JWT da web como contrato externo nem amplie permissões. Imagens são só metadados; pendências só listar/criar. Teste isolamento de Casa, leitura/escrita, autoria, conflito idempotente, ETag e paginação. Não implemente OAuth, MCP ou frontend neste prompt.
