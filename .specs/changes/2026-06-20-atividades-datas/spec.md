# Feature: atividades com datas

## Contexto

Activities today already have `CreatedAt` via `AuditableEntity`, but the API contract and
frontend do not expose it. There is also no expected end date for an activity.

## Objetivo

Expose the creation timestamp and add an optional expected due date to activities across
API, database, OpenAPI, frontend, tests and docs.

## Escopo

- Persist `DueDate` on `Activity`.
- Expose `CreatedAt` and `DueDate` in the activity contract.
- Show due date in cards, list rows and the edit dialog.
- Show creation timestamp in the activity details sheet.

## Fora de escopo

- Change sorting, filters, permissions or notifications.
- Add validation that makes `DueDate` required.
- Change the audit model beyond exposing the existing `CreatedAt`.

## Arquivos ou areas envolvidas

- `apps/api/src/HomePit.Domain/Projects/Activity.cs`
- `apps/api/src/HomePit.Application/Projects/*`
- `apps/api/src/HomePit.Infrastructure/Migrations/*`
- `contracts/openapi/homepit.v1.yaml`
- `apps/web/src/lib/api.ts`
- `apps/web/src/features/projects/*`

## Regras de negocio

- `DueDate` is optional.
- `CreatedAt` is read-only and comes from the existing auditable base class.
- Date-only values use `DateOnly` in the backend and UTC-safe formatting in the frontend.

## Riscos

- Banco: nullable column and migration discovery must stay aligned with EF startup.
- API/contrato: clients consuming `Activity` must handle the new fields.
- Frontend: date formatting must avoid timezone shifts.
- Deploy/ambiente: migration metadata must remain discoverable at startup.

## Plano

1. Add `DueDate` to the domain entity, DTOs, contract and migration.
2. Surface `CreatedAt` from the existing audit field in the activity DTO.
3. Update the dashboard UI to capture and render the new dates.
4. Refresh the affected tests and docs.

## Testes

- Backend create/list/update activity flows with `dueDate`.
- Frontend dashboard fixtures and rendering for cards, list rows and details.
- Manual smoke test on create/edit/read after the migration.

## Criterios de aceite

- Activities can be created and updated with or without `dueDate`.
- Activity responses include `createdAt`.
- The dashboard shows due dates without timezone drift.

## Decisao final

Add an optional `dueDate` to `Activity`, expose the existing `CreatedAt`, and render both
dates in the project dashboard.
