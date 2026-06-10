# AGENTS.md

Este repositório usa uma política simples e consistente de versionamento e changelog.

## Regras de versão

- A versão oficial do projeto deve ser a mesma em todos os arquivos `package.json` do repositório.
- A versão atual começa em `1.0.0`.
- Quando eu disser `Suba a versão`, isso significa aplicar a próxima versão seguindo Semantic Versioning:
  - `patch` para correções e ajustes internos compatíveis.
  - `minor` para novas funcionalidades compatíveis.
  - `major` para mudanças incompatíveis ou de quebra.
- Não altere a versão sem essa instrução, a menos que seja necessário para iniciar o projeto ou corrigir inconsistência entre manifests.

## Regras de changelog

- Mantenha um `CHANGELOG.md` na raiz do repositório.
- Registre no changelog todas as alterações relevantes que afetem comportamento, contrato, integração, operação ou entrega.
- Sempre que houver mudanças e a versão não for explicitamente elevada, atualize o changelog mantendo a versão corrente.
- Quando a versão subir, crie a nova seção correspondente no topo do changelog e mantenha as versões anteriores preservadas.
- Use um formato claro, preferencialmente compatível com [Keep a Changelog](https://keepachangelog.com/pt-BR/1.1.0/).

## Fluxo recomendado

- Antes de publicar uma mudança, confira se os `package.json` continuam consistentes com o `CHANGELOG.md`.
- Se houver arquivos gerados como `package-lock.json`, mantenha-os alinhados com a versão vigente quando isso fizer sentido para o repositório.
- Evite misturar mudanças de versão com refatorações grandes sem registrar o motivo no changelog.

