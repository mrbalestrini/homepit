# Decisoes

1. A feature tera uma unica imagem ativa por atividade.
2. O binario continua em object storage privado e o banco guarda apenas metadados.
3. O upload substitui a imagem anterior em vez de criar historico de anexos.
4. A imagem sera carregada com a casa ativa explicitada no request do frontend.
5. A exclusao de atividade, projeto ou universo deve remover os binarios vinculados para
   evitar arquivos orphanados.
6. Migrations criadas ou ajustadas manualmente devem manter os metadados de descoberta do
   EF Core.
