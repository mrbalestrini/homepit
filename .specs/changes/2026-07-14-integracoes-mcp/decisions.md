# Decisions

## 2026-07-14 - fronteira e escopo inicial

- A API de integração terá contrato próprio OpenAPI 3.1, versão `1.0.0`, sob `/api/integrations/v1`; ela não reutiliza diretamente a API web autenticada por JWT.
- A Casa será vinculada à conexão. A API externa rejeitará `X-Household-Id` para evitar troca de tenant pelo cliente.
- Financeiro e Projetos são as primeiras superfícies. Imagens retornam somente metadados; pendências preservam somente listar/criar.

## 2026-07-14 - credenciais e autorização

- Conexão manual terá identificador público, segredo aleatório de 256 bits e hash HMAC-SHA256 com pepper configurado por ambiente.
- A chave completa é revelada uma única vez com `Cache-Control: no-store`; somente prefixo e metadados são mostrados depois.
- Toda conexão tem modo `ReadOnly` ou `ReadWrite`, expira em até 365 dias e é revogável imediatamente.
- A autorização reaplica as regras de Casa, papel, autoria, cota e ciclo de vida da conta no momento de cada operação.

## 2026-07-14 - protocolo e proteção operacional

- O MCP remoto usa Streamable HTTP stateless em `/mcp`, protocolo `2025-11-25`, e OAuth 2.1 com OpenIddict, Authorization Code + PKCE S256, DCR e tokens de referência.
- Access token dura 15 minutos; refresh token é rotativo e não pode sobreviver à conexão que o originou.
- Mutações usam chave de idempotência por 90 dias. REST usa ETag/`If-Match`; exclusão MCP usa preview e confirmação de uso único válida por cinco minutos.
- Auditoria mantém por 90 dias somente conexão, superfície, operação, resultado, data e `traceId`, sem payloads ou segredos. O limite inicial é 60 operações/minuto por conexão.

## 2026-07-14 - documentação e liberação

- `docs/integrations/` e o OpenAPI externo são fontes canônicas para consumidores; a aba Conexão os apresenta sem duplicar a especificação.
- A skill `.agents/skills/integration-docs` mantém a paridade de REST, MCP e exemplos sem segredos.
- A liberação é protegida por `Integrations:Enabled` e `Mcp:Enabled`. O rollback imediato desabilita flags e preserva os registros para investigação.

## 2026-07-16 - paginação e concorrência REST

- Listagens externas usam envelope `{ items, nextCursor }`, cursor opaco vinculado à consulta, limite padrão 50 e máximo 200.
- Recursos mutáveis são retornados como `{ data, etag }`; a ETag opaca codifica o identificador e a versão `UpdatedAt` e também é enviada no cabeçalho de respostas unitárias.
- `UpdatedAt` é token de concorrência do EF apenas para entidades Financeiro/Projetos expostas pelas integrações. Escritas externas exigem `If-Match`; ausência retorna `428`, formato inválido `400` e versão ultrapassada `412`.
- A migration de concorrência não executa DDL ou backfill; ela preserva o snapshot e os metadados de descoberta do EF.
