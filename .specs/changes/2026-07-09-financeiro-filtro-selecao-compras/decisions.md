# Decisoes

1. O filtro das compras de cartao vai operar apenas no frontend, sem novos parametros de API.
2. A correspondencia textual vai considerar os principais campos abertos exibidos na linha da
   compra, incluindo titulo, comerciante, categoria, classificacao, fatura e valores.
3. O checkbox de selecionar todos vai atuar apenas sobre os itens visiveis no momento, sem
   limpar selecoes feitas anteriormente.
4. O fechamento da fatura vai ganhar uma acao explicita de selecao total para evitar cliques
   repetidos item a item.
