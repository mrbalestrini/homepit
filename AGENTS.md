# AGENTS.md

Este arquivo e o ponto de entrada para trabalho assistido por IA no HomePit.

## Como iniciar

1. Leia `.specs/README.md`.
2. Leia `.specs/active-change.md` para saber se existe uma mudanca ativa explicita.
3. Consulte somente os arquivos relevantes em `.specs/memory/` e `.specs/shared/`.
4. Use `.specs/shared/sources-of-truth.md` quando houver divergencia entre memoria,
   documentacao, scripts e codigo.
5. Se `active-change.md` apontar uma mudanca ativa, leia a pasta indicada em
   `.specs/changes/`; se nao houver mudanca ativa, crie uma nova pasta quando a tarefa
   exigir registro duravel.
6. Escolha em `.agents/skills/` somente a skill cujo gatilho corresponda a tarefa.
7. Antes de editar, apresente um plano proporcional ao impacto.

Nao assuma stack, comando, contrato ou regra marcada como `NÃO IDENTIFICADO`.
Nao infira mudanca ativa apenas pela presenca de pastas em `.specs/changes/`.

## Skills

Use skills sob demanda; nao carregue todas. Quando aplicavel, uma skill especifica prevalece
sobre orientacao generica. Use `architect`, `reviewer` e `tester` para trabalho transversal
ou quando nenhuma especializacao corresponder claramente.

## Mudancas sensiveis

Nao altere codigo sem plano quando houver impacto em banco, autenticacao, autorizacao,
deploy, variaveis de ambiente, dados, arquitetura ou regras de negocio. Registre decisoes
duraveis em `.specs/changes/<mudanca>/decisions.md`.

Em mudancas com EF Core e banco:
- Nao trate a criacao do arquivo de migration como suficiente. Se a migration for criada ou
  editada manualmente, preserve os metadados que permitem ao EF detecta-la automaticamente
  no startup e no deploy.
- Considere o fluxo real de deploy: no ambiente padrao a API pode depender de aplicacao
  automatica de migrations; uma migration "invisivel" para o EF pode deixar o banco
  defasado mesmo quando o log disser que nao ha pendencias.

Nao inclua segredos no repositorio e nao execute `commit`, `push` ou `git add` sem ordem
explicita.

## Versao e changelog

- A versao oficial deve ser igual em todos os `package.json`.
- So altere a versao quando o usuario disser `Suba a versao` ou quando o dia atual for
  diferente da data da ultima versao publicada no `CHANGELOG.md`.
- Se o dia atual for o mesmo da ultima versao publicada, mantenha a mesma versao e
  registre as mudancas no bloco existente.
- Use Semantic Versioning: `patch` para correcoes compativeis, `minor` para funcionalidades
  compativeis e `major` para quebras.
- Registre no `CHANGELOG.md` mudancas relevantes de comportamento, contrato, integracao,
  operacao ou entrega, preservando as secoes anteriores, mesmo quando a versao nao mudar.
- Mantenha `package-lock.json` alinhado quando a versao mudar.

## Referencias

- Estado ativo: `.specs/active-change.md`
- Memoria factual: `.specs/memory/`
- Regras compartilhadas: `.specs/shared/`
- Fontes de verdade: `.specs/shared/sources-of-truth.md`
- Mudancas e decisoes: `.specs/changes/`
- Mudancas encerradas: `.specs/archive/`
- Templates: `.specs/templates/`
- Skills sob demanda: `.agents/skills/`
