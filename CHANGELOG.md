# Changelog

Todas as mudanças relevantes deste repositório devem ser registradas aqui.

Este changelog segue uma linha compatível com [Keep a Changelog](https://keepachangelog.com/pt-BR/1.1.0/) e Semantic Versioning.

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
