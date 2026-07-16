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
- Todas as listagens respondem com `{ "items": [...], "nextCursor": "..." }`. Informe `limit` (padrão 50, máximo 200) e passe o `nextCursor` recebido como `cursor` para buscar a página seguinte. O cursor é opaco e só vale para a mesma rota e os mesmos filtros.
- Cada item mutável vem como `{ "data": { ... }, "etag": "\"...\"" }`. Criações e atualizações também enviam a mesma versão no cabeçalho `ETag`.
- Atualizar, alterar status ou excluir exige `If-Match` com a ETag recebida, evitando sobrescrever uma edição concorrente. A falta do cabeçalho retorna `428`; uma ETag malformada retorna `400`; uma versão antiga retorna `412`.
- Mutações aceitam `Idempotency-Key`. A mesma chave e mesmo payload retornam o resultado original por 90 dias; o mesmo valor com payload diferente retorna conflito.
- A conexão define a Casa. Não envie `X-Household-Id`.

## Permissões e superfície

Conexões `ReadOnly` só podem usar leituras. `ReadWrite` ainda respeita papel, autoria, cotas e demais regras vigentes do criador. A v1 prioriza Financeiro e Projetos; pendências permitem apenas listar/criar e imagens expõem apenas metadados.

Consulte o OpenAPI para os `operationId`, schemas e exemplos válidos antes de gerar uma requisição. Não derive nomes de campos a partir da interface web.

## Paginar com segurança

```bash
curl "$HOMEPIT_BASE_URL/api/integrations/v1/finance/entries?year=2026&month=7&limit=50" \
  -H "Authorization: Bearer $HOMEPIT_INTEGRATION_TOKEN"
```

Use somente o valor de `nextCursor` devolvido pela API; não tente montá-lo ou reutilizá-lo em outra consulta.

## Atualizar com ETag

```powershell
$headers = @{
  Authorization = "Bearer $env:HOMEPIT_INTEGRATION_TOKEN"
  'If-Match' = '"ETAG_RECEBIDA_DA_LISTAGEM"'
}

Invoke-RestMethod "$env:HOMEPIT_BASE_URL/api/integrations/v1/finance/categories/SEU_ID" `
  -Method Put -Headers $headers -ContentType 'application/json' `
  -Body '{"name":"Moradia"}'
```

Ao receber `412`, leia o recurso novamente, decida como reconciliar a alteração e repita com a nova ETag. Nunca tente remover as aspas da ETag.
