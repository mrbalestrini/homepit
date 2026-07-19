# Database change: mostrar plano no catálogo

## Contexto

O catálogo de planos precisa persistir uma flag de exibição pública por plano.

## Objetivo

Adicionar `showInCatalog` à tabela de planos para permitir ocultar itens do catálogo
público sem afetar o plano efetivo da conta autenticada.

## Escopo

- Nova coluna booleana em `plan_definitions`.
- Migration com valor padrão `true` e backfill dos registros existentes.
- Atualização do snapshot do EF e dos metadados de descoberta da migration.

## Fora de escopo

- Alterar dados de assinaturas.
- Criar novas tabelas ou índices.

## Modelo atual

- `plan_definitions` já armazena os atributos comerciais do plano, incluindo `IsPopular`
  e `SortOrder`.

## Alteracao proposta

- Adicionar `ShowInCatalog` como booleano obrigatório, com default `true`.
- Manter a semântica atual para todos os registros existentes.

## Riscos para dados e compatibilidade

- Se a migration não for descoberta corretamente, o startup pode acreditar que o schema
  está atualizado quando a coluna ainda não existir.

## Rollback

- Remover a coluna `ShowInCatalog`.

## Plano de migration

- `AddColumn<bool>(..., defaultValue: true)`.
- Atualizar o snapshot do contexto para incluir o novo campo.

## Validacao explicita antes de DDL/DML

- [x] Impacto e rollback revisados.
- [x] Execucao realizada no codigo do repositório.

## Testes e validacao

- Build e testes backend relacionados ao catálogo de planos.

## Criterios de aceite

- A migration deixa os planos existentes visíveis por padrão.
- O EF descobre a migration normalmente no startup.

## Decisao final

Persistir `showInCatalog` na tabela de planos com default `true` e backfill dos registros
existentes.
