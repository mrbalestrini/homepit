# Supermarket Module

Planned after the finance foundation.

## Goals

- Shopping lists shared by household.
- Store and product catalog.
- Purchase history with price tracking.
- Receipt ingestion through QR/XML/OCR when available.

## Initial Entities

- `Store`: market, location and optional tax identifier.
- `Product`: canonical product name, category and unit.
- `ShoppingList`: planned purchase list.
- `ShoppingListItem`: product, quantity and checked state.
- `Purchase`: date, store, total and receipt source.
- `PurchaseItem`: product, quantity, unit price and total.
- `PriceObservation`: normalized historical price for trend views.

## Receipt Import

The importer should preserve the raw source and create a review step before committing products/prices, because Brazilian receipts vary heavily by state and issuer.
