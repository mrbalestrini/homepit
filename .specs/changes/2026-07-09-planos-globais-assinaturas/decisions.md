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

## 2026-07-10 - ownership comercial da casa separado do papel Owner

- O papel `HouseholdRole.Owner` continua exclusivo para permissões de gestão da casa.
- A propriedade comercial da casa passou a ser persistida em `Household.CreatedByUserId`,
  sem fluxo de transferência nesta entrega.
- A contagem de `casas próprias`, a exclusão de conta com purge da estrutura e as cotas de
  universos e projetos em casas compartilhadas passam a seguir o criador da casa.
- Para casas legadas, o backfill do criador prioriza `Owner` ativo mais antigo, depois o
  membro ativo mais antigo da própria casa, e só então cai para o primeiro usuário elegível
  da plataforma.

## 2026-07-10 - cotas totais por autoria e convidados ativos

- As cotas de `universos` e `projetos` deixaram de ser estruturais e passaram a contar o
  total criado pela pessoa usuária, independente da casa ou do universo onde o item está.
- Estourar a cota total bloqueia apenas novas criações; edição e exclusão de itens já
  existentes continuam seguindo autoria e papel na casa.
- A cota de convidados usa memberships ativas em casas criadas pela pessoa, exclui a
  própria membership do criador e libera vaga quando o vínculo fica inativo.
- O novo campo `MaxInvitedMembers` dos planos aceita `null` como ilimitado; nesta entrega,
  o seed padrão dos cinco planos nasce sem limite explícito para convidados.
