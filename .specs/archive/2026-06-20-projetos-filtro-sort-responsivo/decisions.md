# Decisoes

1. A preferencia de `Ordenar por` sera salva em `localStorage` com a chave
   `homepit.projects.activity-sort`.
2. A leitura do sort salvo valida o valor antes de aplicar o estado.
3. O botao `Limpar` volta o sort para `priority` e atualiza o armazenamento local.
4. Os `select` do filtro usam crescimento flexivel com largura minima para ocupar o
   espaco livre sem perder o comportamento de wrap em telas menores.
