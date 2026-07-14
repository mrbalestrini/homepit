# API REST externa

> Status: especificação v1. A URL e as operações finais são publicadas no OpenAPI externo quando a integração estiver habilitada.

## Autenticação

Envie a chave de integração somente no cabeçalho `Authorization`:

```bash
curl "$HOMEPIT_BASE_URL/api/integrations/v1/space" \
  -H "Authorization: Bearer $HOMEPIT_INTEGRATION_TOKEN"
```

Defina `HOMEPIT_BASE_URL` e `HOMEPIT_INTEGRATION_TOKEN` no ambiente ou no cofre de segredos da ferramenta. Nunca coloque a chave em URL, commit, argumento de linha de comando ou log.

## Convenções

- Base: `/api/integrations/v1`.
- `GET /space` descreve a Casa vinculada, papel efetivo, modo de acesso, expiração e versão.
- Listagens usam cursor; o tamanho padrão é 50 e o máximo é 200.
- Respostas de recursos mutáveis incluem `ETag`. Atualizar ou excluir exige `If-Match` com a versão recebida, evitando sobrescrever uma edição concorrente.
- Mutações aceitam `Idempotency-Key`. A mesma chave e mesmo payload retornam o resultado original por 90 dias; o mesmo valor com payload diferente retorna conflito.
- A conexão define a Casa. Não envie `X-Household-Id`.

## Permissões e superfície

Conexões `ReadOnly` só podem usar leituras. `ReadWrite` ainda respeita papel, autoria, cotas e demais regras vigentes do criador. A v1 prioriza Financeiro e Projetos; pendências permitem apenas listar/criar e imagens expõem apenas metadados.

Consulte o OpenAPI para os `operationId`, schemas e exemplos válidos antes de gerar uma requisição. Não derive nomes de campos a partir da interface web.
