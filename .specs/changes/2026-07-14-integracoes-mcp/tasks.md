# Tasks

- [x] Registrar o contrato OpenAPI externo inicial e a matriz de tools MCP inicial.
- [x] Implementar modelos, mappings, migration, autenticação de conexão, auditoria, idempotência e rate limiting.
- [x] Completar REST externo de Financeiro e Projetos com paginação por cursor e ETags/`If-Match` reais.
- [x] Implementar OAuth/OpenIddict e consentimento para MCP remoto; o bridge stdio permanece em etapa futura.
- [x] Implementar a aba Conexão e publicar a documentação canônica e o início rápido no perfil.
- [ ] Validar OAuth, paridade integral OpenAPI/tools, MCP Inspector, Docker e smoke local com flags.

## Validações previstas

- Backend: testes unitários para cursor e ETag; testes de integração existentes para os módulos e credenciais. OAuth/MCP inclui discovery, DCR, PKCE, consentimento, revogação e autorização por conexão.
- Frontend: testes da aba Conexão, revelação única e revogação; `npm test` e `npm run build`.
- Operação: `dotnet ef migrations list`, MCP Inspector nos transports remoto e stdio, OpenAPI parsing estrutural, Docker build e smoke com flags.
