# Decisions

## 2026-07-18 - identidade e nomenclatura

- O nome oficial e `Organiza Club`; namespaces e projetos usam `OrganizaClub.*`.
- A hierarquia oficial e `Espaco -> Nucleo -> Projeto`; no codigo, `Space -> Core -> Project`.
- `Casa` permanece permitida somente quando significa literalmente residencia ou imovel.
- Os oito SVGs recebidos sao mestres canonicos e nao devem ser redesenhados.
- DM Sans e a fonte de interface; o lettering exclusivo existe somente nos SVGs oficiais.

## 2026-07-18 - reset sem compatibilidade

- O modelo anterior nao tera migracao de dados, aliases, redirects ou DTOs de transicao.
- A nova baseline usa o schema `organiza_club` e historico EF dentro do mesmo schema.
- O reset operacional pode remover somente o schema `homepit`, o historico antigo
  comprovadamente pertencente ao produto e o bucket exato `homepit-assets`.
- O reset nunca remove o banco PostgreSQL inteiro nem schemas internos do Supabase.
- A execucao destrutiva exige confirmacao explicita, alvos exatos e ambiente validado.

## 2026-07-18 - temas e acessibilidade

- Os temas suportados sao `system`, `light` e `dark`; `system` e o padrao e acompanha
  `prefers-color-scheme` em tempo real.
- A paleta oficial usa creme `#F7F3E8`, marinho `#18223A`, azul `#2F63F5`, verde
  `#20B26B`, laranja `#FF8A34` e vermelho `#F04B4B`.
- O anfitriao aparece apenas em onboarding, ajuda, estados vazios e celebracoes.
- Contraste AA, foco visivel, teclado, reducao de movimento e larguras 320/768/1440 sao gates.

## 2026-07-18 - dominios e integracoes

- Os dominios oficiais sao `organiza.club` e `api.organiza.club`.
- A API externa preserva `GET /api/integrations/v1/space`, vinculada ao Space da conexao.
- Os scopes sao `organiza.read` e `organiza.write`; recursos usam `organiza://`; chaves
  manuais usam o prefixo `orgc_`.
- A API externa rejeita `X-Space-Id`: a conexao define o tenant e o header nao pode troca-lo.
- O bridge stdio permanece em etapa futura, conforme a decisao incorporada da mudanca de
  integracoes; esta entrega mantém o MCP remoto como transporte oficial.

## 2026-07-18 - versao e entrega

- A versao permanece `1.12.2`, pois a ultima versao publicada e desta mesma data.
- O trabalho ocorre na branch `main`, sem stage, commit, push ou renomeacao remota automatica.
