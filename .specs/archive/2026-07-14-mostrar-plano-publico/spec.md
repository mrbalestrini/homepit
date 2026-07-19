# Feature: mostrar plano no catálogo público

## Contexto

O hub do SuperAdmin já gerencia o catálogo de planos, mas a vitrine pública ainda mostra
todos os planos de forma indiscriminada.

## Objetivo

Permitir que o SuperAdmin marque um plano para aparecer no catálogo público e esconder
os demais, mantendo visível para a pessoa usuária o plano atualmente efetivo da conta
mesmo quando ele não estiver marcada para exibição.

## Escopo

- Adicionar a flag persistida `showInCatalog` no domínio de planos.
- Filtrar o catálogo público de planos pela nova flag.
- Garantir que o plano efetivo da conta apareça na vitrine quando estiver oculto.
- Expor a nova opção na edição de planos do SuperAdmin.
- Atualizar contrato, frontend, testes e changelog.

## Fora de escopo

- Alterar a lógica comercial do plano efetivo.
- Mudar o fluxo de contratação além da listagem de planos.
- Exibir terminologia técnica de visibilidade na interface da pessoa usuária.

## Regras de negocio

- A flag começa habilitada nos planos existentes e nos seeds novos.
- O catálogo público mostra somente planos marcadas para exibição, com exceção do plano
  efetivo da conta autenticada quando ele estiver oculto.
- A criação e edição de assinaturas no hub do SuperAdmin continuam enxergando todos os
  planos.

## Riscos

- Banco: nova coluna precisa de migration com descoberta segura pelo EF.
- API/contrato: `PlanDefinition` muda e precisa ficar alinhado entre backend e frontend.
- Frontend: o catálogo de assinatura do perfil precisa incluir o plano atual quando ele
  estiver oculto.

## Plano

1. Atualizar domínio, DTOs, migration e seed.
2. Filtrar o catálogo público e manter a exceção do plano efetivo autenticada.
3. Ajustar o hub do SuperAdmin e o diálogo de assinatura no perfil.
4. Atualizar testes, OpenAPI e changelog.

## Testes

- xUnit para catálogo público, exceção do plano efetivo e update do flag.
- Vitest para o card de plano no SuperAdmin e para o plano atual oculto no perfil.
- `npm run build` em `apps/web`.

## Criterios de aceite

- O SuperAdmin consegue marcar e desmarcar `Mostrar plano`.
- O catálogo público só exibe planos marcadas, exceto o plano efetivo da conta autenticada
  quando ele estiver oculto.
- A pessoa usuária não vê termos técnicos de visibilidade na tela.

## Decisao final

Usar `showInCatalog` como nome interno da flag e iniciar todos os planos existentes e
novos seeds como exibidos no catálogo.
