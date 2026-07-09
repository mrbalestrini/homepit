# Decisions

## 2026-07-09 - catalogo fixo e plano efetivo

- O catalogo comercial desta entrega usa cinco slugs fixos: `free`, `standard`, `bronze`,
  `silver` e `gold`.
- O SuperAdmin pode editar precos e limites desses planos, mas nao criar novos tipos.
- O plano efetivo do usuario e resolvido pela assinatura ativa na data atual; sem assinatura
  ativa, o fallback obrigatorio e `Free`.

## 2026-07-09 - historico de assinaturas com uma ativa por vez

- Assinaturas sao administradas manualmente em tabela propria com historico preservado.
- O sistema rejeita sobreposicao de assinaturas ativas para o mesmo usuario.
- Valores pagos podem ser `0,00` para acomodar vouchers, testes e descontos totais.

## 2026-07-09 - cota de imagens por retencao

- A cota de imagens governadas por plano vale inicialmente para imagens privadas de
  atividades e prompts.
- O upload novo nao e bloqueado por excesso; o sistema degrada as imagens mais antigas fora
  da janela de retencao em qualidade original.
- Foto de perfil, universo e imagens institucionais ficam fora da cota nesta entrega.

## 2026-07-09 - conteúdo visível orientado à pessoa usuária

- Textos de interface devem informar apenas o que ajuda a pessoa a entender, decidir ou
  concluir a ação atual.
- Detalhes de implementação, arquitetura e evolução planejada não devem aparecer na UI,
  exceto quando alterarem uma decisão ou consequência relevante para a pessoa.
- A diretriz compartilhada está em `.specs/shared/ui-ux-copy.md` e é obrigatória para
  mudanças de conteúdo visível no frontend.
