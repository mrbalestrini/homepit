# Data Model

## Projects MVP

- `User`: login identity with e-mail, password hash, display name and optional WhatsApp number.
- `Household`: tenant boundary.
- `HouseholdMember`: user membership and role inside a household.
- `Universe`: top-level grouping, matching the Notion `Universo`, with the creating household member recorded for permissions.
- `Project`: belongs to a universe and records the creating household member.
- `Activity`: belongs to a project, records the creating household member and carries status, priority, size and optional responsible member.
- `ActivityComment`: activity discussion entry authored by the logged household member. Edited comments are flagged from audit timestamps.
- `PendingItem`: subtarefa with optional due date, snooze days and completion timestamp.
- `NotificationPreference`: digest settings per household member.
- `NotificationRun`: idempotency log for WhatsApp messages.

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
