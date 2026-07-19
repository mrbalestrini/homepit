---
name: auth
description: Use for authentication, JWT, refresh tokens, password hashing, session handling, space roles, SuperAdmin, and authorization in OrganizaClub. Do not use for unrelated API or UI work.
---

# Auth

## Quando usar
- Alterar cadastro, login, refresh, perfil, claims ou sessao frontend.
- Alterar `SystemRole`, `SpaceRole`, SuperAdmin ou regras de permissao.

## Quando não usar
- Endpoint ou tela sem impacto de identidade ou acesso.
- Mudanca de schema isolada; coordene com `database`.

## Antes de agir, leia
- `.specs/memory/architecture.md`
- `.specs/memory/security.md`
- `.specs/memory/gotchas.md`
- `.specs/shared/domain-rules.md`
- `.specs/shared/technical-patterns.md`
- `docs/permissions.md`
- A spec ativa em `.specs/changes/`, se existir.

## Responsabilidades
- Preservar isolamento por espaço e acesso somente leitura do SuperAdmin.
- Avaliar backend, claims, frontend, OpenAPI e testes em conjunto.
- Tratar credenciais e tokens sem expor valores.

## Limites
- Nao enfraquecer hashing, validacao JWT ou autorizacao por conveniencia.
- Nao mover verificacao de seguranca apenas para o frontend.
- Nao inventar MFA, recovery ou politica ausente.

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
