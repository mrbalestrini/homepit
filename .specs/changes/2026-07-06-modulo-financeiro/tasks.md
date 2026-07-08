# Tarefas

- [x] Adicionar entidades de dominio financeiro e atualizar `IHomePitDbContext`.
- [x] Mapear o modulo financeiro no `HomePitDbContext` e criar a migration manual.
- [x] Implementar `FinanceService` com regras de tenancy, autoria e geracao mensal.
- [x] Expor o grupo `/api/finance` em `Program.cs`.
- [x] Atualizar o OpenAPI manual com os novos caminhos e schemas.
- [x] Ativar a rota `/finance` no shell do workspace.
- [x] Criar tipos frontend, controller `use-finance-dashboard` e workspace visual.
- [x] Cobrir comportamentos centrais com testes backend e frontend.
- [x] Atualizar changelog, versao e memoria relevante.
- [x] Refatorar a barra do financeiro para a modal dedicada de recorrências e revisar a copy em pt-BR com acentuação correta.
- [x] Adicionar categorias financeiras por household com defaults, CRUD de personalizadas e seleção opcional em caixa, recorrências e compras de cartão.
- [x] Adicionar edição inline otimista nas tabelas do financeiro com rollback em erro e sincronização pontual por seção.
- [x] Simplificar a confirmação de exclusão em caixa/cartão e permitir exclusão em lote nas tabelas de lançamentos e compras.
- [x] Organizar a área central do financeiro em abas locais para `Caixa` e `Cartões`, mantendo `Patrimônio` fixo abaixo.
- [x] Adicionar importação em lote de compras de cartão via JSON com revisão editável, criação automática de categorias faltantes e persistência atômica.
