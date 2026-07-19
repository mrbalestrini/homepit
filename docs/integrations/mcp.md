# MCP: agentes e automações

> Disponível quando `Integrations:Enabled=true` e `Mcp:Enabled=true`, com URLs públicas HTTPS configuradas.

## MCP remoto

O endpoint remoto é `/mcp`, com transporte Streamable HTTP stateless e OAuth 2.1. Ele não aceita chaves manuais `orgc_*`: essas chaves pertencem somente à API REST externa.

O cliente deve descobrir o recurso em `/.well-known/oauth-protected-resource/mcp`, registrar-se em `POST /connect/register` quando ainda não tiver um `client_id` e usar Authorization Code com PKCE S256. Envie o parâmetro `resource` com a URL canônica completa do MCP, por exemplo `https://api.exemplo.com/mcp`, na autorização e na troca do código.

Os scopes funcionam assim:

- `openid` é aceito para compatibilidade com clientes OAuth/OIDC e, quando houver ID token, ele contém apenas o identificadar estável `sub` da pessoa autenticada. Esse scope não concede acesso aos dados do Espaço nem habilita tools MCP.
- `organiza.read` é obrigatório para usar o MCP.
- `organiza.write` habilita escrita somente quando a conexão também foi aprovada como leitura e escrita.
- `offline_access` permite receber refresh token rotativo, limitado à validade da conexão.

Na tela do Organiza Club, a pessoa escolhe Espaço, somente leitura ou leitura/escrita e validade. O access token vale até 15 minutos; refresh tokens são rotativos e não sobrevivem à expiração ou revogação da conexão.

Os tools atuais seguem `finance_<verbo>_<recurso>` e `projects_<verbo>_<recurso>`. Conexões somente leitura não podem executar escrita; o servidor valida tanto o escopo OAuth quanto a conexão. Resources disponíveis:

- `organiza://space`
- `organiza://finance/catalog`
- `organiza://projects/catalog`
- `organiza://docs/agent-guide`

O bridge local stdio não faz parte desta versão.
