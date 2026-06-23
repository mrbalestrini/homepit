# Feature: imagem em atividade

## Contexto

O modulo de projetos ja permite texto, prazo, responsavel, comentarios e pendencias para
`Activity`, mas nao possui suporte a imagem vinculada a uma atividade.

## Objetivo

Permitir anexar uma imagem a uma atividade, visualizar a imagem no dashboard e remover o
anexo depois.

## Escopo

- Persistir metadados de imagem em `Activity`.
- Expor upload, leitura e exclusao da imagem via API protegida.
- Mostrar a imagem na edicao e no detalhe da atividade.
- Refletir a imagem nos cards do dashboard quando houver anexo.
- Cobrir o fluxo com testes backend e frontend.

## Fora de escopo

- Galeria com multiplas imagens por atividade.
- Edicao de imagem com recorte ou processamento especial.
- Imagem publica sem autenticacao.
- Mudanca nas regras de autoria ou tenancy de projetos.

## Arquivos ou areas envolvidas

- `apps/api/src/HomePit.Domain/Projects/Activity.cs`
- `apps/api/src/HomePit.Application/Projects/ProjectDtos.cs`
- `apps/api/src/HomePit.Application/Projects/ProjectService.cs`
- `apps/api/src/HomePit.Api/Program.cs`
- `apps/api/src/HomePit.Infrastructure/Data/HomePitDbContext.cs`
- `apps/api/src/HomePit.Infrastructure/Migrations/*`
- `apps/web/src/lib/api.ts`
- `apps/web/src/features/projects/*`
- `contracts/openapi/homepit.v1.yaml`

## Regras de negocio

- Cada atividade pode ter no maximo uma imagem ativa.
- A imagem aceita JPG, PNG ou WEBP e tem limite de 5 MB.
- O upload substitui o anexo anterior e a exclusao remove a imagem da atividade.
- A leitura da imagem continua protegida pela sessao e pela casa ativa.
- Ao excluir atividade, projeto ou universo, o binario da imagem deve ser removido junto
  com os registros correspondentes.

## Riscos

- Banco: nova migracao e novo mapeamento precisam permanecer descobriveis pelo EF.
- API/contrato: `Activity` passa a expor novos campos e novos endpoints.
- Autenticacao/autorizacao: o upload e a leitura precisam respeitar a casa ativa e a
  autoria do conteudo.
- Frontend: o editor e o detalhe da atividade ganham fluxo de upload e remocao.
- Deploy/ambiente: uma migracao manual sem metadados de descoberta pode passar despercebida
  no startup automatico.

## Plano

1. Adicionar metadados de imagem ao dominio, ao contrato e ao mapeamento EF.
2. Implementar upload, leitura e exclusao no servico e nas rotas de atividade.
3. Atualizar o dashboard para editar, exibir e remover a imagem.
4. Cobrir o comportamento com testes e atualizar a documentacao observavel.

## Testes

- Servico de projetos com upload, leitura, remocao e limpeza em exclusoes.
- Endpoints de atividade com multipart/form-data e retorno binario protegido.
- Vitest do dashboard para preview, upload e remoção da imagem.

## Criterios de aceite

- A atividade pode receber uma imagem e remove-la depois.
- O card e o detalhe da atividade mostram a imagem quando ela existe.
- A exclusao de atividade, projeto ou universo nao deixa binario orphanado no storage.
- O contrato e a migration refletem os novos campos.

## Decisao final

Adicionar uma imagem unica por atividade, armazenada em object storage privado e
gerenciada pelo dashboard de projetos.
