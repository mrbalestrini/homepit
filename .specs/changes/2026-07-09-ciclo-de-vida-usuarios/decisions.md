# Decisoes

1. Espaço "criada pelo usuario" sera inferida pelas memberships ativas em que ele possui `Role = Owner`.
2. Cancelamento proprio com espaço propria nao remove dados imediatamente; a conta entra em
   `PendingSelfDeletion` por 30 dias.
3. Exclusao definitiva de um usuario remove tambem comentarios autorados por suas memberships
   antes de apagar os vinculos, para respeitar o `DeleteBehavior.Restrict` atual.
4. O bloqueio real de conta desativada ocorrera no backend por middleware/guard dedicada; o
   frontend apenas reflete o estado retornado pela sessao.
5. O painel global do superadmin ficara em `/admin/users`, separado do CMS institucional.
