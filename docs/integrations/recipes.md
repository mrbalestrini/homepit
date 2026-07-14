# Receitas de automação

> Substitua sempre valores de exemplo pelos campos e `operationId` publicados no OpenAPI. As operações abaixo mostram o fluxo; não definem schema além do contrato canônico.

## Criar um lançamento financeiro

1. Consulte `GET /space` para confirmar Casa, modo e expiração.
2. Consulte no OpenAPI o `operationId` de criação de lançamento e o schema correspondente.
3. Envie a operação com `Authorization` e uma `Idempotency-Key` nova para esta intenção.
4. Guarde o identificador e a `ETag` devolvidos para uma alteração futura.

```python
import os
import uuid
import requests

response = requests.post(
    f"{os.environ['HOMEPIT_BASE_URL']}/api/integrations/v1/SEU_PATH_DO_OPENAPI",
    headers={
        "Authorization": f"Bearer {os.environ['HOMEPIT_INTEGRATION_TOKEN']}",
        "Idempotency-Key": str(uuid.uuid4()),
    },
    json={"campo": "consulte o schema no OpenAPI"},
    timeout=30,
)
response.raise_for_status()
```

## Criar projeto e atividade

Use primeiro o catálogo de projetos ou o resource `homepit://projects/catalog` para localizar o universo/projeto correto. Em seguida, use os tools MCP publicados para criar projeto e atividade, ou as operações REST correspondentes. Não invente pendências de edição, conclusão ou exclusão: na v1 elas são apenas listadas e criadas.

## Atualizar sem sobrescrever outra pessoa

Leia o recurso e capture sua `ETag`. Envie a atualização com `If-Match` igual à versão lida. Se receber falha de precondição, leia novamente, apresente o conflito à pessoa usuária e só então refaça a alteração com dados reconciliados.
