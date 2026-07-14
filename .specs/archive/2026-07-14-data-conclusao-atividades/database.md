# Database change: data de conclusão das atividades

## Contexto

Atividades só armazenam o status atual; não há histórico mínimo para saber quando entraram em concluídas.

## Objetivo

Adicionar uma data de conclusão opcional e manter seu ciclo de vida alinhado ao status.

## Escopo

- Coluna nullable `CompletedAt` em `homepit.activities`.
- Mapeamento EF Core para `timestamp with time zone`.
- Migration aditiva e atualização do snapshot.

## Fora de escopo

- Backfill de atividades já concluídas, pois não existe uma data confiável para derivar.
- Limpeza ou arquivamento físico de registros.

## Modelo atual

`Activity` possui `Status`, `CreatedAt` e `DueDate`, mas não possui data de conclusão.

## Alteracao proposta

Adicionar `DateTimeOffset? CompletedAt` e preencher/limpar o valor na transição de status no `ProjectService`.

## Riscos para dados e compatibilidade

- A coluna nullable permite rollout sem backfill e sem quebrar registros existentes.
- Atividades concluídas existentes terão `CompletedAt = null` e não serão tratadas como antigas até uma nova conclusão registrar a data.
- O campo deve aparecer no snapshot para que o EF detecte corretamente a migration no startup/deploy.

## Rollback

Remover a coluna por uma migration de rollback somente após confirmar que nenhum código implantado depende do campo. Não executar rollback neste trabalho.

## Plano de migration

Criar migration aditiva `AddActivityCompletedAt` com `[DbContext(typeof(HomePitDbContext))]` e `[Migration("...")]`, preservando os metadados de descoberta do EF.

## Validacao explicita antes de DDL/DML

- [x] Impacto e rollback revisados.
- [ ] Execução explícita autorizada.

## Testes e validacao

- Verificar metadados da migration.
- Executar testes de domínio/serviço e integração quando o SDK estiver disponível.

## Criterios de aceite

- O modelo e o snapshot contêm `CompletedAt` nullable.
- A migration adiciona apenas a coluna nullable.
- O startup do EF consegue descobrir a migration.

## Decisao final

Não aplicar DDL diretamente neste trabalho; apenas versionar a migration para o fluxo normal de startup/deploy.
