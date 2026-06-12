# Decisoes

1. Usar `AGENTS.md` como ponto de entrada curto para o Codex.
2. Separar fatos observados em `.specs/memory/` de regras reutilizaveis em `.specs/shared/`.
3. Registrar cada mudanca em `.specs/changes/<id>-<nome>/`.
4. Manter templates pequenos, sem duplicar o contrato OpenAPI ou a documentacao operacional.
5. Na etapa inicial, criar somente as skills `architect`, `reviewer` e `tester`, cada uma
   com responsabilidade unica.
6. Carregar skills apenas quando a descricao corresponder a tarefa.
7. Preservar no `AGENTS.md` as regras existentes de versao e changelog.
8. Separar todo conteudo refinado em `FATO OBSERVADO`, `INFERÊNCIA` e `NÃO IDENTIFICADO`.
9. Usar codigo, configuracao, testes e documentacao como evidencias, registrando divergencias
   sem escolher uma fonte de verdade por suposicao.
10. Nao copiar valores de configuracao sensivel; registrar apenas a existencia e o risco.
11. Manter a memoria curta e apontar para os arquivos originais quando o detalhe completo
    continuar sendo necessario.
12. Tratar riscos derivados das evidencias como inferencias ate validacao humana.
13. Refinar `architect`, `reviewer` e `tester` para arquitetura, revisao e planejamento de
    testes, mantendo uma responsabilidade principal por skill.
14. Criar `frontend` por haver Next.js, React, TypeScript, Tailwind, hooks de feature e Vitest.
15. Criar `backend` por haver ASP.NET Core Minimal APIs, camadas e servicos de Application.
16. Criar `database` por haver PostgreSQL, EF Core, Fluent API e migrations versionadas.
17. Criar `devops` por haver Dockerfiles, Compose, Coolify, setup PowerShell e ambientes.
18. Criar `auth` por haver JWT, refresh tokens, PBKDF2, papeis, tenancy e SuperAdmin.
19. Criar `qa` por haver suites estruturadas com xUnit, WebApplicationFactory e Vitest;
    `tester` planeja cenarios e `qa` executa/verifica.
20. Nao criar `mobile`: nenhum projeto ou framework mobile foi identificado.
21. Nao criar skill especifica de CI/CD: `.github/workflows` nao existe e os comandos de CI
    permanecem `NÃO IDENTIFICADO`; deploy e containers ficam em `devops`.
22. Nao criar skills de financeiro ou supermercado: esses modulos estao apenas planejados.
23. Selecionar skills sob demanda; quando o gatilho for claro, a skill especifica prevalece
    sobre as skills genericas.
24. Manter a memoria atual: a revisao final nao encontrou excesso ou duplicidade que
    justificasse remover fatos uteis para tarefas futuras.
25. Padronizar os templates com contexto, objetivo, escopo, fora de escopo, areas, riscos,
    plano, testes, criterios de aceite e decisao final.
26. Exigir autorizacao explicita antes de executar DDL ou DML em mudancas de banco.
27. Registrar o diretorio de trabalho junto aos comandos para evitar execucao na raiz
    incorreta.
28. Usar nas skills o procedimento neutro `Atuar somente dentro da responsabilidade e do
    escopo da skill`, preservando os limites de papeis que nao implementam codigo.
29. Nao criar novas skills: todas as skills especificas continuam sustentadas pela stack
    observada e possuem responsabilidade distinta.
30. Nao executar build nesta validacao documental; ele cria artefatos e nao valida a
    estrutura de IA. Testes e lint do frontend podem ser executados como validacao segura.
