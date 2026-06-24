# Decisoes

1. `IsArchived` será armazenado diretamente no prompt e não em uma tabela auxiliar.
2. A listagem padrão de prompts retornará apenas itens ativos; a visão arquivada virá por
   filtro explícito.
3. Arquivar e desarquivar manterão o prompt íntegro, incluindo categorias, imagem e
   autoria.
4. A permissão para arquivar/desarquivar seguirá a mesma regra de gerenciamento do prompt.
5. A preferência de imagens será persistida em `localStorage` somente quando estiver
   escondida; voltar a mostrar imagens remove a chave.
6. A ausência da chave de preferência significa `mostrar imagens`.
