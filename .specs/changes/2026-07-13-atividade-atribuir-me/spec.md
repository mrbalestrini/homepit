# Feature: Atribuir-me na atividade

## Contexto

O menu de ações de uma atividade precisa oferecer um atalho para que a pessoa
logada assuma o responsável rapidamente.

## Objetivo

Adicionar a ação `Atribuir-me` no dropdown das atividades para preencher o
responsável com o membro atual da casa.

## Escopo

- Exibir o atalho no menu das atividades.
- Reutilizar a atualização existente da atividade.
- Atualizar a interface com o responsável alterado.

## Fora de escopo

- Criar endpoint novo.
- Alterar regras de permissão.
- Alterar schema, migration ou contrato da API.

## Arquivos ou areas envolvidas

- `apps/web/src/features/projects/use-project-dashboard.ts`
- `apps/web/src/features/projects/project-dashboard-workspace.tsx`
- `apps/web/src/features/projects/*.test.tsx`
- `CHANGELOG.md`

## Regras de negocio

- A ação só deve aparecer quando houver membro atual reconhecido na casa.
- A atividade deve continuar respeitando as permissões existentes de edição.
- O atalho deve atribuir a atividade ao membro atual da sessão.

## Riscos

- Banco: nenhum.
- API/contrato: nenhum.
- Autenticacao/autorizacao: baixo, somente reaproveitamento das regras atuais.
- Frontend: baixo, mudança localizada no menu de atividades.
- Deploy/ambiente: baixo, sem dependências novas.

## Plano

1. Reaproveitar a mutação existente de atualização da atividade.
2. Expor o item no dropdown das atividades.
3. Cobrir o atalho com testes de UI e hook.
4. Registrar a mudança no changelog.

## Testes

- `npm test` em `apps/web`
- `npm run build` em `apps/web`

## Criterios de aceite

- O dropdown das atividades mostra `Atribuir-me` quando a ação está disponível.
- Ao acionar o item, o responsável passa a ser o membro logado.
- A interface reflete o novo responsável sem recarregar a página.

## Decisao final

O atalho foi implementado no frontend, usando a atualização existente da
atividade e sem mudança de contrato.
