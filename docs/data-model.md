# Data Model

## Projects MVP

- `User`: login identity with e-mail, password hash, display name and optional WhatsApp number.
- `Space`: tenant boundary.
- `SpaceMember`: user membership and role inside a space.
- `Core`: top-level grouping, matching the Notion `Núcleo`, with the creating space member recorded for permissions.
- `Project`: belongs to a core and records the creating space member.
- `Activity`: belongs to a project, records the creating space member, exposes audit `CreatedAt` and carries status, priority, size, optional `DueDate` and optional responsible member.
- `ActivityComment`: activity discussion entry authored by the logged space member. Edited comments are flagged from audit timestamps.
- `PendingItem`: subtarefa with optional due date, snooze days and completion timestamp.
- `NotificationPreference`: digest settings per space member.
- `NotificationRun`: idempotency log for WhatsApp messages.

## Prompt Bank

- `Prompt`: shared prompt library entry scoped by `Space`, created by a space member, with required `Title` and `PromptText`, optional `Description`, optional link metadata and optional private image metadata stored in MinIO.
- `PromptCategory`: reusable category scoped by `Space`, created by a space member and unique by name inside the same house.
- `PromptCategoryAssignment`: explicit many-to-many join between `Prompt` and `PromptCategory`.
- `Core`: reused from the projects module as an optional classification for prompts. When a core is deleted, related prompts stay alive and their `CoreId` becomes `null`.

## Prompt Bank Constraints

- Every `Prompt` must belong to exactly one `Space`.
- Every `Prompt` must have at least one category assignment.
- `Prompt.LinkUrl` and `Prompt.LinkTitle` must be filled together or both remain `null`.
- Prompt categories are unique per space through the `(SpaceId, Name)` index.
- Prompt/category assignments are unique through the `(PromptId, CategoryId)` key.
- Prompt listing is optimized by indexes on `(SpaceId, UpdatedAt)` and `(SpaceId, CoreId, UpdatedAt)`.

## Institutional Page

- `InstitutionalPage`: global, auditable landing page configuration identified by the
  unique slug `home`; it is not scoped to a space.
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

- `[system] Núcleo.Núcleo` maps to `Core.Name`.
- `[system] Projetos.Nome` maps to `Project.Name`.
- `Atividades.Atividade` maps to `Activity.Title`.
- `Atividades.Descrição`, `Status`, `Prioridade`, `Tamanho` map directly.
- Activity comments are native to Organiza Club and are authored by the active space membership.
- `Pendências.Pendência`, `Descrição`, `Prioridade`, `Fim`, `Adiar` map to `PendingItem`.
- The prompt bank is native to Organiza Club and does not depend on Notion tables.
