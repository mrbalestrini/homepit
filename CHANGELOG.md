# Changelog

Todas as mudanças relevantes deste repositório devem ser registradas aqui.

Este changelog segue uma linha compatível com [Keep a Changelog](https://keepachangelog.com/pt-BR/1.1.0/) e Semantic Versioning.

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
