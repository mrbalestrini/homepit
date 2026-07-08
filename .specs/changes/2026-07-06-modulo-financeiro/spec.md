# Feature: modulo financeiro

## Contexto

O HomePit ainda nao possui um modulo financeiro implementado, embora o shell do workspace ja
reserve esse espaco e exista uma documentacao inicial inspirada no fluxo mensal atual do
Notion.

## Objetivo

Permitir controlar caixa mensal, recorrencias, cartoes de credito e patrimonio da household
em um modulo unico `/finance`, com tenancy, autoria e filtros compatíveis com os demais
modulos da casa.

## Escopo

- Persistir periodos mensais por household com unicidade por ano/mes.
- Persistir lancamentos financeiros mensais, recorrencias mensais/anuais, cartoes, compras,
  faturas, bens e referencias anuais de valor.
- Persistir categorias financeiras por household, com 12 categorias padrao imutaveis e CRUD
  apenas para categorias personalizadas visiveis no modulo financeiro.
- Reaproveitar `Universe` e `Project` como classificacoes opcionais em lancamentos,
  recorrencias e compras de cartao.
- Expor rotas `/api/finance/*` para periodos, lancamentos, recorrencias, bens e cartoes.
- Ativar a rota `/finance` no workspace com `Resumo` acima, abas locais para `Caixa` e
  `Cartoes` e `Patrimonio` sempre visivel.
- Cobrir regras centrais com testes backend e frontend.

## Fora de escopo

- Importacao automatica por SMS, XLS, OCR ou integracoes externas de cartao.
- Parcelamento de cartao, conciliacao bancaria e anexos de comprovantes.
- Modelagem estrutural de IPTU/IPVA dentro do patrimonio.

## Arquivos ou areas envolvidas

- `apps/api/src/HomePit.Domain/Finance/*`
- `apps/api/src/HomePit.Application/Finance/*`
- `apps/api/src/HomePit.Api/Program.cs`
- `apps/api/src/HomePit.Infrastructure/Data/HomePitDbContext.cs`
- `apps/api/src/HomePit.Infrastructure/Migrations/*`
- `apps/api/tests/*`
- `apps/web/src/app/finance/page.tsx`
- `apps/web/src/features/finance/*`
- `apps/web/src/features/workspace/homepit-workspace-shell.tsx`
- `apps/web/src/lib/api.ts`
- `contracts/openapi/homepit.v1.yaml`

## Regras de negocio

- O modulo e compartilhado pela household.
- `Owner` e `Admin` gerenciam todos os registros; `Member` gerencia apenas os registros que
  criou; `SuperAdmin` continua somente leitura.
- Periodo mensal e unico por `(HouseholdId, Year, Month)`.
- Gerar o mes aceita os modos `missingOnly` e `duplicateAll`.
- Quando `ProjectId` vier preenchido, o backend valida o projeto e deriva `UniverseId`.
- Lancamentos de caixa, recorrencias e compras de cartao podem apontar opcionalmente para
  uma categoria financeira da mesma household.
- Categorias padrao nao podem ser editadas nem excluidas.
- Excluir categoria personalizada apenas desvincula os registros que a utilizavam.
- O caixa mensal registra apenas a fatura consolidada do cartao, nao cada compra.
- A fatura consolidada do cartao permanece sem categoria para evitar dupla classificacao com
  as compras individuais.
- A visao analitica do mes soma gastos de caixa sem fatura consolidada e compras de cartao
  do mes para evitar dupla contagem.
- Bens podem ser `Property`, `Vehicle` ou `Other`; apenas os dois primeiros exigem detalhes
  tipados.
- Referencias anuais de valor usam registros livres por ano/rotulo, sem colunas fixas por
  ano.

## Riscos

- Banco: novo conjunto de tabelas, FKs opcionais para universo/projeto e migration manual
  precisam permanecer descobriveis pelo EF.
- API/contrato: muitas rotas e schemas novos precisam manter OpenAPI manual sincronizado.
- Autenticacao/autorizacao: o modulo inteiro precisa respeitar `X-Household-Id`, autoria e
  bloqueio de escrita do `SuperAdmin`.
- Frontend: a nova tela precisa reutilizar o shell e nao conflitar com os modulos ja ativos.
- Deploy/ambiente: migration manual sem metadados validos pode passar despercebida no startup.

## Plano

1. Adicionar entidades e mappings do dominio financeiro.
2. Criar migracao manual do modulo financeiro.
3. Implementar `FinanceService` e rotas `/api/finance`.
4. Ativar `/finance` no shell e criar controller + workspace do modulo.
5. Atualizar contrato, changelog, versao e testes.

## Testes

- Servico: geracao de periodo, recorrencia anual, consistencia universo/projeto, fatura
  consolidada, categorias e permissao.
- Integracao: CRUD basico de lancamentos, recorrencias, bens, cartoes e categorias com
  `X-Household-Id`.
- Frontend: carregamento do mes atual, dialogo de geracao, toggle de verificado,
  filtros/agrupamentos, formularios tipados e gestao de categorias.

## Criterios de aceite

- O usuario consegue operar o modulo `/finance` na household ativa.
- O mes atual abre mesmo sem periodo previamente gerado.
- Recorrencias podem gerar lancamentos do mes e duplicar quando solicitado.
- O cartao possui compras e faturas, e a fatura gera o lancamento mensal correspondente.
- `Caixa` e `Cartoes` podem ser alternados por abas locais sem esconder `Patrimonio`.
- O usuario consegue gerir categorias padrao e personalizadas sem sair do modulo financeiro.
- O shell deixa `Financeiro` de ser roadmap e passa a abrir a tela real.

## Decisao final

Implementar o modulo financeiro v1 do HomePit como um modulo interno compartilhado por
household, com caixa mensal, recorrencias, cartoes e patrimonio em um unico fluxo.
