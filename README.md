# HomePit

HomePit é um ambiente pessoal para organizar projetos de casa, demandas familiares, financeiro, automações domésticas e armazenamento de prompts da família. O MVP começa pelo módulo de projetos, inspirado na estrutura que já existe no Notion: `Universo > Projeto > Atividade > Pendência`.

## Stack

- Backend: ASP.NET Core, EF Core, PostgreSQL/Supabase self-hosted.
- Frontend: Next.js, TypeScript, Tailwind CSS e lucide-react.
- Object storage: MinIO privado para imagens e futuros documentos.
- Mensageria: Evolution API para resumos diários via WhatsApp.
- Deploy: Coolify com recursos separados por `docker-compose.yml`.

## Estrutura

```txt
apps/api         API .NET modular monolith
apps/web         Frontend Next.js
infra/supabase   Supabase/Postgres self-hosted
infra/evolution  Evolution API + Postgres + Redis
infra/minio      MinIO privado para assets
contracts        Contratos OpenAPI
docs             Arquitetura, dados e fases futuras
```

## Desenvolvimento

O backend mira `net10.0`. Instale o SDK .NET 10 para compilar localmente.

```powershell
cd apps/web
npm install
npm run dev
```

Cada recurso do Coolify tem seu próprio `.env.example`; copie para `.env` no ambiente de deploy e configure secrets pelo próprio Coolify.
