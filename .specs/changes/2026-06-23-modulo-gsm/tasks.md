# Tarefas

- [x] Adicionar entidade GSM, enum de status, DTOs e servico de aplicacao.
- [x] Mapear a entidade no `HomePitDbContext` e criar a migracao com indice unico.
- [x] Expor os endpoints `/api/gsm-numbers` e atualizar o OpenAPI manual.
- [x] Adicionar tipos, pagina `/gsm`, hook de feature e workspace do modulo.
- [x] Implementar mascara, formatacao e contador textual de recarga no frontend.
- [x] Atualizar a apresentacao da gestao GSM com tabela responsiva, plano e custo mensal opcional.
- [x] Cobrir o fluxo com testes backend e frontend.
- [x] Atualizar memoria, changelog e versao ao concluir.
- [x] Separar cadastro GSM de lancamentos de recarga, com historico editavel e recalculo automatico da ultima recarga.
- [x] Adicionar `DaysWithoutRecharge` no GSM, calculando proxima recarga e atraso no frontend.
- [x] Trocar a listagem GSM para cards no mobile abaixo de `lg`.
- [x] Registrar o backfill do `LastRechargeOn` legado para o novo historico de recargas.
