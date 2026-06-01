# Supabase self-hosted

Este recurso fornece o Postgres do HomePit e uma UI de apoio. O MVP usa autenticação custom na API, então Supabase Auth/Storage ficam fora do caminho crítico inicial.

## Coolify

1. Crie a rede externa `homepit_net`.
2. Adicione este diretório como um recurso Docker Compose.
3. Configure as variáveis do `.env.example`.
4. Use `Host=supabase-db;Port=5432;Database=postgres;Username=supabase_admin;Password=...` na API.

Para produção completa com Auth/Storage/Realtime, substitua este compose pelo compose oficial completo do Supabase mantendo o serviço `supabase-db` na rede `homepit_net`.
