# Regras de dominio

## FATO OBSERVADO

- Um usuario pode pertencer a varias casas; com mais de uma, a API exige `X-Household-Id`.
- O primeiro usuario comum cadastrado recebe `SystemRole.Admin`; os seguintes recebem `User`.
- Cadastro pode criar uma casa inicial opcional e tornar o usuario `Owner`.
- Casa deve manter ao menos um proprietario ativo.
- Somente `Owner` edita/exclui casa e gerencia papel ou remocao de membros.
- `Owner` e `Admin` compartilham casa; compartilhamento aceita apenas `Admin` ou `Member`.
- Remover membro marca o vinculo como inativo e preserva historico.
- `Owner` e `Admin` gerenciam conteudo da casa; `Member` gerencia apenas conteudo criado por ele.
- Comentario so pode ser editado pelo autor; `Owner` e `Admin` podem excluir comentarios de
  outras pessoas.
- Hierarquia de projetos: `Universe > Project > Activity > PendingItem`.
- `Activity` expõe `CreatedAt` auditavel e pode ter `DueDate` opcional como prazo esperado.
- `Activity` pode ter no maximo uma imagem privada; o upload substitui o anexo anterior e
  a exclusao de atividade, projeto ou universo deve limpar o binario correspondente.
- Contagem de atividades do projeto considera apenas atividades nao concluidas.
- Responsavel de atividade deve ser membro ativo da mesma casa.
- Status: `NaoIniciada`, `EmAndamento`, `Concluido`.
- Prioridade: `Baixa`, `Media`, `Alta`, `Urgente`.
- Todo prompt pertence a uma casa, exige titulo, texto e ao menos uma categoria valida.
- Texto do prompt aceita no maximo 20000 caracteres.
- Titulo e URL do link devem existir juntos; URL deve ser HTTP ou HTTPS.
- Universo do prompt e opcional; excluir universo limpa o vinculo sem excluir o prompt.
- Categoria e unica por nome dentro da casa.
- Excluir categoria exige substituta quando algum prompt ficaria sem categoria.
- Imagens privadas pertencem ao usuario, universo ou prompt correspondente.
- SuperAdmin lista casas e conteudo globalmente, mas operacoes de escrita nos modulos das
  casas sao proibidas.
- SuperAdmin e o unico perfil que pode escrever no CMS institucional global.
- A pagina institucional possui entre 1 e 6 beneficios e entre 1 e 6 etapas ordenadas.
- Resumo diario considera atividades abertas atribuidas ao membro e ate tres pendencias
  abertas por atividade.

## INFERÊNCIA

- Regras de autoria e casa sao invariantes centrais e devem ser testadas em qualquer novo
  modulo que armazene dados compartilhados.

## NÃO IDENTIFICADO

- Regras de conclusao, edicao e exclusao de pendencias alem das rotas atuais.
- Configuracao por interface das preferencias de notificacao.
- Regras implementadas para financeiro e supermercado.
- Transferencia explicita de propriedade de uma casa.
