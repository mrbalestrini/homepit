# Conteúdo de interface e UX

## Objetivo

Todo texto visível na interface deve ajudar a pessoa a entender o que vê, decidir ou
concluir uma ação. A interface não deve explicar sua própria estrutura, antecipar
decisões de implementação ou preencher espaço com frases genéricas.

## Regras obrigatórias

- Escreva a partir da necessidade e do vocabulário de quem usa a tela, não da
  arquitetura, do roadmap ou da implementação.
- Mostre somente a informação necessária naquele momento para a pessoa agir com
  segurança e entender a consequência relevante da ação.
- Nunca exponha detalhes internos, como conversões, processamento, limites técnicos
  aplicados internamente, estrutura de componentes, evolução planejada ou preparação
  para funcionalidades futuras, salvo se isso mudar uma decisão da pessoa.
- Não use textos de preenchimento ou autoexplicação da tela, como "área preparada para
  futuras abas", "campos organizados para crescer" ou equivalentes.
- Para uploads, informe os formatos aceitos e, quando necessário para a escolha do
  arquivo, o tamanho máximo. Não descreva normalização, conversão, compressão ou
  redimensionamento internos.
- Prefira frases curtas, diretas e específicas. Um subtítulo, ajuda contextual, estado
  vazio, aviso ou confirmação deve responder a uma dúvida real ou orientar a próxima
  ação.
- Preserve informações importantes de consequência, irreversibilidade, permissão,
  prazo, custo, limite de uso ou impacto sobre dados, em linguagem clara e não técnica.

## Revisão antes de entregar UI

Para cada texto novo visível ao usuário, confirme:

1. Ele ajuda a pessoa a tomar uma ação ou compreender um resultado agora?
2. Ele evita detalhes de engenharia e de planejamento interno?
3. Ele poderia ser removido sem reduzir a clareza ou a segurança? Se sim, remova-o.
4. A linguagem está específica ao contexto da tela, em vez de genérica?

## Exemplos

| Evitar | Preferir |
| --- | --- |
| A área de perfil já nasce preparada para futuras abas. | Remover o texto. |
| O sistema converte a imagem para WEBP e limita a 2000 px. | Formatos aceitos: JPG, PNG, WEBP, GIF ou BMP. |
| Campos organizados para crescer junto com futuras abas. | Remover o texto. |

