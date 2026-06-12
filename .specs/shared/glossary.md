# Glossario

## FATO OBSERVADO

- `AppUser`: identidade de login, perfil e contato opcional.
- `SystemRole`: perfil global `User`, `Admin` ou `SuperAdmin`.
- `Household`: casa e limite principal de tenancy.
- `HouseholdMember`: vinculo ativo/inativo entre usuario e casa.
- `HouseholdRole`: papel `Owner`, `Admin` ou `Member` dentro da casa.
- `Universe`: agrupamento superior de projetos e classificacao opcional de prompts.
- `Project`: projeto pertencente a um universo.
- `Activity`: trabalho pertencente a um projeto, com status, prioridade e responsavel.
- `PendingItem`: pendencia/subtarefa de uma atividade.
- `ActivityComment`: comentario de atividade com autoria preservada.
- `Prompt`: entrada compartilhada do banco de prompts da casa.
- `PromptCategory`: categoria reutilizavel e unica por nome dentro da casa.
- `PromptCategoryAssignment`: associacao muitos-para-muitos entre prompt e categoria.
- `NotificationPreference`: configuracao de resumo diario por membro.
- `NotificationRun`: registro de envio usado para idempotencia.
- `SuperAdmin`: usuario global configurado por ambiente, com leitura entre casas e sem escrita.
- `Daily digest`: resumo de atividades abertas atribuidas ao membro, enviado por WhatsApp.

## INFERÊNCIA

- `Admin` global e `Admin` da casa representam conceitos distintos; tarefas devem sempre
  indicar se tratam de `SystemRole` ou `HouseholdRole`.

## NÃO IDENTIFICADO

- Traducoes oficiais para todos os termos exibidos ao usuario.
- Termo oficial para o conjunto completo de modulos do HomePit.
