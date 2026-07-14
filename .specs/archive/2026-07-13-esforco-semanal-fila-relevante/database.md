# Database change: esforço semanal

## Alteração proposta

Adicionar a tabela `homepit.member_effort_allocations`, sem backfill, para valores explícitos de pontos por membro, escopo e dia da semana.

## Rollback

Remover a tabela e seus índices. Como não há migração de dados existentes, não há transformação reversa.

## Plano de migration

Criar chaves estrangeiras para Casa, membro, Universo e Projeto, índices únicos parciais por escopo e restrições de valor não negativo e escopo exclusivo.

## Testes e validação

- A migration declara `[DbContext(typeof(HomePitDbContext))]` e `[Migration("20260713160000_AddMemberEffortAllocations")]` no próprio arquivo para descoberta pelo EF.
- `dotnet test HomePit.sln`: 95 testes unitários e 39 testes de integração aprovados em 2026-07-14.

## Validação explícita antes de DDL/DML

- [x] Impacto e rollback revisados.
- [x] Execução autorizada pela solicitação de implementação.
