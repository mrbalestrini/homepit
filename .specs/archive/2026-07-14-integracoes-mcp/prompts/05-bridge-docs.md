# Prompt 05 — bridge stdio e documentação de consumidores

Implemente o bridge local MCP e complete a documentação canônica da integração. Leia `.agents/skills/integration-docs/SKILL.md`, `docs/integrations/` e o OpenAPI externo; trate esses artefatos como fonte de verdade para consumidores.

Crie um executável/host local stdio que leia exclusivamente `HOMEPIT_BASE_URL` e `HOMEPIT_INTEGRATION_TOKEN`, encaminhe a API externa e nunca registre token ou aceite segredo em argumento de linha de comando. Valide com MCP Inspector. Atualize guias de REST, MCP remoto, bridge, segurança, erros e recipes em curl, PowerShell, Python e TypeScript, com exemplos de lançamento financeiro e criação de projeto/atividade. Gere `llms.txt` conciso.

Não invente endpoint, campo ou comportamento fora do OpenAPI implementado. Adicione checagens estruturais de OpenAPI e paridade `operationId`/tools, incluindo execução controlada dos exemplos sem segredos reais.
