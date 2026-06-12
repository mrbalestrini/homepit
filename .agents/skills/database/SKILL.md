---
name: database
description: Use for PostgreSQL, EF Core models, mappings, constraints, migrations, and data lifecycle changes in HomePit. Do not use for UI-only or service-only changes without schema impact.
---

# Database

## Quando usar
- Alterar entidades persistidas, `HomePitDbContext`, indices ou constraints.
- Planejar migration, compatibilidade, dados existentes ou rollback.

## Quando não usar
- Alteracao somente de UI ou regra sem impacto persistente.
- Executar `destroy` ou aplicar migration sem confirmacao.

## Antes de agir, leia
- `.specs/memory/architecture.md`
- `.specs/memory/commands.md`
- `.specs/memory/gotchas.md`
- `.specs/shared/domain-rules.md`
- `.specs/shared/technical-patterns.md`
- `.specs/templates/database-change.md`
- A spec ativa em `.specs/changes/`, se existir.

## Responsabilidades
- Preservar schema `homepit`, tenancy, autoria e historico.
- Avaliar cascata, `SetNull`, `Restrict`, unicidade e limites.
- Planejar migration, rollout, rollback e testes de metadados.

## Limites
- Nao inventar comando EF CLI.
- Nao apagar dados ou volumes.
- Nao mudar cascatas sem analisar impacto de dominio.

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
