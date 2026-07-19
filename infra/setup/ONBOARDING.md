# Onboarding local

Este guia ajuda a subir o Organiza Club completo na maquina de desenvolvimento usando containers.

## Visao geral

O projeto e dividido em quatro recursos Docker Compose independentes:

- `infra/supabase`: Postgres/Supabase Studio local.
- `infra/evolution`: Evolution API, Postgres e Redis para WhatsApp.
- `apps/api`: API ASP.NET Core do Organiza Club.
- `apps/web`: aplicacao Next.js.

Todos entram na rede externa `organiza_club_net`, que e criada automaticamente pelo script de setup local.

## Primeiro start

1. Instale Docker Desktop ou Podman.
2. Garanta que a engine esteja em execucao.
3. A partir da raiz do projeto, rode:

```powershell
.\infra\setup\organiza-club-local.ps1 -Action start
```

Na primeira execucao, o script cria `.env` locais com secrets de desenvolvimento, cria a rede `organiza_club_net`, baixa imagens de infraestrutura e constroi as imagens da API e da web.

Quando terminar, acesse:

- Web: http://localhost:3000
- API: http://localhost:8080/health
- Supabase Studio: http://localhost:54323

## Arquivos de ambiente

O setup local cria estes arquivos apenas se eles ainda nao existirem:

- `infra/supabase/.env`
- `infra/evolution/.env`
- `apps/api/.env`
- `apps/web/.env`

Os valores sao apropriados para desenvolvimento local. Caso precise alterar portas, secrets ou URLs, edite os `.env` manualmente e execute `start` novamente.

Valores importantes:

- `POSTGRES_PASSWORD` em `infra/supabase/.env` deve bater com a senha usada em `ConnectionStrings__OrganizaClubDb` na API.
- `NEXT_PUBLIC_API_BASE_URL` em `apps/web/.env` deve apontar para a API exposta no host, por padrao `http://localhost:8080`.
- `Notifications__DailyDigestEnabled` fica desativado por padrao no setup local para evitar envio acidental de mensagens.

## Ciclo de uso

Subir ou atualizar a stack:

```powershell
.\infra\setup\organiza-club-local.ps1 -Action start
```

Parar containers preservando dados:

```powershell
.\infra\setup\organiza-club-local.ps1 -Action stop
```

Apagar containers e volumes locais:

```powershell
.\infra\setup\organiza-club-local.ps1 -Action destroy
```

Use `destroy` quando quiser limpar bancos e recomecar sem dados locais.
Para automacoes sem prompt de confirmacao, use `-Action destroy -Yes`.

## Portas padrao

- `3000`: web.
- `8080`: API.
- `54322`: Postgres/Supabase.
- `54323`: Supabase Studio.
- `54324`: Supabase Postgres Meta.
- `8081`: Evolution API.

Se alguma porta estiver ocupada, edite o `.env` correspondente antes de rodar `start`.

## Docker ou Podman

O script tenta detectar engines nesta ordem:

1. Docker com `docker compose`.
2. Podman com `podman compose`.
3. Podman com `podman-compose`.

Para forcar uma escolha:

```powershell
.\infra\setup\organiza-club-local.ps1 -Engine podman -Action start
```

No Podman em Windows/macOS, confirme que a machine esta iniciada antes de rodar o setup.

## Problemas comuns

Se a API iniciar antes do banco ficar pronto, o container pode reiniciar algumas vezes ate a migration conseguir conectar. Isso e esperado no primeiro start.

Se o PowerShell bloquear a execucao:

```powershell
powershell -ExecutionPolicy Bypass -File .\infra\setup\organiza-club-local.ps1 -Action start
```

Se os containers subirem com configuracoes antigas, confira se os `.env` ja existiam. O script nao sobrescreve arquivos existentes para preservar ajustes locais.
