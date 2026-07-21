---
description: >
  Audita e migra um repositório existente para o padrão Axis, BC por BC: mapeia os bounded contexts,
  apresenta o plano, dispara um agente axis-bc-migrator por BC e compila o relatório consolidado
  (corrigidos / pendentes de decisão). Args opcionais: caminho do repo (default: cwd), "report-only"
  para auditar sem corrigir, "parallel" para workers em paralelo (exige worktrees).
---

Você é o **orquestrador da auditoria Axis** deste repositório. Execute as fases abaixo NA ORDEM, no
contexto principal (os workers não podem acionar subagentes — o fan-out é seu).

Argumentos recebidos: `$ARGUMENTS` (caminho do repo alvo; flags `report-only` e/ou `parallel`).

## Fase 1 — Mapear os BCs (plano)

1. Faça o levantamento do repositório alvo (read-only). Se a divisão em BCs não for óbvia pela
   estrutura de pastas/projetos, acione o agente `axis-architect` para propor a divisão; caso
   contrário, derive-a você mesmo da solução (.sln/.slnx) e das pastas.
2. Identifique também os **arquivos compartilhados** (solution, Directory.Packages.props,
   Directory.Build.props, csproj com referências cruzadas) — eles ficam FORA do escopo dos workers
   por padrão.
3. Apresente ao usuário o plano: a lista de BCs (nome + pastas/projetos de cada um), a ordem de
   execução, o fix level (`fix` ou `report-only`) e o modo (sequencial ou paralelo).
   **Aguarde a aprovação do usuário antes da Fase 2** — use AskUserQuestion se houver escolhas a
   fazer (ex.: BCs a incluir/excluir, ordem, modo).

## Fase 2 — Fan-out (um worker por BC)

Para cada BC aprovado, acione o agente `axis-bc-migrator` com um prompt contendo o input contract
dele: `BC scope` (pastas/projetos exatos), `Shared-file policy: NO`, `Fix level` conforme o plano.

- **Default: sequencial** — um worker por vez, na ordem do plano. BCs são pastas disjuntas, mas os
  arquivos compartilhados não; sequencial elimina conflito de edição.
- **`parallel` (opt-in)**: só se o usuário pediu. Nesse caso use isolation worktree por worker e
  avise que a consolidação dos worktrees fica como passo manual/posterior.
- Se um worker falhar ou voltar vazio, registre e siga para o próximo — não aborte a auditoria toda.

## Fase 3 — Consolidação

Compile os relatórios dos workers em um único relatório final:

1. **Sumário executivo** — tabela: BC · corrigidos · pendentes · build/testes antes→depois.
2. **Pendentes de decisão (agregados)** — agrupe por tema (contratos, canais cross-BC, schema,
   arquivos compartilhados…), cada item com rule-id, path e a recomendação do worker. Esta é a lista
   que o usuário precisa decidir — apresente-a como tal.
3. **Arquivos compartilhados** — os ajustes que os workers registraram como necessários mas não
   fizeram; proponha aplicá-los você mesmo (no loop principal, com aprovação do usuário) ao final.
4. **Verificação global** — após todos os workers (e eventuais ajustes compartilhados), rode o build
   e os testes da solução inteira e reporte o estado final honestamente, incluindo falhas.

Não faça commit — entregue o relatório e deixe o commit para o usuário (sugira `/axis-review` ou o
agente `axis-reviewer` como gate antes do commit).
