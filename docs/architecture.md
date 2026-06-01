# Architecture

HomePit is a modular monolith with deployable edges split for Coolify.

## Runtime Resources

- `infra/supabase`: PostgreSQL using the Supabase Postgres image, plus a lightweight Studio/meta setup.
- `infra/evolution`: Evolution API with its own PostgreSQL and Redis.
- `infra/minio`: private S3-compatible object storage for profile photos and future file uploads.
- `apps/api`: ASP.NET Core API, custom auth, EF Core migrations and WhatsApp digest worker.
- `apps/web`: Next.js operational UI.

All resources join the external Docker network `homepit_net`.

## Backend Modules

- `Auth`: custom login/register/refresh tokens.
- `Households`: tenant boundary for a family/home.
- `Projects`: `Universe > Project > Activity > PendingItem`.
- `Notifications`: daily WhatsApp summaries through Evolution API.
- `Storage`: private object storage abstraction backed by MinIO.

Future modules should stay inside the same API until they need independent scaling.

## Tenancy

Every user belongs to one or more households. Data-bearing project entities store `HouseholdId`; the API resolves the active household from `X-Household-Id`. If a user has only one household, the API can infer it.

## Secrets

No real secrets should be committed. Use `.env.example` for shape only and configure production values in Coolify.
