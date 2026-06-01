# Setup local do HomePit

Esta pasta centraliza o fluxo para executar o HomePit localmente com Docker ou Podman.

## Uso rapido

No PowerShell, a partir da raiz do projeto:

```powershell
.\infra\setup\homepit-local.ps1
```

O script abre um menu com tres opcoes:

- `start`: cria a rede local, prepara arquivos `.env` quando faltarem, constroi as imagens da API/web e sobe a stack.
- `stop`: derruba os containers da stack, mantendo volumes e dados locais.
- `destroy`: derruba containers e remove os volumes locais da stack.

Tambem e possivel executar sem menu:

```powershell
.\infra\setup\homepit-local.ps1 -Action start
.\infra\setup\homepit-local.ps1 -Action stop
.\infra\setup\homepit-local.ps1 -Action destroy
```

Para confirmar a remocao de volumes sem prompt interativo:

```powershell
.\infra\setup\homepit-local.ps1 -Action destroy -Yes
```

Se a politica de execucao do Windows bloquear scripts locais, use:

```powershell
powershell -ExecutionPolicy Bypass -File .\infra\setup\homepit-local.ps1 -Action start
```

Em Linux/macOS com PowerShell instalado:

```bash
pwsh ./infra/setup/homepit-local.ps1 -Action start
```

## Pre-requisitos

Instale e deixe em execucao uma das opcoes:

- Docker Desktop com `docker compose`.
- Podman com `podman compose` ou `podman-compose`.

O script detecta automaticamente Docker ou Podman. Se ambos estiverem disponiveis, Docker e usado por padrao. Para forcar uma engine:

```powershell
.\infra\setup\homepit-local.ps1 -Engine docker -Action start
.\infra\setup\homepit-local.ps1 -Engine podman -Action start
```

Use `-NoBuild` com `start` quando quiser apenas subir containers ja construidos.

## Enderecos locais

Depois do `start`, os principais endpoints ficam em:

- Web: http://localhost:3000
- API healthcheck: http://localhost:8080/health
- Supabase Studio: http://localhost:54323
- Postgres local: `localhost:54322`
- Evolution API: http://localhost:8081

## O que o script prepara

- Cria a rede externa `homepit_net`, exigida pelos `docker-compose.yml` do projeto.
- Cria `.env` locais quando eles ainda nao existem em:
  - `infra/supabase/.env`
  - `infra/evolution/.env`
  - `apps/api/.env`
  - `apps/web/.env`
- Sobe os recursos nesta ordem:
  - Supabase/Postgres
  - Evolution API
  - API HomePit
  - Web HomePit

Os arquivos `.env` gerados sao locais e estao ignorados pelo Git. Se voce ja tiver arquivos `.env`, o script mantem os valores existentes.

## Limpeza

`stop` remove containers, mas preserva volumes e dados.

`destroy` remove containers e volumes. Isso apaga o banco local do HomePit, o banco da Evolution e o Redis local da Evolution. Use apenas quando quiser recomecar do zero.

Para um passo a passo mais completo, veja [ONBOARDING.md](./ONBOARDING.md).
