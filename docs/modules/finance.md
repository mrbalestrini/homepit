# Módulo Financeiro

O Financeiro é um módulo interno compartilhado pelo Espaço, disponível em `/finance`. Ele organiza o fluxo mensal em Caixa, Cartões e Patrimônio.

## Capacidades atuais

- Períodos mensais únicos por Espaço, com criação de itens recorrentes ausentes ou duplicação explícita de todos os itens ativos.
- Lançamentos de caixa com entrada/saída, valor, data, verificação e classificações opcionais por categoria, núcleo e projeto.
- Recorrências mensais e anuais, incluindo geração para um período.
- Categorias financeiras padrão imutáveis e categorias personalizadas do Espaço. Excluir uma categoria personalizada apenas desvincula os registros associados.
- Cartões, compras, fechamento de fatura e lançamento consolidado no Caixa. A visão analítica evita contar a fatura e as compras duas vezes.
- Importação atômica de compras de cartão por JSON no formato `{"transactions":[...]}`, com revisão antes da gravação e criação de categorias inexistentes por nome.
- Patrimônio com bens do tipo imóvel, veículo ou outro e referências anuais de valor.

## Permissões

`Owner` e `Admin` gerenciam os registros do Espaço. `Member` gerencia somente o conteúdo que criou. `SuperAdmin` permanece somente leitura nos módulos dos Espaços.

## Limites atuais

Não há conciliação bancária, parcelamento, anexos de comprovante nem importação automática por SMS, XLS, OCR ou serviços externos. A integração externa planejada está documentada em [Integrações](../integrations/README.md); sua disponibilidade depende da liberação da feature e não substitui as rotas internas atuais.
