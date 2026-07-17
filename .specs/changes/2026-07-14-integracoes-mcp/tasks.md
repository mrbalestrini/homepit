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

## Validações executadas

- 2026-07-17: `dotnet test HomePit.sln` aprovou 142 testes, incluindo discovery OAuth com `registration_endpoint`, `none` em `token_endpoint_auth_methods_supported` e DCR público com callbacks loopback.
- 2026-07-17: `dotnet publish src/HomePit.Api/HomePit.Api.csproj -c Release -o .\\artifacts\\api-publish /p:UseAppHost=false` foi concluído com sucesso.
- 2026-07-17: `dotnet test tests/HomePit.IntegrationTests/HomePit.IntegrationTests.csproj --filter FullyQualifiedName~OAuthDiscoveryEndpointsTests` aprovou 6 testes: metadata do recurso, DCR com permissão de recurso, autorização canônica sem `invalid_target` e rejeição de recurso diferente.
- 2026-07-17: `dotnet test HomePit.sln` aprovou 146 testes (101 unitários e 45 de integração).
- 2026-07-17: `dotnet build src/HomePit.Api/HomePit.Api.csproj -c Release --no-restore` foi concluído com 0 avisos e 0 erros.
