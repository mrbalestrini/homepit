# MinIO local

Este recurso sobe o armazenamento de objetos privado do HomePit.

## Enderecos locais

- API S3 compatível: `http://localhost:9000`
- Console administrativo: `http://localhost:9001`

## Credenciais

Use as variáveis de `infra/minio/.env`.

O backend consome o mesmo `MINIO_ROOT_USER` e `MINIO_ROOT_PASSWORD` via `apps/api/.env`.
