# Feature: modulo gsm

## Contexto

O OrganizaClub ainda nao possui um modulo dedicada para organizar numeros GSM compartilhados da
espaço, embora o workspace ja preveja a expansao para novos modulos internos.

## Objetivo

Permitir cadastrar, visualizar, editar e excluir numeros GSM com dados de contexto,
status, prazo de recarga e historico operacional de recargas.

## Escopo

- Persistir numeros GSM por space com autoria e tenancy.
- Expor CRUD completo em `/api/gsm-numbers`.
- Expor CRUD do historico de recargas em `/api/gsm-numbers/{id}/recharges`.
- Validar e normalizar numeros com DDI opcional e DDD obrigatorio.
- Adicionar a pagina interna `/gsm` ao workspace compartilhado.
- Incluir plano da linha, custo mensal opcional e `DaysWithoutRecharge` nos cadastros GSM.
- Exibir os numeros cadastrados em tabela responsiva no desktop e cards no mobile.
- Exibir a proxima recarga calculada e o destaque de atraso no frontend.
- Cobrir o fluxo com testes backend e frontend.

## Fora de escopo

- Alertas automaticos, importacao em massa ou filtros avancadas.
- Suporte a formatos de telefone fora de 11 ou 13 digitos.
- Restringir DDI explicito a um pais especifico.

## Arquivos ou areas envolvidas

- `apps/api/src/OrganizaClub.Domain/*`
- `apps/api/src/OrganizaClub.Application/*`
- `apps/api/src/OrganizaClub.Api/Program.cs`
- `apps/api/src/OrganizaClub.Infrastructure/Data/OrganizaClubDbContext.cs`
- `apps/api/src/OrganizaClub.Infrastructure/Migrations/*`
- `apps/api/tests/*`
- `apps/web/src/app/*`
- `apps/web/src/features/gsm/*`
- `apps/web/src/features/workspace/organiza-club-workspace-shell.tsx`
- `apps/web/src/lib/api.ts`
- `contracts/openapi/organiza-club.v1.yaml`

## Regras de negocio

- O modulo e compartilhado pela space.
- `Owner` e `Admin` gerenciam todos os numeros; `Member` gerencia apenas os numeros que
  criou; `SuperAdmin` continua com acesso somente leitura.
- `DaysWithoutRecharge` e opcional e deve ser um inteiro positivo quando informado.
- O numero deve aceitar apenas 11 ou 13 digitos.
- Entrada com 11 digitos e salva como `55 + DDD + numero`.
- Entrada com 13 digitos preserva o DDI explicito informado.
- O mesmo numero nao pode existir duas vezes na mesma space.
- `Title` e `AcquiredOn` sao obrigatorios.
- `LastRechargeOn` e um resumo derivado do historico de recargas; nao e editado
  diretamente.
- Lancamentos de recarga exigem data, valor positivo e observacao opcional; a data nao pode
  ficar no futuro nem ser anterior a `AcquiredOn`.
- `Status` aceita `Ativo`, `Inativo` e `Abandonado`.
- `Plan` aceita `PrePago` e `PosPago`.
- `MonthlyCost` e opcional, deve ser armazenado com precisao monetaria.

## Riscos

- Banco: nova entidade, indice unico e migracao precisam permanecer descobriveis pelo EF.
- API/contrato: novo recurso com DTOs e endpoints precisa ser refletido no OpenAPI manual.
- Autenticacao/autorizacao: o CRUD precisa respeitar a space ativa e autoria.
- Frontend: nova pagina e novo modulo no shell sem conflitar com mudancas locais em
  andamento no dashboard de projetos.
- UX: a troca de tabela para cards no mobile precisa manter legibilidade e acoes acessiveis.
- Deploy/ambiente: migration manual sem metadados validos pode passar despercebida.

## Plano

1. Adicionar a entidade GSM, enum de status, DTOs, servico e mapeamento EF.
2. Criar a migracao e expor o CRUD pela API protegida.
3. Implementar a pagina `/gsm`, o hook de feature, a mascara, o historico e a proxima
   recarga.
4. Cobrir comportamento e permissoes com testes backend e frontend.
5. Atualizar memoria, changelog e versao do frontend ao concluir.

## Testes

- Testes de servico para normalizacao, validacao, permissao e unicidade.
- Testes de integracao para CRUD e tenancy via endpoints.
- Vitest para utilitarios de mascara/projecao e a tela do modulo GSM.

## Criterios de aceite

- O usuario consegue cadastrar, editar, listar e excluir numeros GSM na space ativa.
- O formulario impede formatos invalidos e normaliza o numero salvo.
- A lista mostra status, plano, custo mensal, datas, proxima recarga e atraso quando
  existir prazo configurado.
- O historico de recargas pode ser criado, editado e excluido.
- Permissoes, contrato e migration refletem o novo modulo.

## Decisao final

Adicionar um novo modulo interno `/gsm`, compartilhado por space, com CRUD completo de
numeros GSM, historico de recargas, validacao forte de numero e projecao de proxima recarga
no frontend.
