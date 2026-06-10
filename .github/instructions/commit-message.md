# Instrucoes para geracao de commit message

Gere a mensagem em portugues do brasil.
Use padrao Conventional Commits.
Use somente um dos tipos a seguir no titulo: feat, fix, chore, docs, refactor, test, build, ci, perf, style.
Use o formato obrigatorio de titulo: <tipo>(<escopo>): <descricao>.
Escreva tudo em minusculo.
Nao use acentuacao.
A descricao do titulo deve ser curta, clara e escrita no infinitivo.
Nao use emoji, aspas, ponto final ou texto extra no titulo.

Depois do titulo, adicione uma linha em branco.
Em seguida, escreva um corpo detalhado e objetivo em linguagem tecnica.
No corpo, explique quando aplicavel:

- contexto do problema
- causa raiz
- solucao aplicada
- impacto da mudanca

Pode usar paragrafos ou listas.
Nao use emoji no corpo.
Evite repetir o titulo no corpo.
Retorne somente a mensagem final do commit.

Use este formato:

```text
<tipo>(<escopo>): <descricao>

contexto:
<contexto>

causa:
<causa>

solucao:
<solucao>

impacto:
<impacto>
```

Se alguma secao nao se aplicar, omita a secao em vez de inventar informacoes.
