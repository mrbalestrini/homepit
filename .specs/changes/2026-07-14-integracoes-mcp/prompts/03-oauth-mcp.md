# Prompt 03 — OAuth e MCP remoto

Implemente OAuth e MCP conforme `.specs/changes/2026-07-14-integracoes-mcp/decisions.md`. Use as skills `auth`, `backend` e `database`; preserve integralmente a fronteira REST externa existente.

Adicione OpenIddict 7.5 no schema `homepit`, discovery, protected-resource metadata, registro dinâmico de clientes públicos, Authorization Code com PKCE S256, tokens de referência, access token de 15 minutos e refresh token rotativo limitado pela conexão. Crie consentimento autenticado na conta HomePit para escolher Casa, permissão e expiração, e permita revogação.

Hospede MCP Streamable HTTP stateless em `/mcp`, protocolo `2025-11-25`, com tools derivados dos `operationId` no padrão `finance_<verbo>_<recurso>` e `projects_<verbo>_<recurso>`. Omita e bloqueie tools de escrita para leitura. Exclusão deve usar preview e confirmação de uso único em cinco minutos. Entregue resources de espaço, catálogos e guia. Teste discovery, DCR, PKCE, audience, refresh, revogação, read-only e confirmação. Não crie a UI do perfil neste prompt.
