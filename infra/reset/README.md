# Reset HomePit -> Organiza Club

Este procedimento implementa o reset sem apagar o banco PostgreSQL nem tocar nos schemas
internos do Supabase. Ele aceita somente os alvos legados fixos `homepit` e
`homepit-assets`, valida marcadores do produto e interrompe se o novo schema ou bucket ja
existirem.

## Preflight

Defina as credenciais apenas no processo atual; o script nao as imprime nem as grava no
repositorio:

```powershell
$env:PGPASSWORD = "senha-postgres"
$env:ORGANIZA_RESET_STORAGE_ACCESS_KEY = "access-key"
$env:ORGANIZA_RESET_STORAGE_SECRET_KEY = "secret-key"
.\infra\reset\organiza-club-reset.ps1 `
  -DatabaseHost "servidor-postgres" `
  -DatabaseName "postgres" `
  -DatabaseUser "supabase_admin" `
  -StorageEndpoint "https://storage.exemplo"
```

O preflight e obrigatorio e nao altera dados. Depois de conferir o alvo exibido, execute
novamente com:

```powershell
-Execute -Confirmation "RESET-HOMEPIT-PARA-ORGANIZA-CLUB"
```

## Protecoes

- nunca executa `DROP DATABASE`;
- nunca remove `public`, `auth`, `storage`, `realtime`, `extensions` ou outros schemas;
- remove somente o schema exato `homepit`, depois de validar tabelas marcadoras;
- remove um historico EF em `public` somente se todas as migrations estiverem na lista
  conhecida do HomePit;
- interrompe se `organiza_club` ou `organiza-club-assets` ja existirem;
- usa uma configuracao temporaria do cliente MinIO e a apaga ao terminar;
- aplica a baseline do EF que recria `organiza_club` e seu historico;
- nao recria clientes OAuth antigos: o novo ambiente inicia sem clientes e sem sessoes.

As chaves JWT, o pepper de integracao e as chaves OAuth devem ser gerados novamente no
gerenciador de segredos do ambiente. A aplicacao web remove automaticamente apenas chaves de
`localStorage`, `sessionStorage` e Cache Storage com prefixo legado `homepit`.
