# MinIO local

Este recurso sobe o armazenamento de objetos privado do HomePit.
O compose usa por padrão a imagem community `minio/minio` para evitar a edicao enterprise `aistor`, que bloqueia operacoes S3 sem licenca.

## Enderecos locais

- API S3 compatível: `http://localhost:9000`
- Console administrativo: `http://localhost:9001`

## Credenciais

Use as variáveis de `infra/minio/.env`.

O backend consome o mesmo `MINIO_ROOT_USER` e `MINIO_ROOT_PASSWORD` via `apps/api/.env`.

## Migracao de ambiente

Se voce ja subiu o container antigo com `quay.io/minio/aistor/minio`, recrie o servico para trocar a imagem:

```powershell
Set-Location infra/minio
docker compose down
docker compose up -d
```
