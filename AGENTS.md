# AGENTS.md

Este arquivo e o ponto de entrada para trabalho assistido por IA no HomePit.

## Como iniciar

1. Leia `.specs/README.md`.
2. Consulte somente os arquivos relevantes em `.specs/memory/` e `.specs/shared/`.
3. Verifique se existe uma mudanca ativa em `.specs/changes/`.
4. Escolha em `.agents/skills/` somente a skill cujo gatilho corresponda a tarefa.
5. Antes de editar, apresente um plano proporcional ao impacto.

Nao assuma stack, comando, contrato ou regra marcada como `NÃO IDENTIFICADO`.

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
- So altere a versao quando o usuario disser `Suba a versao`, salvo correcao de inconsistencia.
- Use Semantic Versioning: `patch` para correcoes compativeis, `minor` para funcionalidades
  compativeis e `major` para quebras.
- Registre no `CHANGELOG.md` mudancas relevantes de comportamento, contrato, integracao,
  operacao ou entrega, preservando as secoes anteriores.
- Mantenha `package-lock.json` alinhado quando a versao mudar.

## Referencias

- Memoria factual: `.specs/memory/`
- Regras compartilhadas: `.specs/shared/`
- Mudancas e decisoes: `.specs/changes/`
- Templates: `.specs/templates/`
- Skills sob demanda: `.agents/skills/`
