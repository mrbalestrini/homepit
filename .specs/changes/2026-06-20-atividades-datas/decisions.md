# Decisoes

1. `DueDate` sera opcional e persistido como `DateOnly` na coluna `activities.DueDate`.
2. `CreatedAt` continua vindo da base auditavel e sera apenas exposto no contrato.
3. A interface mostrara `Prazo esperado` no editor, `Prazo` nos cards/lista e `Criada em`
   no detalhe da atividade.
4. O formatter de data-only usara UTC para evitar deslocamento de fuso no navegador.
5. O startup de migrations ignora providers nao relacionais para manter os testes com
   InMemory operando sem depender de APIs relacionais do EF Core.
