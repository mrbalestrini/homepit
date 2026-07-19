# Database Change - Organiza Club baseline

## Estrategia

- Substituir o historico de migrations por uma unica baseline do modelo completo.
- Usar schema `organiza_club` e tabela `__EFMigrationsHistory` nesse schema.
- Renomear tabelas, colunas, indices, constraints e FKs para `Space` e `Core`.
- Preservar os atributos `[DbContext]` e `[Migration]` para descoberta automatica.

## Reset protegido

O script deve validar provedor, host, banco, schemas e buckets antes de oferecer a operacao.
A confirmacao deve citar os alvos exatos. Sao permitidos somente:

- `DROP SCHEMA homepit CASCADE` no banco explicitamente validado;
- remocao de historico antigo somente quando ele pertencer comprovadamente ao produto;
- limpeza e remocao do bucket exato `homepit-assets`;
- orientacao para limpar chaves locais e sessoes antigas no navegador.

E proibido remover o banco inteiro ou schemas de sistema/Supabase.

## Verificacoes

- `dotnet ef migrations list` encontra a baseline.
- PostgreSQL vazio recebe schema e tabelas completos.
- Startup com banco e storage vazios aplica a baseline.
- O teste de descobribilidade de migrations permanece verde.
- Cascatas de Space/Core e desvinculo de prompt ao excluir Core sao preservados.
