# Database change: plano popular

## Contexto

O catálogo comercial já existe no banco, mas não possui um campo persistido para indicar qual plano está destacada como popular na interface.

## Objetivo

Adicionar persistência para o destaque popular do catálogo de planos e garantir um valor inicial consistente.

## Escopo

- Adicionar `IsPopular` em `plan_definitions`.
- Backfill inicial para o plano Gold.
- Manter compatibilidade com os registros existentes.

## Fora de escopo

- Reestruturar o catálogo comercial.
- Criar regras de faturamento novas.

## Arquivos ou areas envolvidas

- `apps/api/src/OrganizaClub.Domain/Plans/PlanDefinition.cs`
- `apps/api/src/OrganizaClub.Infrastructure/Data/OrganizaClubDbContext.cs`
- `apps/api/src/OrganizaClub.Infrastructure/Migrations/*`

## Modelo atual

- `plan_definitions` possui campos de slug, nome, preços, limites e `SortOrder`.
- Não existe um marcadar persistido de popularidade.

## Alteracao proposta

- Incluir o campo booleano `IsPopular`.
- Definir valor padrão `false`.
- Atualizar o registro Gold para `true` na migration.

## Riscos para dados e compatibilidade

- Bancos já existentes precisam receber o novo campo sem perda de dados.
- O catálogo pode ficar sem destaque se a aplicação não consolidar o estado após edições.

## Rollback

- Remover a coluna `IsPopular` e reverter o backfill.

## Plano de migration

- `ADD COLUMN IsPopular boolean NOT NULL DEFAULT false`
- `UPDATE plan_definitions SET IsPopular = true WHERE Slug = 'gold'`

## Validacao explicita antes de DDL/DML

- [x] Impacto e rollback revisados.
- [x] Execucao explicitamente autorizada.

## Testes e validacao

- Testes de serviço e integração cobrindo leitura e atualização do destaque popular.
- Validação do startup com a migration descoberta pelo EF.

## Criterios de aceite

- O banco registra o plano popular sem exigir mudança manual em produção.
- O Gold vem destacada na base inicial.

## Decisao final

- O destaque popular é um booleano simples por plano, sem tabela extra.
