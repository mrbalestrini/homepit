# Bugfix: persistir selecao da casa ativa

## Contexto e problema observado

Hoje a aplicacao sempre resolve a casa ativa apenas pela sessao carregada. Se o usuario
tem mais de uma casa, a selecao nao sobrevive a reload e o fluxo cai para a primeira casa
disponivel. Se a casa salva deixa de existir, a selecao antiga continua sujeita a ser
reutilizada sem limpeza explicita do armazenamento local.

## Objetivo

Persistir a casa ativa em `localStorage` para lembrar a ultima selecao valida por usuario,
com fallback seguro para outra casa existente e limpeza automatica do valor salvo quando
ele ficar invalido.

## Evidencia

- `use-project-dashboard` e `use-prompt-bank` resolvem `activeHouseholdId` somente pela
  sessao.
- A lista de casas chega ordenada pelo backend e o frontend usava apenas o estado atual
  da sessao como fallback.
- `Household` e uma entidade auditavel e o contrato agora expõe `CreatedAt` na resposta de
  casas para permitir um fallback por recencia mais preciso.

## Causa

O estado da casa ativa vive apenas em memoria do hook. Nao existe persistencia local nem
validação dedicada para limpar o valor salvo quando ele deixa de corresponder a uma casa
existente.

## Escopo

- Adicionar persistencia segura da casa ativa por usuario no frontend.
- Reutilizar a mesma regra em projetos e banco de prompts.
- Limpar o valor salvo quando a casa nao existir mais.
- Manter fallback funcional para uma casa existente da sessao, priorizando a mais recente
  quando houver `CreatedAt`.

## Fora de escopo

- Mudar regras de permissao, tenancy ou persistencia no banco.
- Criar novo estado global fora do frontend.

## Arquivos ou areas envolvidas

- `apps/web/src/lib/`
- `apps/web/src/features/projects/use-project-dashboard.ts`
- `apps/web/src/features/prompts/use-prompt-bank.ts`
- `apps/web/src/features/projects/use-project-dashboard.test.tsx`
- possivel novo teste de helper compartilhado

## Riscos

- Persistencia errada entre usuarios se a chave nao for isolada por usuario.
- Fallback indevido para casa removida se a validacao nao limpar o armazenamento.
- Divergencia entre telas se a regra for duplicada em vez de compartilhada.

## Plano de correcao

1. Criar helper compartilhado para ler, gravar, limpar e resolver a casa ativa com
   tratamento seguro de erro em `localStorage`.
2. Atualizar os hooks de projetos e prompts para usar o helper na inicializacao e nas
   mudancas de selecao.
3. Garantir que a selecao invalida seja removida do armazenamento local.
4. Cobrir o comportamento com testes focados no helper e na integracao do hook.

## Testes e validacao

- `npm test` em `apps/web`
- `npm run lint` em `apps/web`

## Criterios de aceite

- O navegador lembra a ultima casa selecionada por usuario.
- Ao recarregar, a aplicacao restaura a selecao valida salvo no `localStorage`.
- Se a casa salva nao existir mais, a aplicacao limpa o valor salvo e usa outra casa
  existente.
- Um erro de leitura/gravação nao impede a aplicacao de abrir.

## Decisao final

Adicionar a persistencia somente no frontend, com helper compartilhado e limpeza
defensiva do armazenamento local.
