# Perfis e Permissoes

Organiza Club separa permissoes em dois niveis: perfil do sistema e perfil dentro de cada espaço.

## Perfil do sistema

- `Admin`: concedido automaticamente ao primeiro usuario cadastrado. Este perfil fica guardado para futuras telas de administracao da ferramenta.
- `SuperAdmin`: acesso global quando configurado via `.env`/Coolify. Pode navegar por
  qualquer espaço e modulo existente para suporte e testes, mas continua sem criar, editar,
  excluir ou compartilhar dados dos espaços.
- `User`: perfil padrao dos demais usuarios.

## CMS institucional

- Somente `SystemRole.SuperAdmin` acessa `/admin/institutional`.
- O SuperAdmin pode editar e publicar o conteudo global da pagina institucional e gerenciar
  as imagens de hero e destaque.
- `SystemRole.Admin`, `SystemRole.User` e acessos anonimos nao podem ler nem alterar o painel.
- A pagina e as imagens publicadas possuem leitura anonima.
- Esta e a unica excecao de escrita ao perfil global SuperAdmin; as regras dos espaços nao
  mudam.

## Perfis do espaço

### Proprietario

- Edita e exclui o espaço.
- Compartilha o espaço com administradores e membros.
- Edita o papel de membros do espaço e remove membros sem apagar o historico.
- Cria, edita e exclui núcleos, projetos e atividades do espaço.
- Cria, edita e exclui prompts e categorias de prompts do espaço.
- Exclui comentarios de qualquer pessoa.
- Edita somente os proprios comentarios.

### Administrador

- Compartilha o espaço com administradores e membros.
- Cria, edita e exclui núcleos, projetos e atividades do espaço.
- Cria, edita e exclui prompts e categorias de prompts do espaço.
- Pode editar atividades que nao foram criadas por ele.
- Exclui comentarios de qualquer pessoa.
- Edita somente os proprios comentarios.

### Membro

- Cria núcleos, projetos, atividades e comentarios.
- Cria prompts e categorias de prompts.
- Edita e exclui somente núcleos, projetos e atividades criados por ele.
- Edita e exclui somente prompts e categorias de prompts criados por ele.
- Nao edita nem exclui projetos criados por outra pessoa.
- Nao edita nem exclui prompts ou categorias de prompts criados por outra pessoa.
- Exclui somente os proprios comentarios.
- Edita somente os proprios comentarios.

## Regras especificas do Banco de Prompts

- Todo prompt precisa permanecer com pelo menos uma categoria.
- Ao excluir uma categoria:
  - se todos os prompts afetados ainda tiverem outra categoria, a exclusao pode ocorrer sem substituicao;
  - se algum prompt ficaria sem categoria, e obrigatorio informar uma categoria de substituicao da mesmo espaço.
- O núcleo no prompt e opcional. Um membro com permissao pode limpar o núcleo manualmente, e a exclusao de um núcleo no modulo de projetos nao remove os prompts vinculados.

## Comentarios editados

O sistema não guarda historico de edicao de comentarios. Quando um comentario e editado, a interface mostra a tag `Editado`.
