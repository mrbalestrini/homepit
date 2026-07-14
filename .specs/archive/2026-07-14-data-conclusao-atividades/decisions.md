# Decisoes

## 2026-07-14 - data de conclusão

- `CompletedAt` será `DateTimeOffset?`, preservando a primeira conclusão enquanto o status continuar concluído.
- A transição de status no serviço é a fonte da regra; endpoints de criação e edição também passam pela mesma normalização.
- Não haverá backfill para atividades concluídas antes da migration por falta de uma data histórica confiável.

## 2026-07-14 - ocultamento e busca

- O limite é estritamente anterior a 30 dias contados a partir de `CompletedAt`.
- A API continua retornando atividades da casa; o dashboard decide o que ocultar e consegue avisar quando a busca tem correspondência em item oculto.
- O botão de antigas é explícito e permanece ativo enquanto o usuário quiser ver o histórico.
