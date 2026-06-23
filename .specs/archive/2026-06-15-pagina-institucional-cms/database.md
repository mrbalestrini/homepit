# Database change: pagina institucional com CMS

## Contexto

O conteudo institucional nao possui persistencia e nao pertence a uma casa.

## Objetivo

Persistir uma configuracao global `home`, com beneficios e etapas ordenados.

## Escopo

- Tabela `institutional_pages`.
- Tabelas filhas `institutional_benefits` e `institutional_steps`.
- Indice unico para slug e para posicao dentro de cada lista.

## Fora de escopo

- Alterar tabelas ou dados existentes.
- Aplicar a migration em um ambiente persistente.

## Arquivos ou areas envolvidas

- `HomePitDbContext`
- `20260615160000_AddInstitutionalPageCms`

## Modelo atual

Nao existe configuracao institucional persistida.

## Alteracao proposta

Criar uma entidade global auditavel e duas colecoes filhas com exclusao em cascata.
Metadados de imagens guardam somente chave, tipo e data; os binarios ficam no object
storage.

## Riscos para dados e compatibilidade

Baixo risco: somente novas tabelas, sem DML ou alteracao de colunas existentes.

## Rollback

O `Down` remove primeiro as tabelas filhas e depois `institutional_pages`.

## Plano de migration

1. Publicar a aplicacao com a migration versionada.
2. Autorizar explicitamente a execucao no ambiente alvo.
3. Aplicar a migration pelo fluxo operacional existente.
4. Validar os tres indices e a leitura publica.

## Validacao explicita antes de DDL/DML

- [x] Impacto e rollback revisados.
- [ ] Execucao explicitamente autorizada.

## Testes e validacao

- Testes de servico com EF InMemory.
- Testes de integracao dos endpoints.
- A migration nao foi aplicada nesta implementacao.

## Criterios de aceite

- As tabelas sao criadas sem tocar em dados existentes.
- Excluir a pagina remove beneficios e etapas.
- O slug `home` e unico.

## Decisao final

Migration reversivel criada e mantida sem execucao.
