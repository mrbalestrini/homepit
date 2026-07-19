# Receitas de automação

> Substitua sempre valores de exemplo pelos campos e `operationId` publicadas no OpenAPI. As operações abaixo mostram o fluxo; não definem schema além do contrato canônico.

## Criar um lançamento financeiro

1. Consulte `GET /space` para confirmar Espaço, modo e expiração.
2. Consulte no OpenAPI o `operationId` de criação de lançamento e o schema correspondente.
3. Envie a operação com `Authorization` e uma `Idempotency-Key` nova para esta intenção.
4. Guarde o identificadar e a `ETag` devolvidos para uma alteração futura. Em uma listagem, a ETag está em `items[].etag`; em criação e atualização, ela também está no cabeçalho HTTP `ETag`.

```python
import os
import uuid
import requests

response = requests.post(
    f"{os.environ['ORGANIZA_BASE_URL']}/api/integrations/v1/SEU_PATH_DO_OPENAPI",
    headers={
        "Authorization": f"Bearer {os.environ['ORGANIZA_INTEGRATION_TOKEN']}",
        "Idempotency-Key": str(uuid.uuid4()),
    },
    json={"campo": "consulte o schema no OpenAPI"},
    timeout=30,
)
response.raise_for_status()
```

## Percorrer lançamentos em TypeScript

```ts
const url = new URL(`${process.env.ORGANIZA_BASE_URL}/api/integrations/v1/finance/entries`);
url.searchParams.set("year", "2026");
url.searchParams.set("month", "7");

const response = await fetch(url, {
  headers: { Authorization: `Bearer ${process.env.ORGANIZA_INTEGRATION_TOKEN}` },
});
const page = await response.json();
for (const item of page.items) console.log(item.data, item.etag);
```

Se `nextCursor` existir, faça outra chamada com o mesmo filtro e `cursor=nextCursor`.

## Criar projeto e atividade

Use primeiro o catálogo de projetos ou o resource `organiza://projects/catalog` para localizar o núcleo/projeto correto. Em seguida, use as ferramentas MCP publicadas para criar projeto e atividade, ou as operações REST correspondentes. Não invente pendências de edição, conclusão ou exclusão: na v1 elas são apenas listadas e criadas.

## Atualizar sem sobrescrever outra pessoa

Leia o recurso e capture sua `ETag`. Envie a atualização com `If-Match` igual à versão lida. Se receber falha de precondição, leia novamente, apresente o conflito à pessoa usuária e só então refaça a alteração com dados reconciliados.
