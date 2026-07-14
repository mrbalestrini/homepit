# Feature: perfil com assinatura modal e plano popular

## Contexto

A página `/profile` já exibe o plano efetivo, o catálogo público de planos e a ação de solicitar assinatura, mas o fluxo atual mistura contexto de uso, CTA e preços na mesma coluna. O catálogo de planos também ainda não expõe um destaque persistente para o plano popular.

## Objetivo

Reorganizar a tela de perfil para destacar o plano atual, abrir a solicitação de assinatura em uma modal e permitir que o SuperAdmin marque um plano como popular.

## Escopo

- Reordenar a página de perfil para mostrar `Plano` antes do fluxo de assinatura.
- Remover os valores da seção de plano atual e manter apenas o resumo de uso/estado.
- Transformar a solicitação de assinatura em modal acionada por botão.
- Expor e editar o destaque popular do plano no hub do SuperAdmin.
- Atualizar contrato, API, OpenAPI, testes e changelog.

## Fora de escopo

- Alterar o fluxo de compra/contratação além do ponto de entrada na interface.
- Mudar regras comerciais de cálculo de plano efetivo.
- Alterar a versão do produto ou do contrato sem necessidade funcional.

## Arquivos ou areas envolvidas

- `apps/web/src/features/profile/profile-page.tsx`
- `apps/web/src/features/platform/platform-admin-page.tsx`
- `apps/web/src/lib/api.ts`
- `apps/api/src/HomePit.Application/Plans/*`
- `apps/api/src/HomePit.Domain/Plans/*`
- `apps/api/src/HomePit.Infrastructure/*`
- `contracts/openapi/homepit.v1.yaml`

## Regras de negocio

- O destaque popular é um booleano simples por plano.
- O Gold começa destacado como popular.
- O SuperAdmin pode trocar ou remover o destaque popular.
- O plano atual do cliente continua visível no perfil, mas sem exibir preços nessa seção.

## Riscos

- Banco: precisa de migration para o novo campo `IsPopular`.
- API/contrato: `PlanDefinition` muda e precisa de alinhamento entre backend e frontend.
- Autenticacao/autorizacao: sem mudança de escopo.
- Frontend: a tela de perfil concentra o fluxo da modal e precisa de regressão visual/teste.
- Deploy/ambiente: migration precisa continuar descoberta pelo EF no startup.

## Plano

1. Atualizar modelo persistido, migration e seeds para `IsPopular`.
2. Propagar `isPopular` no contrato e nas telas de perfil e SuperAdmin.
3. Reorganizar o perfil com modal de assinatura e novo destaque visual.
4. Atualizar testes e changelog.

## Testes

- Frontend: `apps/web` com Vitest e `npm run build`.
- Backend: testes de serviço e integração de planos.
- Validação manual da modal e do destaque popular no admin.

## Criterios de aceite

- A seção `Plano` aparece antes da assinatura na página de perfil.
- O plano atual é mostrado sem preços.
- A assinatura vira uma modal acionada por botão.
- O plano popular aparece destacado na modal e pode ser alterado pelo SuperAdmin.
- O texto de destino automático foi removido da UI.

## Decisao final

- O destaque popular será representado por `isPopular`.
- O Gold entra como popular inicial.
- O fluxo de contato continua usando WhatsApp ou e-mail conforme a configuração pública.
