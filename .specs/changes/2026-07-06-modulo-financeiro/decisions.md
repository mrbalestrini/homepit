# Decisoes

1. O contexto ativo do workflow passa a ser `2026-07-06-modulo-financeiro`.
2. `Universe` e `Project` existentes serao reaproveitados como classificacao opcional no
   financeiro.
3. Cada household tera no maximo um `FinancePeriod` por ano/mes.
4. A interface de gerar mes sempre oferecera `Adicionar faltantes` e `Duplicar todos`.
5. O lancamento mensal guardara um unico valor editavel e um flag `Verified`.
6. O caixa mensal recebera apenas a fatura consolidada do cartao; compras individuais ficam
   na secao de cartoes.
7. IPTU, IPVA e gastos anuais parecidos entram como recorrencias anuais comuns, sem
   relacionamento estrutural com bens no v1.
8. Faturas de cartao geram ou atualizam um `FinanceEntry` de origem
   `CreditCardStatement` no mes do vencimento.
9. O topo do financeiro expõe `Inserir Recorrências` como ação principal e a lista de
   recorrências vive em uma modal quase tela cheia dedicada.
10. Textos de UI em português no financeiro devem passar por revisão final de acentuação
    antes de novas criações ou renomeações serem consideradas concluídas.
11. Categorias financeiras sao vinculadas por household, mas ficam visiveis apenas dentro
    do modulo `/finance`.
12. As 12 categorias padrao do financeiro sao imutaveis e permanentes; apenas categorias
    personalizadas podem ser criadas, editadas e excluidas.
13. Excluir categoria personalizada apenas desvincula lancamentos, recorrencias e compras
    de cartao; a fatura consolidada do cartao permanece sem categoria.
