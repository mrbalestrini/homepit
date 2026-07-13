# Decisões

- A ação `Atribuir-me` reutiliza `PUT /api/activities/{id}` para evitar novo
  endpoint e manter o fluxo alinhado ao editor de atividades.
- A visibilidade do item depende de haver um membro atual reconhecido na casa e
  de a atividade continuar editável para a pessoa logada.
