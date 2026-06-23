# Fontes de verdade

Use este arquivo quando houver divergencia entre memoria, documentacao, scripts e codigo.

## FATO OBSERVADO

- O estado ativo do workflow fica em `.specs/active-change.md`.
- Mudancas em andamento ficam em `.specs/changes/<id>/`; mudancas encerradas devem ir para
  `.specs/archive/`.
- A versao oficial de entrega do produto fica em `apps/web/package.json`.
- `apps/web/package-lock.json` e a entrada mais recente de `CHANGELOG.md` devem refletir a
  mesma versao oficial do produto.
- A versao do contrato/API fica em `contracts/openapi/homepit.v1.yaml`.
- `/api/system/info`, implementado em `apps/api/src/HomePit.Api/Program.cs`, deve expor a
  mesma versao do contrato/API observada no OpenAPI.
- O endpoint local esperado pelo fluxo guiado de setup fica em
  `infra/setup/homepit-local.ps1`, que define `NEXT_PUBLIC_API_BASE_URL`,
  `ASPNETCORE_URLS` e o healthcheck apresentado ao operador.
- O roteamento principal de trabalho assistido por IA fica em `AGENTS.md`.
- As responsabilidades e limites por especialidade ficam em `.agents/skills/*/SKILL.md`.

## INFERÊNCIA

- Divergencias entre `changes/` e `archive/` devem ser tratadas como problema de governanca
  do workflow, nao como estado valido por tempo indefinido.
- Versao de produto e versao de contrato podem evoluir separadamente, desde que cada trilha
  permaneça consistente dentro dos seus proprios artefatos.
- Quando scripts, docs e memoria divergirem, a correcao deve partir da fonte de verdade
  declarada aqui antes de atualizar resumos derivados.

## NÃO IDENTIFICADO

- Politica automatizada de publicacao que sincronize versao de produto e versao do contrato.
- Ferramenta oficial para gerar o OpenAPI a partir do codigo ou o codigo a partir do OpenAPI.
