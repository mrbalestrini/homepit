# Bugfix: persistir sort e ajustar filtros do dashboard de projetos

## Contexto e problema observado

No dashboard de projetos, a seleção de `Ordenar por` voltava ao padrão depois de reload
ou troca de sessão. No mesmo bloco de filtro, os `select` usavam larguras fixas e não
ocupavam o espaço livre entre a busca e o fim da linha em telas largas.

## Objetivo

Persistir a escolha de ordenação em `localStorage` e tornar os filtros responsivos, com
largura mínima preservada e wrap coerente quando o espaço horizontal não for suficiente.

## Evidencia

- `use-project-dashboard` mantinha `filters.sort` apenas em estado local.
- `project-dashboard-workspace` usava `select` com largura fixa e um container que não
  flexionava para preencher o restante da linha.

## Causa

A preferência de ordenação não tinha persistência dedicada e o layout dos filtros foi
desenhado com tamanho estático em vez de distribuição flexível.

## Escopo

- Persistir a escolha de `Ordenar por` no frontend.
- Ajustar o layout dos filtros do dashboard de projetos.
- Cobrir o comportamento com teste de hook.

## Fora de escopo

- Alterar contratos de API.
- Mudar regras de negócio dos projetos.
- Introduzir novo estado global.

## Arquivos ou areas envolvidas

- `apps/web/src/features/projects/use-project-dashboard.ts`
- `apps/web/src/features/projects/project-dashboard.constants.ts`
- `apps/web/src/features/projects/project-dashboard-workspace.tsx`
- `apps/web/src/features/projects/use-project-dashboard.test.tsx`

## Riscos

- Regredir a ordem padrão se a leitura/gravação do `localStorage` falhar.
- Criar quebra visual em larguras intermediárias se os limites mínimos forem agressivos.

## Plano de correcao

1. Adicionar chave de storage para a preferencia de ordenacao.
2. Ler e gravar o sort no hook do dashboard.
3. Ajustar o container dos filtros para usar crescimento flexivel com largura minima.
4. Validar o comportamento com teste automatizado.

## Testes e validacao

- `npm test -- src/features/projects/use-project-dashboard.test.tsx`
- `npm run lint -- src/features/projects/use-project-dashboard.ts src/features/projects/use-project-dashboard.test.tsx src/features/projects/project-dashboard-workspace.tsx src/features/projects/project-dashboard.constants.ts`

## Criterios de aceite

- O dashboard restaura a ultima escolha de `Ordenar por` apos reload.
- A troca de ordenacao atualiza o valor salvo no navegador.
- O botão de limpar volta para o sort padrao e atualiza o storage.
- Os `select` do filtro ocupam o espaco livre em telas largas e quebram linha quando nao
  couberem.

## Decisao final

Persistir a ordenacao no frontend com `localStorage` e ajustar o bloco de filtros para
largura flexivel com minima preservada.
