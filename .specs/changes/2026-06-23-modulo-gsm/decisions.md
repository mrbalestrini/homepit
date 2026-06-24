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
9. Cada numero GSM tambem carrega `Plan` com os valores `PrePago` e `PosPago`.
10. `MonthlyCost` e opcional, exibido e armazenado como valor monetario em BRL quando
    informado.
11. A listagem GSM no frontend deve priorizar uma tabela responsiva para concentrar
    contexto, status, plano, custo e recargas em uma unica visao.
12. `LastRechargeOn` deixa de ser editavel no formulario GSM e passa a ser um resumo
    derivado do historico de `gsm_recharges`.
13. `DaysWithoutRecharge` e opcional; quando preenchido, o frontend calcula a proxima
    recarga a partir da ultima recarga registrada ou da data de aquisicao.
14. Recargas possuem CRUD proprio com autoria e tenancy do modulo; `Owner` e `Admin`
    podem gerenciar tudo e `Member` apenas os registros que criou.
15. A listagem GSM passa a usar cards no mobile abaixo de `lg`, mantendo a tabela no
    desktop.
