# Feature: esforço semanal e fila relevante

## Objetivo

Permitir que cada membro configure pontos recorrentes por dia para a Casa, seus Universos e Projetos, e sugerir uma fila diária de atividades relevantes.

## Decisão final

- A capacidade pertence ao membro ativo da Casa e se repete de segunda a domingo.
- Pontos explícitos reservam capacidade; campo vazio herda ou deriva a capacidade do nível pai; zero bloqueia o escopo no dia.
- A fila pessoal inclui atividades abertas atribuídas ao membro atual e atividades sem responsável. Atividades atribuídas a outra pessoa não entram.
- O score soma prioridade, urgência de prazo, antiguidade e bônus de atribuição. O tamanho da atividade é o custo usado para encaixe nas reservas.
- Reservas não utilizadas não são emprestadas a outros ramos.

## Fora de escopo

- Histórico, vigência, rollover ou apontamento de esforço realizado.
- Atribuição automática de atividades sem responsável.
- Alterar os campos existentes `CreatedAt` e `DueDate` da atividade.
