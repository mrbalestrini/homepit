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
- Exclui comentarios de qualquer pessoa.
- Edita somente os proprios comentarios.

### Administrador

- Compartilha a casa com administradores e membros.
- Cria, edita e exclui universos, projetos e atividades da casa.
- Pode editar atividades que nao foram criadas por ele.
- Exclui comentarios de qualquer pessoa.
- Edita somente os proprios comentarios.

### Membro

- Cria universos, projetos, atividades e comentarios.
- Edita e exclui somente universos, projetos e atividades criados por ele.
- Nao edita nem exclui projetos criados por outra pessoa.
- Exclui somente os proprios comentarios.
- Edita somente os proprios comentarios.

## Comentarios editados

O sistema não guarda historico de edicao de comentarios. Quando um comentario e editado, a interface mostra a tag `Editado`.
