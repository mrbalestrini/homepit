# Decisoes

1. A pagina institucional usa o slug unico `home` e nao pertence a uma casa.
2. O CMS usa secoes fixas; beneficios e etapas sao as unicas listas repetiveis.
3. O conteudo entra no ar no mesmo salvamento, sem rascunho ou agendamento.
4. Antes do primeiro salvamento a API retorna conteudo padrao sem persistir dados.
5. Hero e destaque usam o object storage existente e endpoints publicos de leitura.
6. O SuperAdmin continua somente leitura nos modulos das casas; escrita e permitida
   exclusivamente no CMS institucional.
7. Textos sao simples e URLs externas aceitam somente HTTP ou HTTPS.
8. Concorrencia usa a regra de ultimo salvamento vence.
9. Como nao existe dominio comercial identificado no repositorio, o conteudo padrao usa
   `homepit.example.com` como placeholder e deve ser substituido pelo SuperAdmin no primeiro
   salvamento destinado a producao.
10. O compartilhamento social usa uma imagem SEO dedicada no slot publico `seo`, restrita a
    WEBP em `1200 x 630 px`, para nao acoplar o preview externo a imagem principal da landing.
