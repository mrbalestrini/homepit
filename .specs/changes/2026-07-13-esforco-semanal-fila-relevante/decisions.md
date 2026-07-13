# Decisions

## 2026-07-13 - capacidade pessoal e hierárquica

- A configuração é vinculada a `HouseholdMember`, não à Casa como um todo.
- Um filho configurado reserva pontos do pai. Valores ausentes derivam o mínimo necessário dos filhos e valores explícitos maiores deixam saldo compartilhado.
- Zero é uma reserva válida que bloqueia o escopo; ausência da linha significa que não há reserva explícita.

## 2026-07-13 - score de relevância

- Prioridade vale 100, 200, 300 e 400 para Baixa, Média, Alta e Urgente.
- Prazo vencido soma 200; hoje 180; em 1–3 dias 120; em 4–7 dias 60.
- Antiguidade soma um ponto por dia até 120 e atribuição ao membro atual soma 25.
- A capacidade é planejamento calculado em tempo real, sem reserva persistida de atividades.
