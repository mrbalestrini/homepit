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
- `apps/api/docker-compose.yml`
- `apps/web/docker-compose.yml`

Start order:

1. Supabase
2. Evolution API
3. API
4. Web

## API Environment

Use the Supabase internal host:

```txt
ConnectionStrings__HomePitDb=Host=supabase-db;Port=5432;Database=postgres;Username=supabase_admin;Password=...
```

Use the Evolution internal host:

```txt
EvolutionApi__BaseUrl=http://evolution-api:8080
EvolutionApi__ApiKey=...
EvolutionApi__InstanceName=homepit
```

## Web Environment

`NEXT_PUBLIC_API_BASE_URL` must be the browser-reachable URL for the API, not the internal Docker hostname.
