# Pontos de atencao

## FATO OBSERVADO

- A versao oficial do produto observada em `apps/web/package.json`, `package-lock.json` e
  `CHANGELOG.md` e `1.6.0`, enquanto `/api/system/info` e OpenAPI informam `0.1.1` como
  versao atual do contrato/API.
- O OpenAPI nao lista seis operacoes implementadas: system info, update/delete de casa e
  upload/get/delete de imagem de universo.
- O onboarding fala em quatro recursos e omite MinIO em listas onde o setup usa cinco.
- `docs/architecture.md` diz que todos os recursos entram em `homepit_net`, mas o Compose da
  web nao declara essa rede.
- O Compose da API usa porta host padrao `5081`; setup, web e documentacao apontam para `8080`,
  e o script nao grava `API_PORT`.
- `npm install` aparece no README; a imagem usa `npm ci`.
- `npm test` passa, mas emite aviso de deprecacao da API CJS do Vite.
- Migrations podem ser aplicadas no startup; quando desativadas, migrations pendentes
  impedem a inicializacao.
- Em deploy automatico, uma migration manual sem metadados de descoberta do EF pode gerar
  falso positivo de "database is up to date" e a falha so aparece depois, em runtime,
  quando uma query toca colunas novas.
- A configuracao observada habilita criacao/verificacao do bucket no startup, com tentativas
  de repeticao antes da API iniciar.
- O setup nao sobrescreve `.env` existentes; configuracao antiga pode permanecer ativa.
- Membro removido fica inativo para preservar autoria e historico.
- Excluir universo remove seus projetos, mas desvincula prompts antes da exclusao.
- Excluir casa apaga comentarios explicitamente antes da cascata da casa.
- Excluir atividade, projeto ou universo precisa remover tambem as imagens privadas de
  atividades no object storage para evitar arquivos orphanados.
- Pendencias possuem somente listagem e criacao nas rotas observadas.
- Casa, universo e projeto usam dialogs de exclusao; atividade, membro, comentario e prompt
  ainda usam `window.confirm`.

## INFERÊNCIA

- Sem `API_PORT=8080` externo, o fluxo local pode publicar a API em `5081` enquanto a web
  tenta acessar `8080`; validar antes de confiar nos enderecos documentados.
- Alterar cascatas, autoria nula/inativa ou exclusoes sem revisar migrations e testes pode
  quebrar preservacao de historico.
- Ao investigar `errorMissingColumn` apos deploy, nao assumir primeiro que faltou aplicar
  migration manualmente; validar tambem se a migration foi descoberta pelo EF no assembly.
- Nao "corrigir" as divergencias de versao, OpenAPI, rede ou portas sem definir primeiro qual
  arquivo e a fonte de verdade.

## NÃO IDENTIFICADO

- Se as divergencias de infraestrutura ocorrem no ambiente atual ou sao compensadas fora do
  repositorio.
- Se o contrato OpenAPI e consumido por clientes externos.
- Plano para completar CRUD de pendencias e uniformizar confirmacoes destrutivas.
