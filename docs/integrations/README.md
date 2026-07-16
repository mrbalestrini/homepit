# Integrações HomePit

> Status: especificação da integração v1. A disponibilidade depende da liberação de `Integrations:Enabled`; não use estes guias como evidência de que uma instância já está habilitada.

Integrações permitem que automações e agentes de IA operem os dados da sua Casa por uma conexão com validade e permissão próprias. Uma conexão pode ser somente leitura ou permitir leitura e escrita, sempre dentro das permissões atuais de quem a criou.

## Antes de começar

1. Para REST, abra **Perfil > Conexão** e crie uma chave para a Casa desejada.
2. Escolha a permissão e a data de expiração (máximo de um ano).
3. Copie a chave no momento da criação. Ela não poderá ser exibida novamente.
4. Para MCP remoto, informe a URL `/mcp` ao cliente e conclua o consentimento OAuth no HomePit.

A conexão não aceita `X-Household-Id`: a Casa já está fixada na credencial. Revogue a conexão imediatamente se o segredo for exposto.

## Guias

- [API REST](rest-api.md): autenticação, paginação, concorrência e idempotência.
- [MCP](mcp.md): OAuth, tools e resources remotos.
- [Segurança e erros](security.md): validade, revogação, limites e respostas de falha.
- [Receitas](recipes.md): lançamentos financeiros e fluxos de projetos.
- [Resumo para agentes](llms.txt): índice compacto e regras de segurança.

O contrato externo canônico é `contracts/openapi/homepit.integrations.v1.yaml`. A interface da aplicação e os exemplos devem acompanhar esse arquivo, sem duplicar ou inventar campos.
