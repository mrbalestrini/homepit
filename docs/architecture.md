# Architecture

Organiza Club is a modular monolith with deployable edges split for Coolify.

## Runtime Resources

- `infra/supabase`: PostgreSQL using the Supabase Postgres image, plus a lightweight Studio/meta setup.
- `infra/evolution`: Evolution API with its own PostgreSQL and Redis.
- `infra/minio`: private S3-compatible object storage for profile photos and future file uploads.
- `apps/api`: ASP.NET Core API, custom auth, EF Core migrations and WhatsApp digest worker.
- `apps/web`: Next.js operational UI.

All resources join the external Docker network `organiza_club_net`.

## Backend Modules

- `Auth`: custom login/register/refresh tokens.
- `Spaces`: shared tenant boundary for a person, family or group.
- `Projects`: `Core > Project > Activity > PendingItem`.
- `Prompts`: prompt bank shared by space, reusing `Core` as an optional taxonomy and storing prompt images in private object storage.
- `Notifications`: daily WhatsApp summaries through Evolution API.
- `Storage`: private object storage abstraction backed by MinIO.

Future modules should stay inside the same API until they need independent scaling.

## Tenancy

Every user belongs to one or more spaces. Data-bearing project entities store `SpaceId`; the API resolves the active space from `X-Space-Id`. If a user has only one space, the API can infer it.

## Secrets

No real secrets should be committed. Use `.env.example` for shape only and configure production values in Coolify.
