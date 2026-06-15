# Padroes tecnicos

## FATO OBSERVADO

- Minimal APIs ficam em `Program.cs`; grupos autenticados usam `RequireAuthorization`.
- `IUserContext` extrai usuario, perfil global e `X-Household-Id` do request.
- Servicos de Application aplicam validacao, tenancy e autorizacao antes de persistir.
- Infrastructure implementa EF Core, JWT, PBKDF2, MinIO, Evolution e worker hospedado.
- `IHomePitDbContext`, `IPasswordHasher`, `ITokenService`, `IObjectStorage` e
  `IWhatsAppClient` isolam dependencias.
- EF Core usa schema `homepit`, configuracao Fluent API, indices, constraints e migrations.
- Entidades auditaveis recebem `CreatedAt` e `UpdatedAt` no `SaveChangesAsync`.
- Exclusoes usam combinacao de cascata, `SetNull`, `Restrict` e inativacao conforme o vinculo.
- Erros de aplicacao viram Problem Details; erros inesperados nao retornam detalhe interno.
- Arquivos privados sao lidos por endpoints autenticados com `Cache-Control: no-store`.
- Imagens institucionais sao a excecao publica do object storage e usam
  `Cache-Control: public, max-age=31536000, immutable` com timestamp na URL.
- O CMS institucional e global, nao usa `X-Household-Id` e valida `SystemRole.SuperAdmin`
  no servico de Application.
- Frontend centraliza tipos, fetch, refresh e eventos de sessao em `src/lib/api.ts`.
- A selecao da casa ativa usa helper compartilhado em `src/lib/household-selection.ts`
  para persistencia segura, validacao contra a sessao e limpeza de valores obsoletos.
- Casas na sessao agora incluem `CreatedAt`, permitindo fallback por recencia quando a
  selecao salva nao existe mais.
- Hooks de feature funcionam como controladores de estado e mutacao.
- Telas reutilizam `HomePitWorkspaceShell` e componentes em `components/ui`.
- Next.js gera output `standalone`; Docker usa build multi-stage e usuario nao root na web.
- Testes backend usam xUnit, EF InMemory e WebApplicationFactory com fakes de storage.
- Testes frontend usam Vitest, jsdom e Testing Library.

## INFERÊNCIA

- Para uma mudanca local, seguir os servicos e hooks existentes e mais consistente do que
  introduzir uma nova camada.
- Alteracoes de contrato devem atualizar implementacao, tipos frontend, OpenAPI e testes
  relacionados, pois hoje esses artefatos sao mantidos separadamente.

## NÃO IDENTIFICADO

- Politica de transacoes para operacoes com banco e object storage.
- Padrao de testes end-to-end em navegador.
- Geracao de cliente a partir do OpenAPI.
- Ferramenta de cobertura ou limite minimo exigido.
