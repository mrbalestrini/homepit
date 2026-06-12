---
name: backend
description: Use for backend ASP.NET Core Minimal APIs, application services, domain behavior, integrations, and API contracts in HomePit. Do not use for changes primarily about authentication or database schema.
---

# Backend

## Quando usar
- Alterar endpoints, DTOs, servicos de Application ou integracoes da API.
- Implementar comportamento de projetos, prompts, casas ou notificacoes.

## Quando não usar
- Mudanca centrada em JWT, papeis ou sessao; use `auth`.
- Mudanca centrada em modelo EF ou migration; use `database`.

## Antes de agir, leia
- `.specs/memory/architecture.md`
- `.specs/memory/conventions.md`
- `.specs/memory/security.md`
- `.specs/shared/domain-rules.md`
- `.specs/shared/technical-patterns.md`
- A spec ativa em `.specs/changes/`, se existir.

## Responsabilidades
- Manter endpoints finos e regras nos servicos de Application.
- Preservar tenancy, autorizacao, Problem Details e cancelamento.
- Coordenar DTOs, OpenAPI, frontend e testes quando o contrato mudar.

## Limites
- Nao adicionar tecnologia fora da stack observada.
- Nao alterar schema incidentalmente.
- Nao usar comandos backend marcados como `NÃO IDENTIFICADO` sem validacao.

## Procedimento padrão
1. Entender a tarefa.
2. Ler a spec ativa em `.specs/changes/`, se existir.
3. Ler os arquivos de memoria relevantes.
4. Identificar impacto.
5. Propor plano antes de alterar codigo quando houver risco.
6. Atuar somente dentro da responsabilidade e do escopo da skill.
7. Indicar validacoes necessarias.

## Formato de resposta esperado
- Resumo:
- Arquivos impactados:
- Plano:
- Riscos:
- Validacao:
- Proximos passos:
