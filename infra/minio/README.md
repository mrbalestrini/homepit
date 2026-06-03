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

Se voce ja subiu o container antigo com `quay.io/minio/aistor/minio`, recrie o servico com o mesmo project name usado pelo setup local:

```powershell
Set-Location infra/minio
docker compose -p homepit-minio -f docker-compose.yml down
docker compose -p homepit-minio -f docker-compose.yml up -d
```

Se o volume antigo tiver persistido configuracoes internas do AIStor, o MinIO community pode iniciar com erro semelhante a:

```text
Unable to initialize OpenID: found invalid keys (azure_use_group_id= azure_tenant_id= )
```

Nesse caso, para ambiente local, remova tambem o volume do MinIO e suba novamente:

```powershell
Set-Location infra/minio
docker compose -p homepit-minio -f docker-compose.yml down -v
docker compose -p homepit-minio -f docker-compose.yml up -d
```
