# Coolify Deploy

## One-time Setup

Create an external Docker network on the Coolify host:

```bash
docker network create homepit_net
```

## Resources

Create one Coolify resource per compose file:

- `infra/supabase/docker-compose.yml`
- `infra/evolution/docker-compose.yml`
- `infra/minio/docker-compose.yml`
- `apps/api/docker-compose.yml`
- `apps/web/docker-compose.yml`

Start order:

1. Supabase
2. Evolution API
3. MinIO
4. API
5. Web

## API Environment

Use the Supabase internal host:

```txt
ConnectionStrings__HomePitDb=Host=supabase-db;Port=5432;Database=postgres;Username=supabase_admin;Password=...
Database__ApplyMigrationsOnStartup=true
```

Use the Evolution internal host:

```txt
EvolutionApi__BaseUrl=http://evolution-api:8080
EvolutionApi__ApiKey=...
EvolutionApi__InstanceName=homepit
```

Use the MinIO internal host:

```txt
ObjectStorage__Endpoint=http://homepit-minio:9000
ObjectStorage__AccessKey=...
ObjectStorage__SecretKey=...
ObjectStorage__BucketName=homepit-assets
ObjectStorage__UseSsl=false
ObjectStorage__CreateBucketOnStartup=true
```

## Troubleshooting

If the API logs an error like `column u.ProfilePhotoObjectKey does not exist`, the production database schema is behind the application model. The migration `20260601161000_AddUserProfilePhoto` must exist in `__EFMigrationsHistory` before the current API build can run correctly.

Quick checks:

```sql
SELECT "MigrationId"
FROM "__EFMigrationsHistory"
ORDER BY "MigrationId";
```

```sql
SELECT column_name
FROM information_schema.columns
WHERE table_schema = 'homepit'
  AND table_name = 'users'
  AND column_name IN ('ProfilePhotoObjectKey', 'ProfilePhotoUpdatedAt');
```

If those entries are missing, redeploy the API with `Database__ApplyMigrationsOnStartup=true` against the correct Postgres instance, or apply the EF Core migrations out of band before bringing the API back up.

## Web Environment

`NEXT_PUBLIC_API_BASE_URL` must be the browser-reachable URL for the API, not the internal Docker hostname.
