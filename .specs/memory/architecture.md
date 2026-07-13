# Arquitetura

## FATO OBSERVADO

- Repositorio dividido em `apps/api`, `apps/web`, `contracts`, `docs` e `infra`.
- Backend ASP.NET Core `net10.0` descrito como monolito modular.
- Dependencias entre projetos: Domain sem referencias internas; Application depende de
  Domain; Infrastructure depende de Application e Domain; Api depende de Application e
  Infrastructure.
- Pontos de entrada: `Program.cs`, paginas Next.js em `src/app` e
  `infra/setup/homepit-local.ps1`.
- `Program.cs` registra DI, JWT, CORS, middleware de erros, migrations, storage e Minimal APIs.
- A regra de negocio fica principalmente em `AuthService`, `HouseholdService`,
  `ProjectService`, `PromptService`, `GsmNumberService` e `DailyDigestService`.
- `EffortPlanningService` concentra a capacidade semanal pessoal e o cálculo diário de relevância das atividades.
- `GsmNumberService` tambem concentra o historico de recargas do modulo GSM, mantendo o
  resumo `LastRechargeOn` derivado do lancamento mais recente.
- PostgreSQL e acessado por EF Core/Npgsql; o schema padrao e `homepit`.
- O frontend usa App Router, paginas finas, hooks-controladores por feature e cliente HTTP
  centralizado em `src/lib/api.ts`.
- O workspace interno agora possui um modulo dedicado `/finance`, com hook-controlador
  proprio e navegacao local entre caixa, cartoes e patrimonio.
- O workspace interno agora possui um modulo dedicado `/gsm`, com hook-controlador proprio
  e navegacao lateral compartilhada com os demais modulos da casa.
- A rota `/` renderiza uma pagina institucional publica a partir da API, enquanto
  `/admin/institutional` oferece um CMS separado para SuperAdmin.
- O conteudo institucional e global, usa o slug unico `home`, listas filhas ordenadas e
  imagens publicas armazenadas no mesmo object storage.
- O frontend persiste a casa ativa por usuario em `src/lib/household-selection.ts`,
  validando a selecao salva contra a sessao atual e limpando entradas obsoletas.
- O contrato de casas expõe `CreatedAt`, permitindo que o frontend escolha a casa mais
  recente quando a selecao salva nao for mais valida.
- Fluxo de autenticacao: cadastro/login em rotas publicas, JWT no grupo protegido `/api`,
  refresh rotacionado e sessao persistida pelo frontend. `/api/auth` e `/api/system/info`
  ficam fora desse grupo protegido; leitura da pagina e das imagens institucionais tambem
  e publica.
- Fluxo de tenancy: `X-Household-Id` seleciona a casa; uma unica casa pode ser inferida.
- Integracoes: Supabase Postgres, MinIO, Evolution API, Redis, Coolify e OpenAPI versionado.
- O worker de resumo consulta atividades abertas atribuidas ao membro e registra envios para
  evitar duplicidade por casa, membro, tipo e data.

## INFERÊNCIA

- Com migrations e criacao/verificacao de bucket habilitadas, a inicializacao da API depende
  da disponibilidade do banco e do MinIO antes de `RunAsync`.
- A concentracao de rotas, servicos e controladores de tela em arquivos extensos aumenta o
  impacto de mudancas nesses arquivos; a separacao futura deve preservar os modulos atuais.
- O OpenAPI parece ser mantido manualmente, pois ha operacoes implementadas ausentes no
  contrato e existe apenas um smoke test textual limitado.

## NÃO IDENTIFICADO

- Geracao automatica ou validacao completa do OpenAPI.
- Observabilidade alem de logs e healthcheck.
- Estrategia de cache, filas internas, transacoes distribuidas ou escalabilidade.
- Politica formal para criar novos modulos ou separar servicos.
