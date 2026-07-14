# Prompt 01 — fundação de conexões

Implemente a fundação de integrações descrita em `.specs/changes/2026-07-14-integracoes-mcp/`. Leia `spec.md`, `decisions.md`, `database.md`, as skills `auth` e `database`, e os padrões de tenancy/autoria antes de editar.

Crie o domínio e os mappings EF para conexões, auditoria e idempotência. Gere chave manual com identificador público, 256 bits de segredo, HMAC-SHA256 com pepper configurável e revelação única. A conexão deve pertencer a usuário e Casa, ter `ReadOnly|ReadWrite`, expiração obrigatória de no máximo 365 dias, revogação e último uso. Implemente autenticação da conexão apenas sob `/api/integrations/v1`; rejeite `X-Household-Id`, imponha papel/autoria/cotas atuais e bloqueie conta/vínculo inativos.

Inclua migration descobrível pelo EF, limpeza segura de retenção de 90 dias, flags `Integrations:Enabled`, Problem Details com `code`, `traceId`, `retryable`, limite de 60/minuto por conexão e testes. Não implemente OAuth, MCP, frontend ou rotas de domínio neste prompt. Não faça commit.
