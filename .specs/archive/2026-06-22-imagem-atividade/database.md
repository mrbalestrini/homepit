# Database change: imagem em atividade

## Contexto

`Activity` hoje nao possui metadados persistidos de imagem. A feature exige armazenar o
estado do anexo no banco para descobrir o arquivo no object storage e permitir remoção
segura.

## Objetivo

Adicionar colunas de metadados de imagem em `activities` sem tocar em dados existentes.

## Escopo

- Nova migracao para `activities`.
- Mapeamento EF para chave, tipo e timestamp da imagem.
- Preparacao para limpeza do binario ao excluir atividade, projeto ou universo.

## Fora de escopo

- Criar tabela separada de anexos.
- Fazer backfill de dados antigos.
- Alterar integridade entre atividades e projetos.

## Arquivos ou areas envolvidas

- `apps/api/src/HomePit.Domain/Projects/Activity.cs`
- `apps/api/src/HomePit.Infrastructure/Data/HomePitDbContext.cs`
- `apps/api/src/HomePit.Infrastructure/Migrations/*`

## Modelo atual

`Activity` possui apenas campos textuais, status, prioridade, tamanho, prazo e relacoes
com projeto, responsavel, comentarios e pendencias.

## Alteracao proposta

Adicionar `ImageObjectKey`, `ImageContentType` e `ImageUpdatedAt` a `activities`, com
largura maxima para chave e content type igual aos demais modulos de imagem.

## Riscos para dados e compatibilidade

- Baixo para dados existentes, porque a migracao e nullable.
- Medio para compatibilidade se o contrato e a limpeza de storage nao forem alinhados.
- Medio para deploy automatico se a migration ficar sem metadados de descoberta.

## Rollback

Remover as colunas na ordem inversa e manter o codigo compativel com banco sem imagem.

## Plano de migration

1. Publicar o codigo com a nova entidade e o mapeamento.
2. Aplicar a migracao nullable em ambiente controlado.
3. Validar descoberta da migration no startup da API.
4. Verificar upload, leitura e exclusao do anexo por atividade.

## Validacao explicita antes de DDL/DML

- [x] Impacto e rollback revisados.
- [ ] Execucao explicitamente autorizada.

## Testes e validacao

- Testes de mapeamento e servico com banco InMemory.
- Verificacao do `MigrationMetadataTests` para descoberta da nova migration.
- Smoke dos endpoints de upload e leitura.

## Criterios de aceite

- A tabela `activities` recebe os novos metadados sem alterar colunas antigas.
- O EF descobre a migration pelo assembly.
- O fluxo de limpeza de storage pode localizar o objeto da atividade.

## Decisao final

Persistir a imagem da atividade na propria tabela de `activities` e manter o binario no
object storage privado.
