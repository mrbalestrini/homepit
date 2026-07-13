# Database change: esforço semanal

## Alteração proposta

Adicionar a tabela `homepit.member_effort_allocations`, sem backfill, para valores explícitos de pontos por membro, escopo e dia da semana.

## Rollback

Remover a tabela e seus índices. Como não há migração de dados existentes, não há transformação reversa.

## Plano de migration

Criar chaves estrangeiras para Casa, membro, Universo e Projeto, índices únicos parciais por escopo e restrições de valor não negativo e escopo exclusivo.

## Testes e validação

- A migration declara `[DbContext(typeof(HomePitDbContext))]` e `[Migration("20260713160000_AddMemberEffortAllocations")]` no próprio arquivo para descoberta pelo EF.
- A compilação e `dotnet ef migrations list` não foram executados: o repositório exige SDK .NET 10.0.100 e o ambiente possui apenas SDK 9.0.304.

## Validação explícita antes de DDL/DML

- [x] Impacto e rollback revisados.
- [x] Execução autorizada pela solicitação de implementação.
