---
name: integration-docs
description: Criar, revisar e manter a documentação de consumo das integrações REST e MCP do HomePit. Use quando alterar o OpenAPI externo, tools MCP, OAuth, bridge stdio, exemplos de automação, receitas para IA ou a documentação exibida na aba Conexão.
---

# Integrações: documentação

## Procedimento

1. Leia `.specs/active-change.md`, `docs/integrations/README.md` e o OpenAPI externo quando existir.
2. Trate `contracts/openapi/homepit.integrations.v1.yaml` como a fonte de nomes, campos, operações e erros REST; trate a implementação MCP como fonte de disponibilidade de tools/resources.
3. Atualize juntos o guia afetado, `README.md`, receitas e `llms.txt` quando a mudança for visível a consumidores.
4. Escreva em português claro; use exemplos mínimos em curl, PowerShell, Python e TypeScript quando a operação for de uso recorrente.
5. Mostre variáveis como `HOMEPIT_INTEGRATION_TOKEN` ou `SEU_TOKEN`; nunca inclua tokens, hashes, pepper, dados financeiros reais, URL interna ou segredo nos exemplos.

## Regras de precisão

- Não documente endpoints, tools, campos, escopos, limites ou versões sem evidência no contrato ou código.
- Diferencie claramente recursos planejados dos disponíveis e não chame integração planejada de pronta.
- Preserve a fronteira: API externa não recebe `X-Household-Id`; a Casa é definida pela conexão.
- Documente permissão, expiração, revogação, idempotência, ETag e erros quando forem aplicáveis.
- Para MCP, mantenha o nome exato do tool, entrada estruturada, efeito de escrita e confirmação de exclusão.

## Validação antes de concluir

- Confirme que todo endpoint citado existe no OpenAPI e que todo tool citado existe no servidor MCP.
- Verifique que exemplos usam somente dados fictícios e segredos por variável de ambiente.
- Execute a validação estrutural disponível do OpenAPI e os exemplos seguros quando existirem; registre comandos não executados como pendentes.
