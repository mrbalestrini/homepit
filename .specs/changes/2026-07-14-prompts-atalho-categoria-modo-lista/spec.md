# Feature: atalhos de categoria e modo lista no banco de prompts

## Contexto

O banco de prompts hoje filtra categorias apenas pelo dropdown e exibe os itens em um
layout em grade com cards variáveis.

## Objetivo

Permitir que as categorias da seção lateral atuem como atalho de filtro e oferecer uma
visualização em modo lista para facilitar a leitura sequencial dos prompts.

## Escopo

- Tornar as categorias da seção lateral clicáveis para aplicar e remover o filtro.
- Adicionar alternância entre modo grade e modo lista na área principal de prompts.
- Ajustar a apresentação dos itens para que o modo lista seja mais compacto e legível.
- Cobrir o comportamento com testes de frontend.

## Fora de escopo

- Mudanças no contrato da API.
- Mudanças no banco, migrations ou regras de domínio.
- Persistência obrigatória da preferência de visualização.

## Arquivos ou areas envolvidas

- `apps/web/src/features/prompts/prompt-bank-workspace.tsx`
- `apps/web/src/features/prompts/use-prompt-bank.ts`
- `apps/web/src/features/prompts/prompt-bank-workspace.test.tsx`
- `CHANGELOG.md`
- `.specs/memory/architecture.md`
- `.specs/memory/conventions.md`

## Regras de negocio

- Clicar em uma categoria aplica o filtro dessa categoria.
- Clicar novamente na mesma categoria remove esse filtro.
- O modo lista deve continuar respeitando os filtros existentes e a paginação atual.

## Riscos

- Banco: nenhum.
- API/contrato: nenhum.
- Autenticacao/autorizacao: nenhum.
- Frontend: risco de regressao visual ao acomodar dois layouts no mesmo componente.
- Deploy/ambiente: baixo, limitado ao build do Next.js.

## Plano

1. Adicionar estado de visualizacao no controller de prompts.
2. Tornar a lista de categorias clicavel como atalho de filtro.
3. Implementar o modo lista para os prompts e o alternador de visualizacao.
4. Atualizar testes e documentacao local.

## Testes

- `npm test` em `apps/web`
- `npm run build` em `apps/web`

## Criterios de aceite

- Uma categoria pode ser usada como atalho de filtro e pode ser desmarcada pelo mesmo
  clique.
- A tela de prompts oferece modo lista e modo grade.
- O comportamento atual de filtros, paginação, abertura de detalhe e ações de prompt
  continua funcionando.

## Decisao final
