# Glossario

## FATO OBSERVADO

- `AppUser`: identidade de login, perfil e contato opcional.
- `SystemRole`: perfil global `User`, `Admin` ou `SuperAdmin`.
- `Space`: espaço e limite principal de tenancy.
- `SpaceMember`: vinculo ativo/inativo entre usuario e espaço.
- `SpaceRole`: papel `Owner`, `Admin` ou `Member` dentro do espaço.
- `Core`: agrupamento superior de projetos e classificacao opcional de prompts.
- `Project`: projeto pertencente a um núcleo.
- `Activity`: trabalho pertencente a um projeto, com `CreatedAt` auditavel, prazo esperado opcional e responsavel.
- `PendingItem`: pendencia/subtarefa de uma atividade.
- `ActivityComment`: comentario de atividade com autoria preservada.
- `Prompt`: entrada compartilhada do banco de prompts do espaço.
- `PromptCategory`: categoria reutilizavel e unica por nome dentro do espaço.
- `PromptCategoryAssignment`: associacao muitos-para-muitos entre prompt e categoria.
- `NotificationPreference`: configuracao de resumo diario por membro.
- `NotificationRun`: registro de envio usado para idempotencia.
- `InstitutionalPage`: configuracao global da landing publica, sem vinculo com espaço.
- `SuperAdmin`: usuario global configurado por ambiente, com leitura entre espaços e escrita
  restrita ao CMS institucional.
- `Daily digest`: resumo de atividades abertas atribuidas ao membro, enviado por WhatsApp.

## INFERÊNCIA

- `Admin` global e `Admin` do espaço representam conceitos distintos; tarefas devem sempre
  indicar se tratam de `SystemRole` ou `SpaceRole`.

## NÃO IDENTIFICADO

- Traducoes oficiais para todos os termos exibidos ao usuario.
- Termo oficial para o conjunto completo de modulos do Organiza Club.
