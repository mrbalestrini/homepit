# Feature: ciclo de vida de usuarios e gestao global pelo superadmin

## Contexto

O fluxo atual permite cadastro com casa inicial opcional, edicao de perfil somente em modal e
nao possui estados formais para contas desativadas nem um painel global de usuarios para o
superadmin.

## Objetivo

Introduzir um ciclo de vida explicito de conta, separar a gestao de perfil em pagina dedicada e
permitir que o superadmin gerencie usuarios comuns globalmente, inclusive com desativacao,
reativacao e exclusao definitiva.

## Escopo

- Remover a criacao de casa inicial do cadastro.
- Adicionar estados `Active`, `PendingSelfDeletion` e `DisabledBySuperAdmin` para usuarios.
- Permitir cancelamento proprio da conta com exclusao imediata ou agendada em 30 dias.
- Criar purge automatico para contas com exclusao agendada vencida.
- Expor endpoints para gestao propria e gestao global pelo superadmin.
- Substituir a modal de perfil por uma pagina `/profile`.
- Criar painel `/admin/users` para o superadmin.

## Fora de escopo

- Transferencia explicita de propriedade de casa.
- Recuperacao de senha, MFA ou novos fluxos de credenciais.
- Ajustes na mudanca ativa de imagens WEBP alem do necessario para conviver com este trabalho.

## Arquivos ou areas envolvidas

- `apps/api/src/HomePit.Domain/Households/*`
- `apps/api/src/HomePit.Application/Auth/*`
- `apps/api/src/HomePit.Application/Households/*`
- `apps/api/src/HomePit.Infrastructure/*`
- `apps/api/src/HomePit.Api/*`
- `apps/api/tests/*`
- `apps/web/src/app/*`
- `apps/web/src/features/workspace/*`
- `apps/web/src/features/profile/*`
- `apps/web/src/features/admin-users/*`
- `apps/web/src/lib/api.ts`
- `contracts/openapi/homepit.v1.yaml`
- `CHANGELOG.md`

## Regras de negocio

- Cadastro sempre cria conta sem casa inicial.
- Conta sem casas proprias pode ser excluida imediatamente.
- Conta com ao menos uma casa em que o usuario e `Owner` entra em `PendingSelfDeletion` por 30 dias.
- Ao vencer o prazo, a conta, suas casas proprias e todos os vinculos dessas casas sao apagados.
- Conta desativada pelo superadmin entra em `DisabledBySuperAdmin` sem exclusao automatica.
- O superadmin pode reativar ou excluir definitivamente usuarios comuns.
- O superadmin permanece protegido e somente leitura no painel global.

## Riscos

- Banco:
  introducao de novos campos e migration manual exige metadados de descoberta do EF.
- API/contrato:
  login, refresh, guard de conta e novos endpoints alteram o shape da sessao.
- Autenticacao/autorizacao:
  contas desativadas precisam autenticar para tela intermediaria, mas nao podem usar rotas comuns.
- Frontend:
  a remocao da modal de perfil afeta toda a shell compartilhada.
- Deploy/ambiente:
  o purge automatico depende de worker hospedado e configuracao default segura.

## Plano

1. Registrar novos estados e datas de ciclo de vida da conta no dominio e no banco.
2. Extrair purge compartilhado de casas e usuarios com limpeza de object storage.
3. Adicionar endpoints e guard para contas desativadas.
4. Migrar a experiencia de perfil para `/profile` e criar `/admin/users`.
5. Atualizar contrato, changelog e cobertura automatizada.

## Testes

- Unitarios de `AuthService`, `HouseholdService` e purge worker.
- Integracao de login, refresh, bloqueio de conta e painel `/api/admin/users`.
- Vitest para cadastro, shell, pagina de perfil, tela de conta desativada e painel global.

## Criterios de aceite

- Cadastro deixa de pedir casa inicial.
- Usuario pode cancelar a propria conta conforme o cenario com ou sem casas proprias.
- Superadmin gerencia usuarios em painel separado.
- Conta desativada mostra tela intermediaria apropriada no login.
- Perfil passa a ser gerenciado em pagina dedicada sem overflow de campos.

## Decisao final

Implementar ciclo de vida de conta com estados persistidos, purge automatico para cancelamento
proprio com casas, pagina dedicada de perfil e painel global de usuarios do superadmin.
