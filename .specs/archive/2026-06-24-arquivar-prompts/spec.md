# Feature: arquivar prompts e ocultar imagens

## Contexto

O banco de prompts hoje permite criar, editar e excluir prompts, mas ainda não distingue
itens ativos de itens guardados para consulta posterior. A visualização também mostra
imagens sempre que existem.

## Objetivo

Permitir arquivar e desarquivar prompts, com uma visão dedicada para prompts arquivados,
sem remover a exclusão definitiva já existente. Também permitir esconder e mostrar
imagens no banco de prompts com preferência persistida no navegador.

## Escopo

- Persistir `IsArchived` no prompt.
- Expor endpoints para arquivar e desarquivar prompts.
- Filtrar a listagem padrão para retornar apenas prompts ativos.
- Expor uma visão de prompts arquivados no frontend.
- Adicionar um toggle de imagens com persistência em `localStorage`.
- Remover a preferência do `localStorage` quando a visão padrão for restaurada.

## Fora de escopo

- Alterar o comportamento de exclusão definitiva.
- Mudar regras de autoria, tenancy ou categorias.
- Aplicar a preferência de imagens fora do módulo de prompts.

## Arquivos ou areas envolvidas

- `apps/api/src/HomePit.Domain/Prompts/Prompt.cs`
- `apps/api/src/HomePit.Application/Prompts/*`
- `apps/api/src/HomePit.Api/Program.cs`
- `apps/api/src/HomePit.Infrastructure/Data/HomePitDbContext.cs`
- `apps/api/src/HomePit.Infrastructure/Migrations/*`
- `apps/api/tests/*`
- `apps/web/src/features/prompts/*`
- `apps/web/src/lib/api.ts`
- `apps/web/src/features/projects/project-dashboard.constants.ts`
- `contracts/openapi/homepit.v1.yaml`

## Regras de negocio

- `Arquivar` é reversível e preserva o prompt inteiro.
- A listagem padrão mostra apenas prompts ativos.
- A visão arquivada mostra apenas prompts arquivados.
- A ação de arquivar/desarquivar usa a mesma regra de permissão do gerenciamento do
  prompt.
- Imagens ocultas não devem ser buscadas nem renderizadas no card ou no detalhe.
- Quando a visão padrão de imagens é restaurada, não deve existir chave persistida para
  essa preferência.

## Riscos

- Banco: nova coluna booleana e novos índices precisam permanecer compatíveis com o EF.
- API/contrato: novos endpoints e novos campos precisam permanecer alinhados no OpenAPI e
  nos tipos do frontend.
- Frontend: a troca entre ativos e arquivados deve não quebrar filtros, paginação e
  masonry.
- Persistência local: a preferência de imagens deve ser lida de forma segura e limpa.

## Plano

1. Adicionar `IsArchived` ao domínio, ao mapeamento e à migration.
2. Atualizar o serviço de prompts, o contrato e os endpoints para arquivar/desarquivar e
   filtrar prompts arquivados.
3. Introduzir a preferência de imagens no hook do banco de prompts e reutilizá-la no card,
   no detalhe e no cálculo do masonry.
4. Atualizar os testes backend, frontend e de contrato.
5. Registrar a mudança no changelog e na memória factual.

## Testes

- Testes de serviço cobrindo arquivar, desarquivar e listagem por estado.
- Testes de integração cobrindo os novos endpoints e filtros.
- Vitest cobrindo toggle de imagens, remoção da preferência e visualização arquivada.

## Criterios de aceite

- O usuário consegue arquivar e desarquivar prompts sem perder dados.
- A visão padrão não mostra prompts arquivados.
- A visão arquivada existe e permite voltar ao estado ativo.
- O botão de imagens esconde e mostra o conteúdo visual, persistindo a escolha apenas quando
  estiver oculto.

## Decisao final

Adicionar arquivamento reversível ao prompt bank e uma preferência local para ocultar
imagens, mantendo a exclusão definitiva atual intacta.
