# Projeto

## FATO OBSERVADO

- HomePit e um ambiente pessoal para organizar projetos de casa, demandas familiares,
  automacoes domesticas e prompts da familia.
- A interface se apresenta como operacao compartilhada de uma casa.
- Modulos implementados: autenticacao/perfil, casas e membros, projetos, banco de prompts,
  financeiro, gestao de numeros GSM, pagina institucional com CMS, armazenamento e
  notificacoes por WhatsApp.
- Projetos seguem `Universo > Projeto > Atividade > Pendencia`, com comentarios em atividades.
- O frontend possui landing publica em `/`, CMS em `/admin/institutional` e paginas internas
  para `/projects`, `/prompts`, `/household` e `/gsm`.
- O modulo financeiro ja esta implementado em `/finance`, com caixa, recorrencias,
  cartoes, patrimonio e filtros/selecao em massa na secao de compras.
- Supermercado ainda existe apenas como documentacao de modulo planejado.
- A versao em `apps/web/package.json`, `apps/web/package-lock.json` e `CHANGELOG.md` e
  `1.9.0`.
- Numeros GSM agora pertencem a uma casa, guardam titulo, numero normalizado com DDI,
  descricao opcional, prazo de recarga, historico de recargas, datas de aquisicao/ultima
  recarga e status compartilhado.
- Atividades expõem `CreatedAt` auditavel e aceitam `DueDate` opcional na API e no
  dashboard.
- Atividades agora aceitam uma imagem unica privada, com upload, leitura e exclusao
  protegidos no dashboard de projetos.
- Atividades agora expõem `CompletedAt`, preenchido ao entrar em `Concluido` e limpo ao
  voltar para uma etapa aberta; o dashboard oculta conclusões com mais de 30 dias até
  `Mostrar concluídas antigas` ser acionado.
- O workflow local de IA usa `AGENTS.md`, `.specs/active-change.md` e
  `.specs/shared/sources-of-truth.md` para roteamento e governanca documental.

## INFERÊNCIA

- O usuario principal parece ser uma familia ou pequeno grupo que compartilha uma casa,
  pois o dominio usa membros, papeis e dados isolados por `Household`.
- O produto ainda parece estar em evolucao de MVP, pois ha modulos planejados e parte da
  administracao do sistema esta descrita como futura.

## NÃO IDENTIFICADO

- Quantidade esperada de usuarios, casas ou volume de dados.
- Uso atual em producao, SLAs, suporte e politica de retencao.
- Prioridade e cronograma dos modulos planejados.
- Publico externo, modelo comercial ou requisitos reguladores.
