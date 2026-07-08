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
14. As listagens tabulares do financeiro passam a aceitar edicao inline em campos rapidos,
    mantendo as modais atuais como caminho de edicao completa.
15. Atualizacoes inline do financeiro devem aplicar patch otimista local com rollback em
    erro e reconciliacao pontual do trecho afetado, sem depender de `refreshWorkspace()`
    apos cada edicao.
16. A exclusao de lancamentos de caixa e compras de cartao passa a usar apenas confirmacao
    simples na UI, sem exigir digitacao do titulo do registro.
17. As tabelas de caixa e compras de cartao passam a permitir selecao multipla para
    exclusao em lote dentro do proprio modulo financeiro.
18. A navegação principal do financeiro passa a alternar apenas entre `Caixa` e `Cartões`,
    com `Caixa` como aba inicial e sem persistência em `localStorage` ou na URL.
19. `Patrimônio` permanece sempre visível abaixo das abas, independentemente da seção
    ativa.
