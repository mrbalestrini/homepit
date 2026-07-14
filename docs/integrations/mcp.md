# MCP: agentes e automações

> Status: especificação v1. O MCP remoto só estará disponível com `Mcp:Enabled` após a configuração segura de OAuth e HTTPS.

## MCP remoto

O endpoint remoto é `/mcp`, com transporte Streamable HTTP stateless e OAuth 2.1. Clientes MCP usam Authorization Code com PKCE S256, discovery e registro dinâmico de cliente quando suportado. A pessoa usuária autoriza uma Casa, modo de acesso e validade na tela de consentimento do HomePit.

Os tools seguem `finance_<verbo>_<recurso>` e `projects_<verbo>_<recurso>`, derivados dos `operationId` do OpenAPI. Conexões somente leitura não recebem tools de escrita e o servidor também os bloqueia. Resources planejados:

- `homepit://space`
- `homepit://finance/catalog`
- `homepit://projects/catalog`
- `homepit://docs/agent-guide`

Para exclusões, primeiro solicite uma prévia. A confirmação resultante é vinculada à conexão, ao recurso e à versão, vale cinco minutos e só pode ser usada uma vez.

## Bridge local stdio

Quando o cliente só aceita MCP por stdio, use o bridge local configurado por ambiente:

```powershell
$env:HOMEPIT_BASE_URL = "https://homepit.example"
$env:HOMEPIT_INTEGRATION_TOKEN = "SEU_TOKEN_DE_INTEGRACAO"
# iniciar o bridge conforme o pacote distribuído pela instância
```

O bridge não deve aceitar token como argumento de linha de comando nem imprimi-lo. Ele encaminha operações para a API externa e mantém as mesmas permissões da conexão manual.
