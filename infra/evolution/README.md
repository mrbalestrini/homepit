# Evolution API

Recurso separado para WhatsApp. A API do Organiza Club chama `http://organiza-club-evolution-api:8080` pela rede `organiza_club_net`.

## MVP

- Envio de resumo diário de atividades abertas.
- Instância padrão: `organiza-club`.
- O endpoint configurável na API é `EvolutionApi__SendTextPathTemplate=/message/sendText/{instance}`.

Depois de subir o recurso, crie/conecte a instância pelo painel/API da Evolution e use a mesma API key no recurso `apps/api`.
