# Decisoes

1. O estado ativo do workflow passara a ser declarado explicitamente em
   `.specs/active-change.md`.
2. Mudancas concluídas devem ser movidas para `.specs/archive/` para que `.specs/changes/`
   represente somente trabalho potencialmente ativo.
3. As fontes de verdade do workflow ficarao centralizadas em
   `.specs/shared/sources-of-truth.md`.
4. A versao oficial do produto sera tratada separadamente da versao do contrato/API.
5. O script `scripts/validate-ai-workflow.ps1` sera a validacao local padrao da estrutura
   de IA e da consistencia documental minima.
6. Todas as skills locais devem possuir `agents/openai.yaml` para roteamento consistente.
7. Divergencias documentadas mas ainda nao harmonizadas no produto podem aparecer como
   `warning` na validacao, desde que nao comprometam a interpretacao do workflow.
