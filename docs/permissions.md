# Perfis e Permissoes

HomePit separa permissoes em dois niveis: perfil do sistema e perfil dentro de cada casa.

## Perfil do sistema

- `Admin`: concedido automaticamente ao primeiro usuario cadastrado. Este perfil fica guardado para futuras telas de administracao da ferramenta.
- `User`: perfil padrao dos demais usuarios.

## Perfis da casa

### Proprietario

- Edita e exclui a casa.
- Compartilha a casa com administradores e membros.
- Cria, edita e exclui universos, projetos e atividades da casa.
- Cria, edita e exclui prompts e categorias de prompts da casa.
- Exclui comentarios de qualquer pessoa.
- Edita somente os proprios comentarios.

### Administrador

- Compartilha a casa com administradores e membros.
- Cria, edita e exclui universos, projetos e atividades da casa.
- Cria, edita e exclui prompts e categorias de prompts da casa.
- Pode editar atividades que nao foram criadas por ele.
- Exclui comentarios de qualquer pessoa.
- Edita somente os proprios comentarios.

### Membro

- Cria universos, projetos, atividades e comentarios.
- Cria prompts e categorias de prompts.
- Edita e exclui somente universos, projetos e atividades criados por ele.
- Edita e exclui somente prompts e categorias de prompts criados por ele.
- Nao edita nem exclui projetos criados por outra pessoa.
- Nao edita nem exclui prompts ou categorias de prompts criados por outra pessoa.
- Exclui somente os proprios comentarios.
- Edita somente os proprios comentarios.

## Regras especificas do Banco de Prompts

- Todo prompt precisa permanecer com pelo menos uma categoria.
- Ao excluir uma categoria:
  - se todos os prompts afetados ainda tiverem outra categoria, a exclusao pode ocorrer sem substituicao;
  - se algum prompt ficaria sem categoria, e obrigatorio informar uma categoria de substituicao da mesma casa.
- O universo no prompt e opcional. Um membro com permissao pode limpar o universo manualmente, e a exclusao de um universo no modulo de projetos nao remove os prompts vinculados.

## Comentarios editados

O sistema não guarda historico de edicao de comentarios. Quando um comentario e editado, a interface mostra a tag `Editado`.
