# Prompt 04 — perfil e publicação da documentação

Implemente a experiência de Conexão conforme a spec ativa. Use a skill `frontend` para a UI e `integration-docs` para os textos; não altere a semântica de segurança definida pelo backend.

Faça `/profile?tab=profile|connection` navegar entre abas reais. Em Conexão, permita criar uma conexão manual com nome, Casa, modo e validade; mostre o segredo apenas na confirmação, com aviso para copiá-lo. Liste chaves e concessões OAuth com prefixo/cliente, Casa, permissão, expiração, último uso, status e revogação. Nunca mostre hash ou segredo após a criação.

Publique na mesma aba os guias canônicos de `docs/integrations/` como Markdown seguro, com downloads do OpenAPI e `llms.txt`. Garanta responsividade, estados de carregamento/erro e testes de UI para revelação única/revogação. Não duplique contratos em strings de componente nem altere MCP/OAuth neste prompt.
