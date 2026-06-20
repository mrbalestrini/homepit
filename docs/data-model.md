# Data Model

## Projects MVP

- `User`: login identity with e-mail, password hash, display name and optional WhatsApp number.
- `Household`: tenant boundary.
- `HouseholdMember`: user membership and role inside a household.
- `Universe`: top-level grouping, matching the Notion `Universo`, with the creating household member recorded for permissions.
- `Project`: belongs to a universe and records the creating household member.
- `Activity`: belongs to a project, records the creating household member, exposes audit `CreatedAt` and carries status, priority, size, optional `DueDate` and optional responsible member.
- `ActivityComment`: activity discussion entry authored by the logged household member. Edited comments are flagged from audit timestamps.
- `PendingItem`: subtarefa with optional due date, snooze days and completion timestamp.
- `NotificationPreference`: digest settings per household member.
- `NotificationRun`: idempotency log for WhatsApp messages.

## Prompt Bank

- `Prompt`: shared prompt library entry scoped by `Household`, created by a household member, with required `Title` and `PromptText`, optional `Description`, optional link metadata and optional private image metadata stored in MinIO.
- `PromptCategory`: reusable category scoped by `Household`, created by a household member and unique by name inside the same house.
- `PromptCategoryAssignment`: explicit many-to-many join between `Prompt` and `PromptCategory`.
- `Universe`: reused from the projects module as an optional classification for prompts. When a universe is deleted, related prompts stay alive and their `UniverseId` becomes `null`.

## Prompt Bank Constraints

- Every `Prompt` must belong to exactly one `Household`.
- Every `Prompt` must have at least one category assignment.
- `Prompt.LinkUrl` and `Prompt.LinkTitle` must be filled together or both remain `null`.
- Prompt categories are unique per house through the `(HouseholdId, Name)` index.
- Prompt/category assignments are unique through the `(PromptId, CategoryId)` key.
- Prompt listing is optimized by indexes on `(HouseholdId, UpdatedAt)` and `(HouseholdId, UniverseId, UpdatedAt)`.

## Institutional Page

- `InstitutionalPage`: global, auditable landing page configuration identified by the
  unique slug `home`; it is not scoped to a household.
- `InstitutionalBenefit`: ordered benefit item deleted with its institutional page.
- `InstitutionalStep`: ordered process item deleted with its institutional page.
- Hero and highlight image metadata live on `InstitutionalPage`; binary objects use the
  existing object storage under `institutional/home/{slot}`.
- `(InstitutionalPageId, Position)` is unique inside each ordered child collection.

## Status and Priority

Activity status values:

- `NaoIniciada`
- `EmAndamento`
- `Concluido`

Priority values:

- `Baixa`
- `Media`
- `Alta`
- `Urgente`

## Notion Mapping

- `[system] Universo.Universo` maps to `Universe.Name`.
- `[system] Projetos.Nome` maps to `Project.Name`.
- `Atividades.Atividade` maps to `Activity.Title`.
- `Atividades.Descrição`, `Status`, `Prioridade`, `Tamanho` map directly.
- Activity comments are native to HomePit and are authored by the active household membership.
- `Pendências.Pendência`, `Descrição`, `Prioridade`, `Fim`, `Adiar` map to `PendingItem`.
- The prompt bank is native to HomePit and does not depend on Notion tables.
