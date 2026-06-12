# Tarefas

- [x] Atualizar o `AGENTS.md` como ponto de entrada.
- [x] Criar memoria factual em `.specs/memory/`.
- [x] Criar referencias compartilhadas em `.specs/shared/`.
- [x] Registrar descoberta e decisoes desta mudanca.
- [x] Criar templates minimos.
- [x] Criar arquivo de orientacao do archive.
- [x] Criar as skills `architect`, `reviewer` e `tester`.
- [x] Reler documentacao, configuracoes, estrutura, codigo e testes representativos.
- [x] Refinar os arquivos de `.specs/memory/`.
- [x] Refinar glossario, regras de dominio e padroes tecnicos.
- [x] Separar fatos, inferencias e itens nao identificados.
- [x] Registrar inconsistencias sem eleger uma fonte de verdade.
- [x] Registrar as decisoes da etapa de refinamento.
- [x] Refinar `architect`, `reviewer` e `tester` no formato operacional padrao.
- [x] Criar skills `frontend`, `backend`, `database`, `devops`, `auth` e `qa`.
- [x] Registrar skills nao criadas e seus motivos.
- [x] Atualizar o `AGENTS.md` para selecao de skills sob demanda.
- [x] Validar estruturalmente todas as skills.
- [x] Auditar `AGENTS.md`, memoria, regras compartilhadas, templates e skills.
- [x] Conferir stack, versoes, comandos, testes e arquivos de ambiente contra o repositorio.
- [x] Completar os cinco templates com os campos minimos exigidos.
- [x] Documentar o fluxo recomendado em `.specs/README.md`.
- [x] Registrar o diretorio de trabalho dos comandos identificados.
- [x] Corrigir referencias e procedimento operacional das skills.
- [x] Verificar ausencia de valores sensiveis na estrutura de IA.
- [ ] Revisar a memoria com o dono do projeto.

## Validacoes executadas em 2026-06-12

- `npm run lint`, em `apps/web`: concluido com exit code 0, sem erros.
- `npm test`, em `apps/web`: 9 arquivos e 25 testes aprovados; aviso de deprecacao da API
  CJS do Vite.
- `$validator = Join-Path $env:USERPROFILE
  '.codex\skills\.system\skill-creator\scripts\quick_validate.py'; Get-ChildItem
  .agents/skills -Directory | ForEach-Object { python $validator $_.FullName }`: as 9 skills
  foram consideradas validas.
- Checagens PowerShell de frontmatter, secoes, templates, marcadores, whitespace e newline:
  aprovadas.
- Varredura PowerShell com `Select-String` para chaves privadas, tokens comuns e atribuicoes
  sensiveis em `AGENTS.md`, `.specs/` e `.agents/`: nenhum valor encontrado.
