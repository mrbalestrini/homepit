# Database change: modulo financeiro

## Contexto

O modulo financeiro v1 introduz persistencia para periodos mensais, caixa, recorrencias,
cartoes de credito e patrimonio.

## Objetivo

Adicionar o schema necessario ao financeiro preservando tenancy por household, autoria,
relacionamentos opcionais com universo/projeto e compatibilidade com o startup que aplica
migrations automaticamente.

## Escopo

- Novas tabelas para periodos, recorrencias, lancamentos, bens, referencias anuais,
  cartoes, compras e faturas.
- Novos indices e FKs opcionais para `universes` e `projects`.

## Fora de escopo

- Backfill de dados existentes.
- DDL/DML manual em banco fora da migration.

## Arquivos ou areas envolvidas

- `apps/api/src/HomePit.Domain/Finance/*`
- `apps/api/src/HomePit.Infrastructure/Data/HomePitDbContext.cs`
- `apps/api/src/HomePit.Infrastructure/Migrations/20260706120000_AddFinanceModule.cs`

## Modelo atual

Nao existe schema persistido para financeiro.

## Alteracao proposta

- Criar tabelas `finance_periods`, `finance_recurring_templates`, `finance_entries`,
  `assets`, `asset_property_details`, `asset_vehicle_details`, `asset_valuations`,
  `credit_card_accounts`, `credit_card_transactions` e `credit_card_statements`.
- Persistir enums como texto e datas sem horario em colunas `date`.
- Usar `SetNull` em FKs opcionais para universo/projeto, autoria e vinculos derivados.

## Riscos para dados e compatibilidade

- FKs opcionais para projeto/universo precisam sobreviver a exclusoes desses modulos sem
  quebrar historico.
- Faturas de cartao geram `FinanceEntry`; exclusoes precisam evitar orfaos.
- Migration manual precisa manter atributos do EF para ser descoberta automaticamente.

## Rollback

Rollback por `Down()` da migration removendo as tabelas do modulo financeiro.

## Plano de migration

1. Criar novas tabelas na ordem de dependencias.
2. Criar indices unicos e auxiliares.
3. Garantir `Down()` completo na ordem inversa.

## Validacao explicita antes de DDL/DML

- [x] Impacto e rollback revisados.
- [x] Execucao explicitamente autorizada.

## Testes e validacao

- Teste de metadados das migrations.
- Testes de servico e integracao cobrindo criacao, vinculacao opcional e fatura consolidada.

## Criterios de aceite

- A API inicializa com a migration descoberta pelo EF.
- O schema suporta o fluxo mensal, cartoes e patrimonio do financeiro v1.

## Decisao final

Persistir o financeiro em tabelas dedicadas dentro do schema `homepit`, mantendo o monolito
modular atual.
