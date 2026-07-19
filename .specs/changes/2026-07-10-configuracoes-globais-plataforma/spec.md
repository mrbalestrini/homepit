# Feature: configuracoes globais da plataforma

## Contexto

O hub global `/admin/platform` ja concentra gestao de usuarios, planos e assinaturas do
`SuperAdmin`, mas ainda nao possui um lugar unico para dados institucionais e de contato
da plataforma.

## Objetivo

Adicionar uma aba de configuracoes na tela da Plataforma Organiza Club para manter dados de
contato, telefone de gestao, instagram e endereco institucional que podera alimentar a
landing page no futuro.

## Escopo

- Criar persistencia e API para configuracoes globais da plataforma.
- Expor os campos organizados por assunto na aba `Configuracoes` do hub do `SuperAdmin`.
- Permitir que os dados de contato e endereco sejam editados e salvos pelo `SuperAdmin`.
- Preparar o retorno para uso futuro na landing page, incluindo regra de visibilidade do
  endereco apenas quando os campos obrigatorios estiverem completos.

## Fora de escopo

- Alterar a landing page para exibir os novos dados nesta entrega.
- Configurar canais de notificacao de plataforma para o telefone de gestao.
- Trocar o modelo atual de planos e assinaturas globais.

## Arquivos ou areas envolvidas

- `apps/api/src/OrganizaClub.Domain/*`
- `apps/api/src/OrganizaClub.Application/*`
- `apps/api/src/OrganizaClub.Infrastructure/*`
- `apps/api/src/OrganizaClub.Api/*`
- `apps/api/tests/*`
- `apps/web/src/features/platform/*`
- `apps/web/src/lib/api.ts`
- `contracts/openapi/organiza-club.v1.yaml`
- `CHANGELOG.md`

## Regras de negocio

- `Telefone Contato` e `E-mail Contato` representam o canal publico da landing.
- `Telefone Gestão` representa o canal interno de notificacoes da plataforma.
- O endereco so deve ser considerado publicavel quando todos os campos de endereco
  estiverem preenchidos.
- Os campos devem aparecer agrupados por assunto, nao em uma lista unica.

## Riscos

- Banco:
  nova entidade e migration podem exigir validacao de startup e descobribilidade.
- API/contrato:
  novas rotas e DTOs precisam ser refletidas no frontend e no OpenAPI.
- Autenticacao/autorizacao:
  a superficie deve continuar restrita ao `SuperAdmin`.
- Frontend:
  a nova aba precisa respeitar o padrao visual e funcional do hub existente.
- Deploy/ambiente:
  ambientes novos precisam receber um registro padrao para nao quebrar a tela.

## Plano

1. Criar a entidade e o service de configuracoes globais.
2. Expor rotas e DTOs no backend com seed/default seguro.
3. Adicionar a aba no hub de plataforma com agrupamento por assunto.
4. Atualizar testes, OpenAPI e changelog.

## Testes

- xUnit para leitura e salvamento das configuracoes globais.
- Vitest para tabs, carregamento e persistencia da nova aba.
- Build do frontend para validar tipos e integracao.

## Criterios de aceite

- O `SuperAdmin` ve uma nova aba `Configuracoes` no hub `/admin/platform`.
- Os campos pedidos sao exibidos e agrupados por assunto.
- Os dados podem ser carregados e salvos pela API.
- O endereco fica marcada como visivel somente quando todos os campos obrigatorios estao
  preenchidos.

## Decisao final

Persistir configuracoes globais separadas do conteudo institucional, com acesso exclusivo
ao `SuperAdmin`, para centralizar os dados de contato e infraestrutura da plataforma.
