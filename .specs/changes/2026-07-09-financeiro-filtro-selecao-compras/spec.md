# Feature: filtro e selecao em massa no financeiro

## Contexto

A area `Financeiro > Cartoes > Compras` ja permite selecao multipla e exclusao em lote, mas a
listagem cresce rapido e a selecao item a item fica trabalhosa.

## Objetivo

Adicionar um filtro textual na listagem de compras do cartao e facilitar a selecao em massa
tanto nessa tabela quanto no fechamento da fatura.

## Escopo

- Inserir um campo de filtro na tabela de compras do cartao.
- Fazer o filtro considerar os campos textuais exibidos na linha da compra.
- Fazer a acao de selecionar todos respeitar apenas os itens atualmente visiveis no filtro.
- Manter selecoes ja feitas quando o filtro for alterado ou limpo.
- Adicionar uma forma mais rapida de selecionar todas as compras disponiveis no dialogo de
  fechamento da fatura.

## Fora de escopo

- Alterar a regra de exclusao em lote alem da selecao.
- Mudar contrato de API ou persistencia do financeiro.
- Criar filtros adicionais fora das telas de compras e fatura.

## Arquivos ou areas envolvidas

- `apps/web/src/features/finance/finance-dashboard-workspace.tsx`
- `apps/web/src/features/finance/finance-dashboard.utils.ts`
- `apps/web/src/features/finance/finance-dashboard-workspace.test.tsx`
- `apps/web/src/features/finance/finance-dashboard.utils.test.ts`
- `CHANGELOG.md`

## Regras de negocio

- O filtro textual das compras deve aceitar os campos abertos visiveis na linha da compra.
- O checkbox de selecionar todos na tabela de compras deve atuar apenas sobre os itens visiveis.
- Ao remover o filtro, a selecao existente permanece.
- No fechamento da fatura, o usuario deve conseguir selecionar todas as compras disponiveis sem clicar uma a uma.

## Riscos

- Banco: nenhum.
- API/contrato: nenhum.
- Autenticacao/autorizacao: nenhum.
- Frontend: a logica de selecao precisa distinguir itens visiveis de itens ja selecionados.
- Deploy/ambiente: nenhuma dependencia extra.

## Plano

1. Criar utilitario de filtro para compras de cartao e cobrir com teste unitario.
2. Atualizar a tabela de compras com filtro e selecao em massa restrita ao filtro.
3. Melhorar a modal de fechamento da fatura com acao de selecionar tudo.
4. Registrar a mudanca no changelog e validar com testes.

## Testes

- Cobertura unitaria do filtro textual das compras.
- Cobertura de interface para selecao em massa com filtro aplicado.
- Cobertura de interface para selecao total na modal de fatura.

## Criterios de aceite

- O usuario consegue filtrar compras por texto na area de cartoes.
- O checkbox de selecionar todos respeita o subconjunto visivel quando ha filtro.
- Limpar o filtro nao apaga selecoes anteriores.
- O fechamento de fatura oferece uma acao clara para selecionar todas as compras disponiveis.

## Decisao final

Implementar o filtro e a selecao em massa no fluxo de compras de cartao sem alterar a
persistencia ou o contrato de API.
