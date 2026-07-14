# Feature: data de conclusão e histórico recente de atividades

## Contexto

O quadro de projetos não registra quando uma atividade entra na coluna concluída e exibe indefinidamente todas as conclusões antigas.

## Objetivo

Registrar a data e hora em que a atividade foi concluída, limpar esse valor quando ela voltar para outra etapa e facilitar a busca de conclusões antigas sem poluir a visão padrão.

## Escopo

- Adicionar `CompletedAt` persistido na atividade e exposto na API/frontend.
- Preencher `CompletedAt` ao mudar o status para `Concluido`.
- Limpar `CompletedAt` ao mudar de `Concluido` para qualquer outro status.
- Ocultar por padrão atividades concluídas há mais de 30 dias no dashboard.
- Permitir alternar a exibição de todas as concluídas por um botão `Mostrar concluídas antigas`.
- Destacar o botão de forma piscante quando o texto buscado corresponder a uma atividade antiga oculta.

## Fora de escopo

- Alterar o prazo esperado (`DueDate`) ou a data de criação (`CreatedAt`).
- Apagar fisicamente atividades antigas.
- Criar uma busca adicional no backend.

## Arquivos ou áreas envolvidas

- Domínio/Application/Infrastructure de projetos e migration EF Core.
- Contrato OpenAPI de atividades.
- Hook, tipos, utilitários e workspace do dashboard de projetos.
- Testes de serviço/integração e Vitest do dashboard.

## Regras de negócio

- Uma atividade criada ou editada já como `Concluido` recebe `CompletedAt` quando não houver data.
- Uma atividade que sai de `Concluido` fica com `CompletedAt = null`.
- Uma atividade que permanece concluída conserva sua primeira data de conclusão.
- O filtro de 30 dias usa a data de conclusão, não a data de criação.
- A busca considera atividade, projeto, universo e descrição, como já ocorre no dashboard.

## Riscos

- Banco: migration aditiva nullable, sem alteração destrutiva.
- API/contrato: novos DTOs precisam continuar compatíveis com clientes que ignoram o campo.
- Autenticacao/autorizacao: nenhuma mudança.
- Frontend: o filtro local precisa continuar funcionando com todos os filtros e com a fila de relevância.
- Deploy/ambiente: a migration precisa ser descoberta automaticamente pelo EF Core.

## Plano

1. Adicionar o campo e a regra de transição no serviço de projetos.
2. Atualizar migration, snapshot, DTOs, OpenAPI e testes.
3. Adicionar o filtro de antigas, botão e destaque de busca no dashboard.
4. Executar testes Vitest/build e as validações backend disponíveis.

## Testes

- Status concluído preenche a data.
- Retorno a uma etapa aberta limpa a data.
- Alterações sem mudança de status preservam a data.
- Atividades antigas ficam ocultas até o botão ser acionado.
- Busca correspondente a item antigo destaca o botão.

## Criterios de aceite

- A atividade concluída mostra `Data concluída` no detalhe.
- Ao voltar de concluída, a data desaparece e pode ser registrada novamente numa futura conclusão.
- Concluídas com mais de 30 dias não aparecem na visão padrão.
- `Mostrar concluídas antigas` revela as atividades antigas.
- Uma busca que poderia encontrar uma antiga oculta faz o botão piscar e informa a ação disponível.

## Decisao final

A data é armazenada como `DateTimeOffset?` em UTC e o ocultamento é aplicado no controlador do dashboard, mantendo a API compatível e permitindo que a busca revele a possibilidade de resultados ocultos.
