# Feature: pagina institucional com CMS

## Contexto

O HomePit ainda nao possui uma pagina publica institucional. A rota `/` redireciona para
o sistema autenticado e nao existe uma superficie para gerenciar conteudo de marketing.

## Objetivo

Criar uma landing page publica de aquisicao e um CMS separado, com publicacao imediata e
acesso de escrita exclusivo ao `SystemRole.SuperAdmin`.

## Escopo

- Pagina publica em `/`.
- CMS em `/admin/institutional`.
- Conteudo estruturado em secoes fixas.
- Duas imagens publicas armazenadas no object storage.
- Configuracao global sem vinculo com casa.
- Conteudo padrao antes do primeiro salvamento.

## Fora de escopo

- Rascunho, agendamento ou historico de versoes.
- Rich text ou HTML arbitrario.
- Multiplas paginas ou page builder.
- Alteracao das permissoes do SuperAdmin sobre dados das casas.

## Arquivos ou areas envolvidas

- `apps/api/src/HomePit.Domain/Institutional/`
- `apps/api/src/HomePit.Application/Institutional/`
- `apps/api/src/HomePit.Infrastructure/Data/HomePitDbContext.cs`
- `apps/api/src/HomePit.Api/Program.cs`
- `apps/web/src/app/`
- `apps/web/src/features/institutional/`
- `contracts/openapi/homepit.v1.yaml`

## Regras de negocio

- Existe no maximo uma configuracao com slug `home`.
- Beneficios e etapas possuem entre 1 e 6 itens ordenados.
- URLs de conversao aceitam somente HTTP ou HTTPS.
- Imagens aceitam JPG, PNG ou WEBP e no maximo 5 MB.
- Somente SuperAdmin le e altera o CMS; a leitura da pagina e das imagens e publica.
- A escrita do CMS e a unica excecao ao acesso global somente leitura do SuperAdmin.

## Riscos

- Banco: nova estrutura global sem tenancy.
- API/contrato: novos endpoints publicos e administrativos.
- Autenticacao/autorizacao: a excecao de escrita deve permanecer limitada ao CMS.
- Frontend: `/` deixa de redirecionar para `/projects`.
- Deploy/ambiente: a migration deve existir, mas nao sera aplicada nesta implementacao.

## Plano

1. Criar entidades, mapeamento e migration reversivel.
2. Implementar servico, autorizacao, endpoints e contrato OpenAPI.
3. Implementar landing publica e CMS.
4. Cobrir comportamento, permissoes e imagens com testes.
5. Atualizar memoria, permissoes e changelog.

## Testes

- Unidade e integracao do backend para defaults, atualizacao, autorizacao e imagens.
- Vitest para landing, CMS e visibilidade do atalho.
- Lint e build do frontend.

## Criterios de aceite

- `/` exibe conteudo institucional sem autenticacao.
- O conteudo padrao funciona sem registro no banco.
- SuperAdmin altera o conteudo e a mudanca fica publica imediatamente.
- Outros perfis nao acessam nem alteram o CMS.
- Imagens publicas usam URL versionada e cache imutavel.

## Decisao final

Usar uma configuracao global estruturada, com listas filhas ordenadas e publicacao
imediata por upsert.
