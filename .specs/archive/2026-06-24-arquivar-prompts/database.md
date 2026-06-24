# Database change: arquivar prompts e ocultar imagens

## Contexto

O prompt atualmente é sempre tratado como ativo e a exclusão é definitiva.

## Objetivo

Adicionar um estado arquivado reversível por prompt, preservando o restante do cadastro.

## Escopo

- Criar a coluna `IsArchived` em `prompts`.
- Ajustar os índices para favorecer a consulta por casa + estado + atualização.
- Manter compatibilidade com o fluxo de migrations do EF Core.

## Fora de escopo

- Alterar a exclusão definitiva.
- Reestruturar categorias, imagens ou autoria.

## Arquivos ou areas envolvidas

- `apps/api/src/HomePit.Domain/Prompts/Prompt.cs`
- `apps/api/src/HomePit.Infrastructure/Data/HomePitDbContext.cs`
- `apps/api/src/HomePit.Infrastructure/Migrations/*`

## Modelo atual

O prompt não possui coluna de arquivamento.

## Alteracao proposta

- Adicionar `IsArchived` como booleano não nulo com padrão `false`.
- Ajustar a consulta principal para usar o novo estado sem mudar o restante do contrato.
- Manter a busca por prompts individuais sem restringir o estado, para o detalhe continuar
  acessível.

## Riscos para dados e compatibilidade

- A migration precisa continuar descobrível pelo EF.
- O novo índice precisa refletir o padrão real de consulta do banco de prompts.

## Rollback

- Reverter a migration se a feature não for publicada.

## Plano de migration

1. Adicionar a propriedade no domínio e no mapeamento.
2. Criar a migration com o novo campo e os índices ajustados.
3. Validar a compilação e os testes que cobrem o estado arquivado.

## Validacao explicita antes de DDL/DML

- [x] Impacto e rollback revisados.
- [x] Execucao explicitamente autorizada.

## Testes e validacao

- Validar listagem padrão, listagem arquivada e arquivar/desarquivar com testes de serviço.
- Validar o contrato da API e o mapeamento do frontend.

## Criterios de aceite

- O banco persiste o estado arquivado do prompt.
- A listagem padrão continua retornando apenas prompts ativos.
- A visão arquivada funciona sem quebrar o fluxo atual de CRUD.

## Decisao final

Adicionar arquivamento reversível diretamente na tabela de prompts, com consultas
separando estado ativo e arquivado.
