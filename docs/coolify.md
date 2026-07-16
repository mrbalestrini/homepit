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

Optional superadmin access:

```txt
SuperAdmin__Email=superadmin@example.com
SuperAdmin__Password=...
SuperAdmin__DisplayName=SuperAdmin
```

Use the MinIO internal host:

```txt
# Use the same credentials configured in the MinIO resource.
# ObjectStorage__AccessKey must match MINIO_ROOT_USER.
# ObjectStorage__SecretKey must match MINIO_ROOT_PASSWORD.
ObjectStorage__Endpoint=http://homepit-minio:9000
ObjectStorage__AccessKey=...
ObjectStorage__SecretKey=...
ObjectStorage__BucketName=homepit-assets
ObjectStorage__UseSsl=false
ObjectStorage__CreateBucketOnStartup=true
```

## OAuth for MCP

Keep `Integrations__Enabled=false` and `Mcp__Enabled=false` until the migration is applied and discovery has been checked over HTTPS. Then configure the API resource with the public URLs and two distinct Base64 secrets containing at least 32 random bytes each:

```txt
Integrations__Enabled=true
Integrations__TokenPepper=...
Mcp__Enabled=true
OAuth__Issuer=https://api.homepit.example.com
OAuth__WebConsentUrl=https://homepit.example.com/oauth/consent
OAuth__SigningKey=...
OAuth__EncryptionKey=...
OAuth__AccessTokenMinutes=15
OAuth__RefreshTokenDays=30
OAuth__InteractionMinutes=10
OAuth__TrustedProxies__0=<IP-interno-do-proxy-Coolify-que-encaminha-para-a-API>
AllowedHosts=api.homepit.example.com
Cors__AllowedOrigins__0=https://homepit.example.com
```

The Coolify proxy must preserve `X-Forwarded-Proto: https`. Set `OAuth__TrustedProxies__0` to the address that appears as the direct peer of the API container (not a Cloudflare address); production startup rejects OAuth without an explicitly trusted proxy. Before enabling MCP, verify `/.well-known/oauth-authorization-server` and `/.well-known/oauth-protected-resource/mcp` through the public API domain. Never reuse the JWT signing key for OAuth.

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
