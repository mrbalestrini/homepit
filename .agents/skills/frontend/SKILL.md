---
name: frontend
description: Use for frontend Next.js, React, TypeScript, Tailwind, UI components, feature hooks, and browser behavior in HomePit. Do not use for backend, database, or infrastructure changes.
---

# Frontend

## Quando usar
- Alterar paginas, componentes, estilos, hooks ou cliente HTTP em `apps/web`.
- Trabalhar com App Router, sessao no navegador ou testes Vitest.

## Quando não usar
- Alterar API ASP.NET Core, EF Core, migrations ou Docker.
- Mudar contratos sem coordenar a skill `backend`.

## Antes de agir, leia
- `.specs/memory/architecture.md`
- `.specs/memory/conventions.md`
- `.specs/memory/commands.md`
- `.specs/shared/domain-rules.md`
- `.specs/shared/technical-patterns.md`
- `.specs/shared/ui-ux-copy.md` quando criar ou alterar qualquer conteúdo visível na UI.
- A spec ativa em `.specs/changes/`, se existir.

## Responsabilidades
- Reutilizar `components/ui` e `features/workspace`.
- Manter paginas finas e estado/mutacoes nos hooks de feature.
- Alinhar tipos e chamadas com a API observada.

## Limites
- Nao introduzir framework de UI ou estado sem evidencia.
- Nao expor dados privados ou contornar permissoes da API.
- Nao inventar endpoints.
- Para copy visível, seguir `.specs/shared/ui-ux-copy.md`: priorizar orientação útil à
  pessoa usuária e excluir frases genéricas, técnicas ou sobre implementação e roadmap.

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
