# Decisions

## 2026-07-08 - pipeline comum no backend

- O processamento de uploads comuns passa a ocorrer no backend com uma abstracao unica,
  evitando duplicacao de validacao e conversao entre servicos.
- A policy comum aceita `jpeg`, `png`, `webp`, `gif` e `bmp`, mas rejeita imagens animadas.
- O arquivo final comum sempre e salvo como `image/webp`, com redimensionamento para caber
  em `2000x2000` sem ampliar imagens menores.
- O fluxo de SEO permanece com policy separada e comportamento funcional inalterado.
