# Feature: imagem de atividade com preview compacto e viewer ampliado

## Contexto

Atividades agora aceitam imagem e o fluxo de edição/detalhe precisa ficar mais compacto e legível.

## Objetivo

Remover o bloco vazio do editor quando não houver imagem e permitir abrir a imagem em popup ampliado a partir do kanban e do detalhe.

## Escopo

- Compactar o editor de atividade quando não existir imagem.
- Permitir abrir a imagem atual em um viewer com zoom e arraste.
- Reaproveitar o mesmo comportamento no card do kanban e no detalhe da atividade.

## Fora de escopo

- Mudanças de API, contrato ou banco.
- Suporte a múltiplas imagens por atividade.
- Edição/corte da imagem no viewer.

## Arquivos ou areas envolvidas

- `apps/web/src/features/projects/project-dashboard-workspace.tsx`
- `apps/web/src/features/projects/protected-activity-image.tsx`
- `apps/web/src/features/projects/activity-image-viewer.tsx`
- `apps/web/src/features/projects/project-dashboard-workspace.test.tsx`

## Regras de negocio

- A atividade continua com no máximo uma imagem privada.
- O upload existente substitui a imagem atual.
- O viewer é somente leitura.

## Riscos

- Banco: nenhum.
- API/contrato: nenhum.
- Autenticacao/autorizacao: nenhum.
- Frontend: o viewer precisa coexistir com o sheet de detalhe sem quebrar o foco.
- Deploy/ambiente: nenhum.

## Plano

- Criar um viewer compartilhado com zoom, arraste e reset.
- Tornar o frame da imagem clicavel apenas quando houver imagem real.
- Condicionar o upload do editor para aparecer somente quando necessario.
- Cobrir os fluxos com testes de interação.

## Testes

- Card do kanban abre o viewer ao clicar na imagem.
- Detalhe da atividade abre o viewer ao clicar na imagem.
- Editor sem imagem mostra apenas o input.
- Editor com imagem mostra preview e ações compactas.
- Viewer responde a zoom, pan e reset.

## Criterios de aceite

- Sem imagem, o editor nao reserva espaco grande.
- Com imagem, o clique abre uma popup maior.
- O zoom e o arraste funcionam no viewer.

## Decisao final

- Solucao implementada somente no frontend.
