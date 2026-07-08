# Changelog

Todas as mudanças relevantes deste repositório devem ser registradas aqui.

Este changelog segue uma linha compatível com [Keep a Changelog](https://keepachangelog.com/pt-BR/1.1.0/) e Semantic Versioning.

## [1.8.0] - 2026-07-08

### Added
- A seção de cartões do financeiro agora aceita importar múltiplas compras por JSON, com arquivo de exemplo, revisão editável e criação automática de categorias faltantes.

### Changed
- A área central do módulo financeiro passou a alternar entre `Caixa` e `Cartões` por abas locais, mantendo `Patrimônio` sempre visível abaixo da navegação.
- A importação em lote de compras de cartão passou a validar universo e projeto por nome antes do envio e só grava o lote inteiro quando todas as linhas estiverem válidas.
- Faturas fechadas do cartão passaram a ser tratadas como verificadas no caixa mensal, inclusive no lançamento consolidado e no resumo do período.
- Uploads comuns de imagem agora são normalizados no backend para `WEBP`, com limite máximo de `2000 px` por lado e rejeição de animações em foto de perfil, universo, atividade, prompt e imagens institucionais não SEO.

## [1.7.1] - 2026-07-07

### Added
- O módulo financeiro passou a ter uma seção de categorias por household, com 12 categorias padrão fixas e gestão de categorias personalizadas.

### Changed
- O topo do financeiro passou a oferecer `Inserir Recorrências` como ação principal e um atalho dedicado para a gestão de recorrências.
- A gestão de recorrências saiu da página principal e passou a viver em uma modal quase tela cheia, mantendo a lista e as ações de edição em um espaço dedicado.
- A edição de recorrências dentro da janela dedicada voltou a aceitar cliques nos campos sem fechar a modal principal nem a modal de edição.
- A copy do módulo financeiro foi revisada para pt-BR com acentuação correta em botões, títulos, mensagens e textos de apoio.
- Lançamentos de caixa, recorrências e compras de cartão agora aceitam categoria opcional e exibem a classificação escolhida nas tabelas do financeiro.
- As tabelas do financeiro passaram a aceitar edição inline em campos rápidos, com atualização otimista imediata, rollback em erro e sincronização pontual sem recarregar o módulo inteiro após cada edição.
- A exclusão de lançamentos de caixa e compras de cartão foi simplificada para confirmação direta, sem exigir digitação do título.
- As seções de caixa e compras de cartão agora permitem selecionar vários registros para exclusão em lote.

## [1.7.0] - 2026-07-06

### Added
- Novo módulo interno `/finance` compartilhado por household, com seções de resumo, caixa, recorrências, cartões e patrimônio.
- API protegida em `/api/finance`, com CRUD para períodos, lançamentos, recorrências, bens, referências anuais, cartões, compras e faturas.
- Persistência financeira dedicada no banco para períodos mensais, templates recorrentes, patrimônio, avaliações anuais e fluxo de cartão de crédito.

### Changed
- O shell do workspace agora ativa `Financeiro` como rota real em vez de espaço reservado.
- O resumo mensal passou a separar fluxo de caixa e visão analítica de gastos, somando compras de cartão sem duplicar a fatura consolidada.
- `Universe` e `Project` passaram a funcionar como classificações opcionais no financeiro, com validação e nulificação segura ao excluir vínculos.

## [1.6.1] - 2026-06-26

### Changed
- Os comentários no detalhe da atividade passaram a exibir o avatar real do autor quando a foto de perfil existe, com leitura protegida da imagem do usuário correspondente.
- A coluna de responsável na tabela de projetos, o detalhe da atividade e os cards do kanban agora exibem o avatar real do membro quando a foto existe, com fallback seguro para iniciais.
- As listas de membros da casa e o diálogo de compartilhamento passaram a reutilizar o avatar protegido dos participantes, com cache compartilhado para evitar downloads repetidos da mesma foto.

## [1.6.0] - 2026-06-24

### Added
- O módulo `/gsm` passou a registrar plano da linha e custo mensal opcional, com persistência na API e no banco.
- O módulo `/gsm` passou a registrar o histórico de recargas, com informacao, edicao, exclusao e recalculo automatico da ultima recarga.
- O formulario GSM passou a aceitar `DaysWithoutRecharge` para projetar a proxima recarga.
- O banco de prompts passou a permitir arquivar e desarquivar prompts, com visão dedicada para itens arquivados.
- O banco de prompts passou a permitir ocultar e mostrar imagens com preferência local persistida no navegador.

### Changed
- A gestão GSM saiu do layout em cards e passou a usar tabela responsiva com título, número, plano, custo, datas e ações.
- O texto principal da tela foi simplificado para focar no gerenciamento de números de telefone da casa.
- A listagem GSM agora combina tabela no desktop com cards no mobile, e exibe a proxima recarga com destaque de atraso.
- `LastRechargeOn` deixou de ser editado diretamente no formulario e passou a ser um resumo do historico.
- A listagem padrão do banco de prompts passou a exibir apenas prompts ativos.
- O frame visual de prompt agora pode ser ocultado no card e no detalhe sem buscar a imagem protegida.

## [1.5.0] - 2026-06-23

### Added
- Novo módulo interno `/gsm` para gestão compartilhada de números GSM por household, com cadastro completo de título, número, descrição, aquisição, última recarga e status.
- API protegida em `/api/gsm-numbers`, com CRUD completo, tenancy por `X-Household-Id` e regras de autoria alinhadas aos demais módulos da casa.
- Máscara e normalização de números com DDI opcional, além do contador textual de tempo desde a última recarga na interface.

### Changed
- A navegação lateral do workspace agora expõe o módulo `GSM` como recurso ativo da operação da casa.
- O contrato OpenAPI e a suíte de testes backend passaram a cobrir o novo recurso de números GSM e o enum de status correspondente.

## [1.4.0] - 2026-06-22

### Added
- Atividades agora aceitam uma imagem unica, com upload protegido, leitura protegida e remocao pelo dashboard de projetos.
- O card, a lista e o painel de detalhes de atividade passam a exibir a imagem quando ela existe.
- A API e o contrato OpenAPI ganharam os endpoints de upload, leitura e exclusao de imagem por atividade.

### Changed
- A exclusao de atividade, projeto ou universo agora remove os binarios de imagem vinculados para evitar anexos orfaos.

## [1.3.0] - 2026-06-20

### Added
- Atividades agora expõem `CreatedAt` e aceitam `DueDate` opcional no contrato, com persistência em banco, OpenAPI e dashboard.
- O formulário, os cards, a lista e o painel de detalhes de atividade passaram a mostrar o prazo esperado e a data de criação.

### Changed
- A formatação de datas sem horário no frontend passou a usar UTC para evitar deslocamento de fuso.

### Fixed
- A imagem protegida de prompts passou a enviar `X-Household-Id` no card e no detalhe, evitando `400` em contas com mais de uma casa.

## [1.2.1] - 2026-06-15

### Added
- Nova página institucional pública em `/`, com conteúdo de aquisição, metadata de SEO e imagens responsivas.
- CMS separado em `/admin/institutional`, com edição estruturada, publicação imediata e acesso exclusivo ao SuperAdmin.
- API pública e administrativa para conteúdo institucional, incluindo imagens públicas versionadas em object storage.

### Changed
- A seleção da casa ativa agora é lembrada por usuário no navegador, com limpeza automática quando a casa salva não existe mais e fallback seguro para a casa existente mais recente.

## [1.2.0] - 2026-06-12

### Changed
- As exclusões de casa, universo e projeto agora usam modais de confirmação com resumo explícito dos impactos; casa e universo exigem digitação do nome antes da exclusão.

## [1.1.0] - 2026-06-10

### Changed
- O texto de prompt passou a aceitar até 20000 caracteres, com validação e armazenamento alinhados ao novo limite.
- Ajustei a concordância dos textos de categoria no banco de prompts para respeitar singular e plural quando o total é 1.
- Removi da interface de categorias o texto explicativo sobre prompts que exigiriam substituição ao excluir uma categoria.
- Corrigi a largura dos cards da tela de prompts para respeitar o masonry responsivo e preencher a coluna por completo.
- Removi os cards redundantes de "Atalhos" e "Retirada de membros" da administração da casa.
- Ajustei a tipografia dos selects de filtro das atividades para reduzir o corte dos textos sem alterar a largura visual dos campos.

### Added
- Nova página de administração da casa em `/household`, com visão em estilo dashboard para nome, membros, permissões e atalhos.
- Atalho de acesso à administração da casa no topo e na navegação lateral compartilhada.
- Bloco de membros preparado para futura retirada individual sem apagar o histórico de ações.
- Navegação entre casas e criação de nova casa diretamente no topo da administração.
- Endpoints para editar e remover membros da casa com proteção de proprietário e preservação de histórico.

## [1.0.0] - 2026-06-10

### Added
- Marco inicial do projeto HomePit.
- Política de versionamento e manutenção de changelog.
- Alinhamento da versão do app para `1.0.0`.
