# Decisions

## 2026-07-14 - visibilidade do catálogo

- A visibilidade pública do plano será controlada por `showInCatalog`.
- O catálogo público vai filtrar por essa flag, mas ainda mostrará o plano efetivo da
  conta autenticada quando ele estiver oculto.
- Os planos existentes e novos seeds começam com `showInCatalog = true` para não mudar o
  comportamento atual por padrão.
