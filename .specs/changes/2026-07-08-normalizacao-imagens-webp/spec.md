# Refactor: normalizacao compartilhada de imagens webp

## Contexto

Uploads de imagem comuns do HomePit ainda gravam o binario original em object storage,
com validacoes repetidas por servico e sem padronizacao de formato ou dimensao.

## Objetivo

Centralizar a preparacao de uploads comuns de imagem no backend para converter novos arquivos
em `WEBP`, limitar a imagem final a no maximo `2000px` de largura ou altura e manter o
fluxo especial de SEO inalterado.

## Escopo

- Criar uma abstracao compartilhada de processamento de imagem.
- Aplicar a regra em foto de perfil, universo, atividade, prompt e imagens institucionais
  `hero` e `highlight`.
- Atualizar frontend, contrato OpenAPI, changelog e testes relacionados.

## Fora de escopo

- Migracao retroativa do acervo ja salvo.
- Alteracao do fluxo especial de SEO alem de reaproveitamento interno sem mudar seu
  comportamento.
- Suporte a `HEIC/HEIF`.

## Arquivos ou areas envolvidas

- `apps/api/src/HomePit.Application/*`
- `apps/api/src/HomePit.Infrastructure/*`
- `apps/api/tests/*`
- `apps/web/src/features/*`
- `contracts/openapi/homepit.v1.yaml`
- `CHANGELOG.md`

## Comportamento que nao deve mudar

- Tenancy, autorizacao e leitura protegida/publica das imagens.
- Chaves de object storage e schema de banco.
- Regras de SEO: `WEBP`, `1200x630`, limite proprio de tamanho e crop no frontend.

## Riscos

- Regressao em uploads existentes por depender de decodificacao real da imagem.
- Divergencia entre arquivos legados e novos uploads se o contrato/documentacao nao deixarem
  claro o comportamento.
- Quebra em testes atuais que assumem bytes arbitrarios ou `Content-Type` original.

## Plano

1. Adicionar policy e processador compartilhados para imagens.
2. Refatorar servicos de upload para usar a nova abstracao.
3. Atualizar frontend e OpenAPI para a nova regra comum.
4. Cobrir conversao, resize, transparencia, animacao e SEO com testes.
5. Registrar a mudanca em changelog e memoria factual relevante.

## Testes e validacao

- xUnit para o processador e servicos impactados.
- Testes de integracao dos endpoints de upload/leitura.
- `npm test` e `npm run build` no frontend.

## Criterios de aceite

- Novos uploads comuns passam a ser armazenados e servidos como `image/webp`.
- A imagem final respeita no maximo `2000x2000` sem upscale.
- GIF/WebP animado e formatos nao suportados sao rejeitados.
- SEO continua com o mesmo comportamento observado hoje.

## Decisao final

Implementar a normalizacao compartilhada de imagens comuns no backend, com conversao para
WEBP e limite maximo de 2000 px, preservando o fluxo especial de SEO como excecao explicita.
