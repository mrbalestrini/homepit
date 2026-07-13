# Feature: planos globais e assinaturas

## Contexto

O HomePit ja possui usuarios, casas, universos, projetos, prompts e uploads de imagem
privados, mas ainda nao possui um dominio comercial para limitar uso por plano nem uma
superficie de administracao global de assinaturas.

## Objetivo

Introduzir um catalogo fixo de planos editaveis pelo SuperAdmin, historico manual de
assinaturas por usuario e aplicacao centralizada de limites comerciais em criacao de casas,
universos, projetos e retencao de imagens privadas governadas por plano.

## Escopo

- Criar entidades persistidas para planos, assinaturas e ledger de imagens por plano.
- Resolver o plano efetivo do usuario a partir da assinatura ativa ou fallback para `Free`.
- Bloquear criacoes alem da cota de casas, universos por casa e projetos por universo.
- Degradar automaticamente imagens antigas de atividade e prompt acima da cota do plano.
- Expor APIs e DTOs para perfil do usuario, hub do SuperAdmin, planos e assinaturas.
- Criar a tela `/admin/platform` com abas `Usuarios`, `Planos` e `Assinaturas`.
- Mostrar o plano efetivo e seus limites na pagina de perfil.
- Atualizar OpenAPI, changelog e testes relacionados.

## Fora de escopo

- Integracao com gateway de pagamento, cobranca automatica ou webhooks.
- Criacao dinamica de novos tipos de plano alem dos cinco slugs fixos.
- Migracao retroativa do acervo para degradar imagens antigas ja acima de cota.
- Forcar rebaixamento de ownership existente quando um plano passa a suportar menos casas.
- Aplicar a cota de imagens a foto de perfil, universo, imagens institucionais ou SEO.

## Arquivos ou areas envolvidas

- `apps/api/src/HomePit.Domain/*`
- `apps/api/src/HomePit.Application/*`
- `apps/api/src/HomePit.Infrastructure/*`
- `apps/api/tests/*`
- `apps/web/src/app/admin/*`
- `apps/web/src/features/*`
- `apps/web/src/lib/api.ts`
- `contracts/openapi/homepit.v1.yaml`
- `CHANGELOG.md`

## Regras de negocio

- Os planos suportados nesta entrega sao `Free`, `Standard`, `Bronze`, `Silver` e `Gold`.
- O usuario sem assinatura ativa entra e permanece no plano `Free`.
- So uma assinatura pode ficar ativa para o mesmo usuario em uma mesma data.
- O plano `Free` nao cria casa, mas pode participar de casas existentes como `Member`.
- Criacao de casa continua promovendo o criador a `Owner`, sujeita ao limite do plano.
- O limite de universos vale por casa e o limite de projetos vale por universo, sempre com
  base no plano efetivo do usuario que esta criando.
- Atividades e prompts continuam aceitando upload mesmo acima da cota; o sistema preserva
  em qualidade original apenas as imagens mais recentes dentro do limite do plano.
- A degradacao substitui o binario original por um `WEBP` com no maximo `300px` e qualidade
  `30`, preservando o mesmo vinculo funcional da entidade.
- O texto descritivo da regra de imagens precisa ser derivado dos numeros atuais do plano,
  sem copy fixa divergente do catalogo.

## Riscos

- Banco:
  novas entidades, indices e constraints podem afetar startup e descobribilidade de migration.
- API/contrato:
  mudancas de DTO e novas rotas exigem sincronismo entre backend, frontend e OpenAPI.
- Autenticacao/autorizacao:
  SuperAdmin deixara de ser somente leitura em uma area nova e precisa permanecer restrito
  aos modulos comerciais globais.
- Frontend:
  a shell global do workspace e a sessao persistida precisarao acomodar novos dados de plano
  sem quebrar modulos existentes.
- Deploy/ambiente:
  seeds iniciais dos cinco planos precisam existir automaticamente em ambientes novos e
  antigos.

## Plano

1. Registrar a mudanca e introduzir o modelo persistido de planos, assinaturas e ledger.
2. Implementar seed, resolvedor de plano efetivo e servicos de aplicacao comercial.
3. Integrar os limites ao fluxo de casas, projetos, prompts e uploads governados.
4. Expor APIs, contrato e tipos frontend para perfil e hub do SuperAdmin.
5. Construir a UI administrativa tabulada e a secao de plano no perfil.
6. Validar com testes de unidade, integracao e frontend, e atualizar changelog.

## Testes

- xUnit para resolucao de plano, validacao de assinatura e enforcement de limites.
- xUnit/integracao para rotas de SuperAdmin, perfil do plano e uploads com degradacao.
- Vitest para hub `/admin/platform`, redirecionamento de `/admin/users` e secao de perfil.
- `npm run build` no frontend e validacao backend relevante.

## Criterios de aceite

- O catalogo padrao de cinco planos e persistido e editavel pelo SuperAdmin.
- O usuario comum enxerga seu plano efetivo, limites e vigencia quando houver assinatura.
- O sistema bloqueia criacoes acima das cotas do plano efetivo.
- Uploads governados acima da cota degradam as imagens mais antigas sem falhar o upload novo.
- O SuperAdmin consegue administrar usuarios, planos e assinaturas em um hub unico.

## Decisao final

Implementar um dominio comercial persistido e editavel pelo SuperAdmin, com enforcement
sincrono das cotas principais e degradacao automatica de imagens privadas governadas por
plano, preservando o comportamento funcional existente dos modulos da casa.
