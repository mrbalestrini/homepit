# Refactor: endurecer workflow de IA

## Contexto

O workflow de IA do HomePit ja possui `AGENTS.md`, memoria factual, regras compartilhadas,
mudancas por pasta e skills por responsabilidade. Ainda assim, o estado ativo de uma mudanca
dependia da leitura implicita de toda a pasta `changes/`, faltava uma validacao automatizada
do proprio workflow e as fontes de verdade para versao, contrato e endpoint local nao estavam
declaradas em um unico lugar.

## Objetivo

Reduzir ambiguidade operacional e aumentar a confianca do trabalho assistido por IA com
estado ativo explicito, validacao automatizada do workflow e governanca documental mais
deterministica.

## Escopo

- Criar um marcador explicito de mudanca ativa.
- Registrar fontes de verdade para workflow, versao, contrato e endpoint local.
- Adicionar um script de validacao do workflow de IA.
- Completar descritores de agentes para `architect`, `reviewer` e `tester`.
- Arquivar mudancas ja concluidas para diminuir ruido em `.specs/changes/`.
- Atualizar memoria e orientacoes relacionadas.

## Fora de escopo

- Alterar comportamento funcional da API ou do frontend.
- Harmonizar automaticamente todas as divergencias de infraestrutura existentes.
- Introduzir CI/CD novo ou integracao externa de agentes.

## Arquivos ou areas envolvidas

- `AGENTS.md`
- `.specs/`
- `.agents/skills/`
- `scripts/validate-ai-workflow.ps1`

## Comportamento que nao deve mudar

- O fluxo continua exigindo leitura contextual antes de editar codigo.
- As skills continuam sendo carregadas sob demanda, por responsabilidade.
- Mudancas sensiveis continuam exigindo plano proporcional e cuidado extra.

## Riscos

- Regras novas demais podem aumentar friccao se a validacao for excessivamente rigida.
- Arquivar mudancas antigas sem preservar contexto quebraria rastreabilidade.
- Fontes de verdade mal escolhidas poderiam cristalizar divergencias incorretas.

## Plano

1. Criar a mudanca e registrar a governanca desejada.
2. Adicionar `active-change.md` e `sources-of-truth.md`.
3. Arquivar mudancas concluídas e manter apenas a mudanca ativa em `changes/`.
4. Implementar o script `validate-ai-workflow.ps1`.
5. Completar `openai.yaml` faltantes e atualizar memoria/orientacoes.
6. Executar validacoes estruturais do novo workflow.

## Testes e validacao

- Executar `.\scripts\validate-ai-workflow.ps1`.
- Validar as skills com o `quick_validate.py` ja usado no bootstrap.
- Conferir `git status` ao final para garantir escopo contido.

## Criterios de aceite

- Existe um unico marcador explicito de mudanca ativa.
- Mudancas concluidas deixam de poluir `.specs/changes/`.
- O workflow possui validacao automatizada reproduzivel.
- As fontes de verdade deixam de ficar implícitas em notas dispersas.
- Todas as skills do projeto possuem descritor de agente consistente.

## Decisao final

Fortalecer a governanca do workflow sem introduzir nova stack nem acoplar o processo a uma
ferramenta externa obrigatoria.
