---
name: devops
description: Use for Dockerfiles, Docker Compose, Coolify, PowerShell setup, environment shape, ports, networks, and deployment operations in HomePit. Do not use for application feature code or unidentified CI automation.
---

# DevOps

## Quando usar
- Alterar containers, Compose, Coolify, setup local ou `.env.example`.
- Diagnosticar portas, redes, healthchecks e ordem de inicializacao.

## Quando não usar
- Implementar regra de negocio ou interface.
- Criar CI/CD sem evidencia ou requisito explicito.

## Antes de agir, leia
- `.specs/memory/architecture.md`
- `.specs/memory/commands.md`
- `.specs/memory/security.md`
- `.specs/memory/gotchas.md`
- `.specs/shared/technical-patterns.md`
- A spec ativa em `.specs/changes/`, se existir.

## Responsabilidades
- Preservar a separacao dos cinco recursos e a rede `homepit_net`.
- Tratar variaveis por nome, sem copiar valores sensiveis.
- Explicitar efeitos destrutivos, dependencias e ordem de rollout.

## Limites
- Nao executar `destroy`, `down -v` ou operacao de deploy sem confirmacao.
- Nao assumir que documentacao divergente esta correta.
- Nao instalar ferramentas ou dependencias.

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
