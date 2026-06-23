# Bugfix: imagem de prompt com `X-Household-Id`

## Contexto e problema observado

Ao abrir a imagem protegida de um prompt, a API respondeu `400` com a mensagem
`Informe X-Household-Id para escolher a casa.`. O endpoint consultado foi
`GET /api/prompts/{id}/image`.

## Objetivo

Garantir que a imagem protegida do prompt seja recuperada com a casa ativa explicitada
na requisição do frontend.

## Evidencia

- O contrato da API exige `X-Household-Id` quando o usuario possui mais de uma casa.
- O fluxo de imagem do prompt no frontend usa `apiFetchBlob` sem `householdId`.
- O card do prompt e o modal de detalhe compartilham o mesmo componente de imagem.

## Causa

O hook de imagem protegida do prompt nao envia `X-Household-Id` ao chamar
`/api/prompts/{id}/image`, entao a API nao consegue resolver a casa ativa em contextos com
mais de uma associacao.

## Escopo

- Ajustar o carregamento de imagem protegida de prompts no frontend.
- Propagar `householdId` para o card e para o modal de detalhe.
- Cobrir o comportamento com teste unitario.

## Fora de escopo

- Alterar o contrato da API.
- Mudar a regra de tenancy do backend.
- Ajustar o armazenamento da imagem no banco ou no object storage.

## Arquivos ou areas envolvidas

- `apps/web/src/features/prompts/prompt-bank-workspace.tsx`
- `apps/web/src/features/prompts/prompt-bank-workspace.test.tsx`
- `CHANGELOG.md`

## Riscos

- Baixo: a mudanca e local no frontend.
- Se algum ponto da interface nao receber `householdId`, a imagem continuara sem carregar
  em vez de gerar 400.

## Plano de correcao

1. Fazer o hook de imagem protegida receber `householdId`.
2. Passar `householdId` para os usos do card e do detalhe.
3. Adicionar teste que valide a chamada com `householdId`.
4. Registrar a correção no changelog.

## Testes e validacao

- `vitest` para o arquivo de prompts do frontend.
- Verificacao manual da abertura de um prompt com imagem em uma conta com multiplas casas.

## Criterios de aceite

- A imagem de prompt deixa de falhar com `400` por falta de `X-Household-Id`.
- O card e o detalhe do prompt continuam exibindo a imagem protegida.
- O teste cobre o envio do `householdId`.

## Decisao final

Enviar `householdId` junto ao fetch da imagem protegida de prompt no frontend.
