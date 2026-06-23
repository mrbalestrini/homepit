# Decisoes

1. O cadastro GSM sera um modulo compartilhado por household, nao um cadastro pessoal.
2. O numero sera salvo apenas como digitos normalizados com DDI.
3. Entrada com 11 digitos recebera `55` como DDI padrao; entrada com 13 digitos mantera o
   DDI explicito.
4. O indice unico sera por `{HouseholdId, NormalizedNumber}`.
5. `LastRechargeOn` pode ficar vazia e deve renderizar `Sem recarga registrada`.
6. O contador de recarga sera calculado no frontend com regra calendaria textual.
7. `Owner` e `Admin` gerenciam todos os registros; `Member` gerencia apenas os proprios;
   `SuperAdmin` permanece somente leitura.
8. Migrations criadas ou ajustadas manualmente devem manter os metadados de descoberta do
   EF Core.
