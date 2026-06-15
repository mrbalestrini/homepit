# Decisoes

1. A selecao da casa ativa sera persistida no frontend em `localStorage`.
2. A chave de armazenamento sera isolada por usuario para evitar cruzamento entre contas
   no mesmo navegador.
3. A regra de resolucao e limpeza sera compartilhada entre os fluxos de projetos e
   prompts.
4. O fallback usara a casa existente mais recente da lista atual quando a selecao salva
   nao for valida.
5. Erros de `localStorage` serao tratados de forma segura para nao bloquear a abertura da
   aplicacao.
