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

## 2026-07-16 - OAuth para MCP remoto

- `/mcp` aceita somente OAuth; chaves `hpit_*` continuam exclusivas do REST externo.
- O consentimento ocorre no Next.js, reutilizando o JWT existente e uma interação efêmera, opaca, vinculada a cliente, redirect URI, PKCE, escopo e resource. A interação expira em dez minutos e só pode ser consumida uma vez.
- OpenIddict 7.6 persiste aplicações, autorizações, escopos e tokens no schema `homepit`. Access e refresh tokens são de referência; o access token dura 15 minutos e refresh tokens são rotativos, até 30 dias e nunca devem exceder a conexão.
- DCR cria somente clientes públicos, sem segredo, com Authorization Code + PKCE. Callbacks exigem HTTPS, exceto loopback local explícito.
- Produção usa duas chaves Base64 distintas de ao menos 32 bytes para assinatura e criptografia OAuth. Como os tokens são referência e a validação é local, esta etapa não publica JWKS.

## 2026-07-17 - credencial assimétrica interna do OpenIddict

- O OpenIddict 7.6 exige uma credencial assimétrica mesmo quando o HomePit não concede o escopo `openid`. Foi adicionada uma chave RSA efêmera exclusivamente para esse requisito interno; as chaves simétricas configuradas continuam protegendo e sendo preferidas para access, refresh e authorization codes de referência.
- Não há ID token nem JWKS nesta etapa. A chave efêmera não é exposta, não adiciona variável de ambiente e sua rotação a cada processo não invalida os tokens de referência emitidos com as chaves simétricas persistentes.

## 2026-07-17 - discovery de Dynamic Client Registration

- O discovery OAuth continua sendo produzido pelo OpenIddict. Um handler de `HandleConfigurationRequestContext` acrescenta `registration_endpoint` para `/connect/register` e declara `none` entre os métodos aceitos pelo endpoint de token, preservando todos os métodos já anunciados pelo servidor.

## 2026-07-17 - recurso canônico do MCP no OpenIddict

- O recurso canônico `OAuthOptions.CanonicalMcpResource` é registrado no servidor OpenIddict e concedido explicitamente aos clientes públicos criados por Dynamic Client Registration. A validação de recurso permanece estrita no OpenIddict e em `OAuthConsentService`.
- Clientes DCR persistidos antes desta correção não recebem a nova permissão retroativamente. Como foram criados para testes do MCP Inspector, devem ser descartados e registrados novamente após o deploy; não haverá migration ou backfill automático.

## 2026-07-18 - compatibilidade OIDC sem ampliação de acesso MCP

- `openid` e `offline_access` são scopes aceitos e podem ser concedidos quando solicitados, mas não são scopes funcionais do MCP: `homepit.read` continua obrigatório e `homepit.write` continua condicionado à conexão `ReadWrite`.
- Clientes públicos criados por DCR recebem permissões explícitas para os scopes OIDC padrão. Quando `openid` é concedido, o ID token recebe somente o `sub` estável do usuário; identificadores da Casa, conexão e modo de acesso permanecem exclusivamente no access token.
- Clientes DCR já persistidos continuam funcionando com os scopes HomePit existentes. Para passar a solicitar `openid` ou `offline_access`, devem ser registrados novamente, pois permissões de scope de clientes existentes não são alteradas automaticamente.
