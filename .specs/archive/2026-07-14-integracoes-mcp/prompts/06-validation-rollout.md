# Prompt 06 — validação e liberação segura

Valide a implementação completa da mudança `2026-07-14-integracoes-mcp`. Use a skill `qa`, leia as decisões e não altere comportamento funcional salvo para corrigir falhas comprovadas.

Execute e registre testes de chave manual, expiração, revogação, conta/membro inativo, read-only, autoria, cotas, isolamento de Casa, auditoria, idempotência concorrente, ETags e rate limit. Cubra OAuth discovery/DCR/PKCE/audience/consentimento/refresh/revogação e MCP remoto/stdio, inclusive preview de exclusão e bloqueio de mídia. Valide migrations e descoberta EF, parser OpenAPI, paridade tools, testes/build da web e builds Docker.

Produza evidência objetiva em `tasks.md`, sem declarar sucesso para comandos não executados. Confirme que produção só habilita `Integrations:Enabled` e `Mcp:Enabled` após HTTPS, pepper, certificados, URL pública e proxies conhecidos estarem configurados. Não faça commit, push ou deploy.
