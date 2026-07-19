# Segurança, expiração e erros

## Operação da instância

Para ativar REST, configure `Integrations:Enabled=true` e forneça `Integrations:TokenPepper` com pelo menos 32 caracteres por secret manager ou variável de ambiente. O pepper não pode ser versionado. Para MCP, configure também `Mcp:Enabled=true`, `OAuth:Issuer`, `OAuth:WebConsentUrl` e duas chaves Base64 distintas de ao menos 32 bytes: `OAuth:SigningKey` e `OAuth:EncryptionKey`.

## Ciclo de vida da conexão

- A validade é obrigatória, sugere 90 dias e nunca pode ultrapassar 365 dias.
- Revogar uma conexão OAuth, desativar a conta ou perder o vínculo ativo com o Espaço bloqueia imediatamente o MCP e seus tokens de referência.
- O segredo é mostrado apenas uma vez. Guarde-o em um cofre e crie uma nova conexão em vez de tentar recuperar uma chave perdida.
- A auditoria guarda operação, superfície (REST/MCP), resultado, data e `traceId` por 90 dias; não guarda token, segredo ou payload financeiro.

## Proteções de operação

- O limite inicial é 60 operações por minuto por conexão. Ao receber `429`, respeite `Retry-After` antes de tentar novamente.
- Escritas devem usar uma `Idempotency-Key` estável por tentativa lógica, especialmente lançamentos e importações.
- Atualizações e exclusões REST devem enviar a `ETag` recebida em `If-Match`.
- Em MCP, exclusão só ocorre após prévia e confirmação vinculada, curta e de uso único.

## Erros

Erros seguem Problem Details e incluem `code`, `traceId` e `retryable`. Trate os códigos de acordo com a intenção: credencial inválida/expirada ou revogada exige nova autorização; somente leitura e autorização negada exigem ajuste de conexão/permissão; conflito de idempotência ou precondição exige reconciliar a versão; rate limit exige aguardar.

Ao pedir suporte, informe o `traceId`, horário e operação, nunca a chave de integração.
