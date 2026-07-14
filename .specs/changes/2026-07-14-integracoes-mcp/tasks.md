# Tasks

- [x] Registrar o contrato OpenAPI externo inicial e a matriz de tools MCP inicial.
- [x] Implementar modelos, mappings, migration, autenticação de conexão, auditoria, idempotência e rate limiting.
- [ ] Completar REST externo de Financeiro e Projetos com paginação por cursor e ETags/`If-Match` reais.
- [ ] Implementar OAuth/OpenIddict, consentimento e bridge stdio; o MCP remoto manual já usa Streamable HTTP stateless.
- [x] Implementar a aba Conexão e publicar a documentação canônica e o início rápido no perfil.
- [ ] Validar OAuth, paridade integral OpenAPI/tools, MCP Inspector, Docker e smoke local com flags.

## Validações previstas

- Backend: testes unitários e de integração para credenciais, tenancy, permissão, idempotência, ETag, rate limit, OAuth e MCP.
- Frontend: testes da aba Conexão, revelação única e revogação; `npm test` e `npm run build`.
- Operação: `dotnet ef migrations list`, MCP Inspector nos transports remoto e stdio, OpenAPI parsing estrutural, Docker build e smoke com flags.
