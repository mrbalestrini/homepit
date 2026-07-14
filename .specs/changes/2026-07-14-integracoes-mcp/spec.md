# Feature: integrações e MCP

## Contexto

O HomePit não possui uma superfície externa com credenciais próprias. A API interna usa JWT e seleciona a Casa por `X-Household-Id`, modelo inadequado para automações e agentes de IA.

## Objetivo

Permitir que um membro ativo crie conexões por Casa, com acesso de leitura ou leitura/escrita, validade de até um ano e revogação imediata. Oferecer API externa versionada, documentação para agentes e um servidor MCP remoto, expondo primeiro Financeiro e Projetos.

## Escopo

- Aba `Conexão` no perfil para chaves manuais e concessões OAuth.
- Credenciais de integração, auditoria, idempotência, limite de requisições e autorização derivada do criador.
- Contrato OpenAPI externo em `/api/integrations/v1` e MCP remoto em `/mcp`.
- OAuth 2.1 para clientes MCP, com Authorization Code + PKCE, discovery e registro dinâmico.
- Documentação canônica em `docs/integrations/`, recipes para automações e skill `integration-docs`.
- Recursos iniciais: Financeiro e Projetos; imagens apenas como metadados de leitura e pendências apenas para listar/criar.

## Fora de escopo

- Compatibilidade com chaves internas, SuperAdmin, administração de usuários, billing ou rotas de sessão.
- Escrita de mídia, upload/download de binários e novas regras de edição/conclusão/exclusão de pendências.
- Backfill de dados ou alteração retroativa de autoria e cotas.

## Regras de negócio

- Cada conexão pertence a um usuário e uma Casa; membro ativo pode criá-la, mas sua permissão nunca excede o papel, autoria, cotas ou acesso atual do criador.
- A validade é obrigatória, padrão de 90 dias e máximo de 365 dias. Conta desativada, vínculo inativo, expiração ou revogação bloqueiam o acesso imediatamente.
- Chave manual é exibida somente na criação, é protegida por HMAC-SHA256 com pepper de ambiente e nunca aparece em log, auditoria ou resposta posterior.
- Escritas exigem conexão `ReadWrite`; exclusão no MCP exige prévia e confirmação única com validade de cinco minutos.
- Requisições são limitadas a 60 por minuto por conexão; mutações suportadas exigem idempotência de 90 dias e updates/deletes REST exigem `If-Match`.

## Riscos

- Banco: credenciais, auditoria, idempotência e tabelas OpenIddict requerem migration descobrível pelo EF.
- Segurança: OAuth, segredo, audience e revogação não podem contornar tenancy, autoria ou cotas.
- Contrato: OpenAPI externo, tools MCP e documentação precisam evoluir juntos.
- Deploy: produção exige HTTPS, pepper, certificados OpenIddict, URL pública e proxies conhecidos configurados antes de habilitar as flags.

## Plano

1. Implementar fundação de conexões, modelos persistidos e feature flags.
2. Expor a API externa com autenticação de conexão, Problem Details, idempotência e ETags.
3. Adicionar OAuth/OpenIddict e servidor MCP com tools derivados do contrato.
4. Criar a aba de perfil e publicar os guias canônicos.
5. Validar segurança, paridade de contrato, REST, MCP, bridge local, migrations e builds.

## Critérios de aceite

- Uma chave criada é revelada uma vez, funciona somente até expiração/revogação e fica limitada à Casa e às permissões efetivas do criador.
- Financeiro e Projetos são operáveis por REST e MCP sem requerer `X-Household-Id`.
- Um cliente MCP obtém autorização OAuth com PKCE e pode revogá-la pela aba Conexão.
- A documentação permite a um agente configurar uma conexão e criar um lançamento financeiro sem incluir segredos.

## Decisão final

Construir a integração como borda externa separada da API web, desabilitada por padrão com `Integrations:Enabled` e `Mcp:Enabled`, preservando os contratos internos existentes.
