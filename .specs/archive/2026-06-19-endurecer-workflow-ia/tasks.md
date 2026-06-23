# Tarefas

- [x] Criar a mudanca de refactor para o endurecimento do workflow.
- [x] Adicionar `.specs/active-change.md`.
- [x] Criar `.specs/shared/sources-of-truth.md`.
- [x] Arquivar mudancas concluidas e reduzir ruido em `.specs/changes/`.
- [x] Atualizar `AGENTS.md` e `.specs/README.md` para o novo fluxo.
- [x] Adicionar `scripts/validate-ai-workflow.ps1`.
- [x] Completar `openai.yaml` de `architect`, `reviewer` e `tester`.
- [x] Atualizar memoria e comandos observados.
- [x] Executar validacoes estruturais do workflow.

## Validacoes executadas em 2026-06-19

- `.\scripts\validate-ai-workflow.ps1`: concluido sem falhas; permaneceu apenas o warning
  conhecido sobre a divergencia entre a porta padrao do compose da API e a expectativa do
  setup local.
- `python %USERPROFILE%\.codex\skills\.system\skill-creator\scripts\quick_validate.py`
  para todas as skills em `.agents/skills`: 9 skills validadas com sucesso.
