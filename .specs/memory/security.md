# Seguranca

## FATO OBSERVADO

- Existem `.env.example` para API, web, Supabase, Evolution e MinIO.
- `.env` e `.env.*` sao ignorados, com excecao de `.env.example`; nenhum `.env` local foi
  encontrado durante esta leitura.
- Campos sensiveis incluem conexao do banco, chave JWT, credenciais do SuperAdmin, Evolution
  API e MinIO. Nao registrar seus valores nesta memoria.
- `appsettings*.json` versionados possuem valores nao vazios em alguns campos sensiveis.
- Senhas de usuarios usam PBKDF2-SHA256 com salt; refresh tokens sao armazenados como hash.
- O refresh token e revogado e substituido ao renovar a sessao.
- JWT inclui identidade, perfil do sistema, casas e papeis por casa.
- O frontend guarda access token e refresh token em `localStorage`.
- SuperAdmin e habilitado por configuracao e bloqueado para escrita nos servicos observados.
- A unica excecao de escrita do SuperAdmin e o CMS institucional global; a verificacao
  ocorre no servico de Application.
- `Owner`, `Admin` e `Member` possuem autorizacao adicional aplicada nos servicos.
- CORS aceita qualquer origem quando a lista configurada esta vazia.
- Uploads de perfil, universo, prompt e atividade aceitam JPG, PNG ou WEBP e limitam 5 MB.
- Imagens institucionais aceitam os mesmos tipos e limite, mas possuem leitura publica
  intencional e cache por URL versionada.
- Dados com indicio de sensibilidade: e-mail, hash de senha, telefone/WhatsApp, tokens,
  objetos privados e identificadores de mensagens.

## INFERÊNCIA

- Tokens em `localStorage` ampliam o impacto de uma vulnerabilidade XSS.
- Valores sensiveis nao vazios em configuracao versionada podem ser usados acidentalmente
  fora do ambiente pretendido.
- CORS aberto por ausencia de configuracao pode expor a API a origens nao planejadas.

## NÃO IDENTIFICADO

- Modelo formal de ameacas, auditoria de seguranca ou teste de penetracao.
- Rate limiting, bloqueio por tentativas, MFA ou recuperacao de senha.
- Politica de rotacao de chaves, expiracao de sessoes globais e revogacao por usuario.
- Garantia de TLS, criptografia em repouso, backup e retencao de dados.
- Politica de privacidade para telefone, mensagens e imagens.
