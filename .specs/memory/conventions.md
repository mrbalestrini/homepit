# Convencoes

## FATO OBSERVADO

- `.editorconfig`: UTF-8, LF, newline final e 2 espacos; C# usa 4 espacos.
- C#: namespaces por pasta, PascalCase, classes frequentemente `sealed`, DTOs como `record`,
  metodos assincronos com sufixo `Async` e `CancellationToken`.
- Dependencias de servicos sao recebidas por construtores primarios e registradas por
  extensoes `AddHomePitApplication` e `AddHomePitInfrastructure`.
- Erros esperados usam subclasses de `AppException`; o middleware converte para Problem
  Details e codigos HTTP.
- Minimal APIs sao agrupadas em `/api/auth` e `/api`, sem controllers MVC observados.
- Entidades usam `Guid`, `AuditableEntity`, navegacoes EF e `IHouseholdScoped` quando aplicavel.
- Tabelas usam nomes `snake_case`; enums de status e papeis sao persistidos como texto.
- Migrations usam prefixo timestamp e ficam em `HomePit.Infrastructure/Migrations`.
- Frontend: arquivos em kebab-case, componentes em PascalCase, hooks `use*` e alias `@/*`.
- Paginas em `src/app` delegam para componentes em `src/features`.
- Componentes compartilhados ficam em `components/ui` e `features/workspace`.
- Estado e mutacoes das telas ficam em hooks como `useProjectDashboard` e `usePromptBank`.
- Testes frontend `*.test.ts(x)` ficam junto ao codigo; backend separa unidade e integracao.
- Commits seguem Conventional Commits em portugues, minusculas e sem acentuacao.

## INFERÊNCIA

- O padrao preferido para novas regras e adiciona-las aos servicos de Application, mantendo
  endpoints e paginas finos, porque esse e o desenho predominante.
- Novos componentes visuais devem reutilizar `components/ui` antes de introduzir outro
  conjunto de primitivas.

## NÃO IDENTIFICADO

- Formatter oficial para C#, TypeScript ou Markdown.
- Convencao formal de branches e pull requests no repositorio.
- Limite formal de tamanho para arquivos, componentes ou servicos.
