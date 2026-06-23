# Feature: modulo gsm

## Contexto

O HomePit ainda nao possui um modulo dedicado para organizar numeros GSM compartilhados da
casa, embora o workspace ja preveja a expansao para novos modulos internos.

## Objetivo

Permitir cadastrar, visualizar, editar e excluir numeros GSM com dados de contexto,
status e acompanhamento do tempo desde a ultima recarga.

## Escopo

- Persistir numeros GSM por household com autoria e tenancy.
- Expor CRUD completo em `/api/gsm-numbers`.
- Validar e normalizar numeros com DDI opcional e DDD obrigatorio.
- Adicionar a pagina interna `/gsm` ao workspace compartilhado.
- Exibir contador textual desde a ultima recarga no frontend.
- Cobrir o fluxo com testes backend e frontend.

## Fora de escopo

- Alertas automaticos, importacao em massa ou filtros avancados.
- Historico de recargas.
- Suporte a formatos de telefone fora de 11 ou 13 digitos.
- Restringir DDI explicito a um pais especifico.

## Arquivos ou areas envolvidas

- `apps/api/src/HomePit.Domain/*`
- `apps/api/src/HomePit.Application/*`
- `apps/api/src/HomePit.Api/Program.cs`
- `apps/api/src/HomePit.Infrastructure/Data/HomePitDbContext.cs`
- `apps/api/src/HomePit.Infrastructure/Migrations/*`
- `apps/api/tests/*`
- `apps/web/src/app/*`
- `apps/web/src/features/gsm/*`
- `apps/web/src/features/workspace/homepit-workspace-shell.tsx`
- `apps/web/src/lib/api.ts`
- `contracts/openapi/homepit.v1.yaml`

## Regras de negocio

- O modulo e compartilhado pela household.
- `Owner` e `Admin` gerenciam todos os numeros; `Member` gerencia apenas os numeros que
  criou; `SuperAdmin` continua com acesso somente leitura.
- O numero deve aceitar apenas 11 ou 13 digitos.
- Entrada com 11 digitos e salva como `55 + DDD + numero`.
- Entrada com 13 digitos preserva o DDI explicito informado.
- O mesmo numero nao pode existir duas vezes na mesma household.
- `Title` e `AcquiredOn` sao obrigatorios.
- `LastRechargeOn` e opcional, nao pode ficar no futuro e nao pode ser anterior a
  `AcquiredOn`.
- `Status` aceita `Ativo`, `Inativo` e `Abandonado`.

## Riscos

- Banco: nova entidade, indice unico e migracao precisam permanecer descobriveis pelo EF.
- API/contrato: novo recurso com DTOs e endpoints precisa ser refletido no OpenAPI manual.
- Autenticacao/autorizacao: o CRUD precisa respeitar a household ativa e autoria.
- Frontend: nova pagina e novo modulo no shell sem conflitar com mudancas locais em
  andamento no dashboard de projetos.
- Deploy/ambiente: migration manual sem metadados validos pode passar despercebida.

## Plano

1. Adicionar a entidade GSM, enum de status, DTOs, servico e mapeamento EF.
2. Criar a migracao e expor o CRUD pela API protegida.
3. Implementar a pagina `/gsm`, o hook de feature, a mascara e o contador.
4. Cobrir comportamento e permissoes com testes backend e frontend.
5. Atualizar memoria, changelog e versao do frontend ao concluir.

## Testes

- Testes de servico para normalizacao, validacao, permissao e unicidade.
- Testes de integracao para CRUD e tenancy via endpoints.
- Vitest para utilitarios de mascara/contador e a tela do modulo GSM.

## Criterios de aceite

- O usuario consegue cadastrar, editar, listar e excluir numeros GSM na household ativa.
- O formulario impede formatos invalidos e normaliza o numero salvo.
- A lista mostra status, datas e o contador desde a ultima recarga.
- Permissoes, contrato e migration refletem o novo modulo.

## Decisao final

Adicionar um novo modulo interno `/gsm`, compartilhado por household, com CRUD completo de
numeros GSM, validacao forte de numero e contador textual de recarga no frontend.
