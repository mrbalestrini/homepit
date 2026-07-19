# Spec - Organiza Club

## Objetivo

Reidentificar integralmente o produto HomePit como Organiza Club e substituir a hierarquia
de tenant `Casa -> Universo -> Projeto` por `Espaço -> Núcleo -> Projeto`, representada no
código por `Space -> Core -> Project`.

## Escopo

- Marca, ativos, copy, temas, acessibilidade e superficies web.
- Entidades, contratos, rotas, tenancy, quotas, integracoes, testes e documentacao.
- Solution, projetos e namespaces `OrganizaClub.*`.
- Baseline unica do EF Core no schema `organiza_club`, sem compatibilidade de dados.
- Infraestrutura com nomes, bucket, servicos e dominios da Organiza Club.

## Fora de escopo

- Criar um modulo de Estudos.
- Compatibilidade, aliases ou redirects para nomes e contratos antigos.
- Renomear o repositorio remoto ou a pasta local antes dos gates finais.
- Executar reset destrutivo de ambientes sem alvo explicito e validado.

## Criterios de aceite

- Nenhum contrato ativo exposto usa HomePit, Household, Universe ou `X-Household-Id`.
- A hierarquia funcional e tecnica e `Space -> Core -> Project`, preservando as regras de
  ownership, papeis, convites, autoria, quotas, cascatas e membros inativos.
- Interface usa Organiza Club, DM Sans, cores oficiais e temas `system`, `light` e `dark`.
- REST, OAuth e MCP usam os novos scopes, recursos, prefixos, campos e rotas.
- A baseline e descoberta automaticamente pelo EF e aplica em PostgreSQL vazio.
- Frontend e backend passam por testes, lint, build, publish e auditoria de termos.

## Fontes da marca

- `docs/brand/Organiza_Club_Guia_de_Identidade_Visual_v1.pdf`
- `docs/brand/CopyBase.txt`
- `docs/brand/vectors/`
- `docs/brand/manifest.md`
