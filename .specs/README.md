# Especificacoes do HomePit

Esta pasta guarda memoria factual, regras compartilhadas e registros de mudancas para
desenvolvimento assistido por IA.

- `memory/`: estado observado do projeto.
- `shared/`: vocabulario, regras de dominio e padroes tecnicos.
- `active-change.md`: estado explicito da mudanca atualmente ativa.
- `changes/`: descoberta, decisoes e tarefas por mudanca.
- `templates/`: modelos minimos para novos registros.
- `archive/`: mudancas encerradas que deixaram de ser ativas.

Atualize a memoria quando fatos do repositorio mudarem. Nao substitua codigo, contratos
OpenAPI ou documentacao operacional por resumos nesta pasta.

## Fluxo recomendado

- Antes de explorar `changes/`, consulte `active-change.md`.
- Nova feature: crie `.specs/changes/AAAA-MM-DD-nome-da-feature/` e use
  `templates/feature.md` como `spec.md`.
- Bug: crie uma pasta em `changes/` e use `templates/bugfix.md`.
- Refactor: use `templates/refactor.md` e declare o comportamento que nao deve mudar.
- Banco: use `templates/database-change.md`; DDL ou DML exige validacao explicita antes da
  execucao.
- Revisao: use `templates/pr-review.md` com a skill `reviewer`.
- Ao finalizar: atualize `decisions.md`, `tasks.md` e as validacoes; mova a mudanca para
  `archive/` e ajuste `active-change.md`, ou registre explicitamente porque ela continua ativa.

## Marcadores

- `FATO OBSERVADO`: confirmado em codigo, configuracao, teste ou documentacao versionada.
- `INFERÊNCIA`: conclusao derivada das evidencias que ainda requer validacao.
- `NÃO IDENTIFICADO`: informacao ausente ou sem evidencia suficiente no repositorio.
