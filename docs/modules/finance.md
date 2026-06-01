# Finance Module

Planned after the projects MVP.

## Notion Mapping

- `Cálculo Mensal` becomes `MonthlyPeriod`.
- `Despesas Mensais` becomes `CashItem`.
- recurring fixed expenses become `MonthlyTemplate`.
- `Meus carros`, `Meus imóveis` and `Meus Bens (relacionamento)` become `Vehicle`, `Property` and `Asset`.

## Initial Entities

- `MonthlyPeriod`: month/year, notes and computed totals.
- `CashItem`: item, type `Entrada`/`Saida`, amount, reference date, verified flag and notes.
- `MonthlyTemplate`: recurring item copied into new monthly periods.
- `Asset`: normalized asset record with value, remaining debt and paid-off status.
- `Vehicle`: brand, model, year, Renavam and FIPE values.
- `Property`: registry, property inscription, private area, debt check date and notes.

## First Workflow

The first finance feature should reproduce the Notion button "Adicionar Itens Mensais": create a new month and copy active templates into it.
