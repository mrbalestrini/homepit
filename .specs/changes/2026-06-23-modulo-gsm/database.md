# Database change: modulo gsm

## Contexto

O HomePit nao possui tabela para armazenar numeros GSM compartilhados por household.

## Objetivo

Persistir numeros GSM com status, datas de aquisicao, prazo de recarga, historico de
recargas, autoria e indice unico por household.

## Escopo

- Criar a entidade persistida `GsmNumber`.
- Adicionar enum de status persistido como texto.
- Adicionar `Plan` como texto, `MonthlyCost` opcional e `DaysWithoutRecharge` inteiro
  opcional com precisao e validacao adequadas.
- Adicionar indice unico por household e numero normalizado.
- Gerar migracao EF Core descobrivel.
- Criar tabela de historico `gsm_recharges` com autoria e recalculo do resumo.

## Fora de escopo

- Migracao de dados legados.
- DDL/DML manual fora da migration do EF.

## Arquivos ou areas envolvidas

- `apps/api/src/HomePit.Domain/Gsm/*`
- `apps/api/src/HomePit.Application/Gsm/*`
- `apps/api/src/HomePit.Infrastructure/Data/HomePitDbContext.cs`
- `apps/api/src/HomePit.Infrastructure/Migrations/*`

## Modelo atual

Nao existe tabela ou entidade para numeros GSM.

## Alteracao proposta

- Criar tabela `gsm_numbers` no schema `homepit`.
- Colunas: `Id`, `CreatedAt`, `UpdatedAt`, `HouseholdId`, `CreatedByMemberId`,
  `Title`, `NormalizedNumber`, `Description`, `Plan`, `MonthlyCost`, `AcquiredOn`,
  `LastRechargeOn`, `Status`.
- `AcquiredOn` e `LastRechargeOn` usam tipo `date`.
- `Plan` usa conversao string e `MonthlyCost` usa precisao `10,2`.
- `Status` usa conversao string.
- Relacao com `Household` em cascata e com `HouseholdMember` em `SetNull`.
- O historico `gsm_recharges` usa `RechargedOn` como `date`, `Amount` com precisao
  monetaria e `Note` opcional.
- `LastRechargeOn` permanece em `gsm_numbers` apenas como resumo do historico.

## Riscos para dados e compatibilidade

- A unicidade do numero por household pode rejeitar duplicatas existentes em seeds futuros.
- O backfill do historico precisa preservar a descoberta da migration pelo EF e nao
  depender de extensoes nao garantidas no banco.
- Uma migracao sem metadados corretos pode nao ser detectada no startup/deploy.

## Rollback

- Remover a migration se a feature nao for liberada.
- Se necessario apos deploy, criar migration reversa que exclua `gsm_numbers`.

## Plano de migration

1. Implementar entidade e mapeamento EF.
2. Gerar migration EF com nome descritivo.
3. Validar metadados via teste `MigrationMetadataTests`.

## Validacao explicita antes de DDL/DML

- [x] Impacto e rollback revisados.
- [x] Execucao explicitamente autorizada.

## Testes e validacao

- Testar CRUD e unicidade com EF/InMemory e endpoints.
- Executar testes que cobrem descobribilidade de migrations.
- Validar CRUD de historico, recalculo do resumo e permissao de escrita.

## Criterios de aceite

- A tabela `gsm_numbers` existe com indice unico por household e numero.
- O EF Core detecta a migration automaticamente.
- O modulo persiste plano, custo mensal, prazo de recarga, datas e status conforme o
  contrato.
- O historico de recargas recalcula automaticamente o resumo `LastRechargeOn`.

## Decisao final

Adicionar uma nova tabela `gsm_numbers` dedicada ao modulo GSM, mantendo tenancy,
autoria e compatibilidade com o fluxo de migrations do HomePit.
