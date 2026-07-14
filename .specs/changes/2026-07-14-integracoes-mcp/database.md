# Database change: integrações e MCP

## Contexto

As integrações precisam de credenciais revogáveis, limitação operacional, idempotência e OAuth sem alterar os dados de Financeiro ou Projetos existentes.

## Alteração proposta

- Criar `integration_connections` com usuário, Casa, nome/cliente, tipo, modo, expiração, revogação, prefixo, hash, último uso e auditoria.
- Criar tabelas de auditoria e idempotência de integração com retenção de 90 dias.
- Adicionar tabelas OpenIddict no schema `homepit` para clientes, autorizações, escopos e tokens de referência.
- Adicionar a migration e snapshot com metadados `[DbContext]` e `[Migration]` para descoberta automática no startup/deploy.

## Compatibilidade e rollback

- Não haverá backfill: nenhuma credencial existente será convertida.
- A API web existente e seus tokens permanecem inalterados.
- Rollback operacional imediato desabilita `Integrations:Enabled` e `Mcp:Enabled`; o rollback estrutural só ocorrerá depois de preservar os registros necessários à investigação.

## Validação explícita antes de DDL/DML

- [x] Impacto e rollback revisados.
- [x] Execução explicitamente autorizada pela solicitação de implementação.

## Testes e validação

- Confirmar criação e descoberta da migration por EF, inclusive no fluxo configurado de startup.
- Cobrir índices de busca por identificador/prefixo, exclusão/revogação, expiração, isolamento da Casa, limpeza de 90 dias e tabelas OpenIddict.
