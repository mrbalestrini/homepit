# Comandos

## FATO OBSERVADO

| Comando | Diretorio | Origem | Uso observado |
| --- | --- | --- | --- |
| `npm install` | `apps/web` | `README.md` | Instalar dependencias do frontend. |
| `npm run dev` | `apps/web` | `apps/web/package.json`, `README.md` | Iniciar Next.js em desenvolvimento. |
| `npm run build` | `apps/web` | `apps/web/package.json`, Dockerfile web | Gerar build do frontend. |
| `npm start` | `apps/web` | `apps/web/package.json` | Iniciar o build Next.js. |
| `npm run lint` | `apps/web` | `apps/web/package.json` | Executar ESLint. |
| `npm test` | `apps/web` | `apps/web/package.json` | Executar `vitest run`. |
| `npm ci` | `apps/web` | Dockerfile web | Instalar dependencias pelo lockfile na imagem. |
| `dotnet restore HomePit.sln` | `apps/api` | Dockerfile API | Restaurar a solucao durante o build da imagem. |
| `dotnet publish src/HomePit.Api/HomePit.Api.csproj -c Release -o /app/publish /p:UseAppHost=false` | `apps/api` | Dockerfile API | Publicar a API na imagem. |
| `.\infra\setup\homepit-local.ps1` | raiz | setup README/script | Abrir menu local. |
| `.\infra\setup\homepit-local.ps1 -Action start` | raiz | setup README/script | Preparar ambiente, construir API/web e subir cinco recursos. |
| `.\infra\setup\homepit-local.ps1 -Action stop` | raiz | setup README/script | Parar containers preservando volumes. |
| `.\infra\setup\homepit-local.ps1 -Action destroy` | raiz | setup README/script | Remover containers, volumes e a rede local. |
| `docker network create homepit_net` | host de deploy | `docs/coolify.md` | Criar a rede externa de deploy. |

- `start` aceita `-Engine docker|podman` e `-NoBuild`.
- O script tambem pode ser executado por `pwsh` ou com `-ExecutionPolicy Bypass`.
- O README do MinIO documenta comandos Compose especificos para recriar o servico.

## Observacoes de seguranca

- Nao executar `destroy`, `destroy -Yes` ou `docker compose down -v` sem confirmacao:
  removem bancos, Redis e objetos locais.
- `start` cria arquivos `.env` quando ausentes, constroi imagens e pode baixar imagens.
- A API pode aplicar migrations e criar/verificar bucket durante o startup.

## INFERÊNCIA

- Comandos usuais como `dotnet test`, `dotnet run`, `dotnet build` e `dotnet ef` devem ser
  validados antes de entrar no fluxo oficial; os projetos existem, mas esses comandos nao
  estao documentados no repositorio.

## NÃO IDENTIFICADO

- Comando oficial para executar, buildar ou testar o backend fora de containers.
- Comando de formatacao.
- Comando oficial para criar ou aplicar migrations via EF CLI.
- Comando unico de validacao de todo o repositorio.
- Comandos de CI/CD.
