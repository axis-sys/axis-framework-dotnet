---
name: axis-docs
description: >
  The method for writing and maintaining human technical documentation the Axis way — README, runbook, API
  doc, onboarding. Method skill (transversal, no `produce/` artifact of its own): the doc lives next to the
  code, versioned, and is reviewed in the SAME pull request as the behavior change. Central principle —
  **LINK, don't duplicate**: each concern has a single source and the doc references it instead of restating
  it — the HTTP contract in the OpenAPI/reference UI (fed by the `///` XML docs), each invariant in its rule
  `id` under `rules/`, decisions in the ADRs (kept outside the repo), and every code snippet injected from a
  real tested scaffold. Use when creating/updating a README, writing an operational runbook, framing
  endpoints, or building the repo's entry sequence. It does NOT own the contracts/decisions themselves
  (→ `axis-webapi-controllers` for the OpenAPI surface, `axis-rules` for the rule base) — only the prose that
  stitches them together.
---

# AxisDocs — write docs that LINK, never duplicate

Documentation is a **map to single sources**, not a second copy of them. Every concern is written once —
the invariant in its rule, the HTTP contract in the OpenAPI reference, the decision in its ADR — and the doc
**references** it. When a doc restates something that already has a home, it is a duplicate that will rot;
delete it and link instead.

> **Same-PR rule.** The doc lives next to the code and changes in the SAME pull request as the behavior it
> describes. A README repository-layout block, an endpoint's `///` prose, a runbook step — each moves with
> the code it maps, never in a follow-up.

## When to use

- Write or trim a **README** (repo root or a package README under `docs/en-us/**`).
- Write an **operational runbook** (often born from a lesson or a piece of debt).
- **Frame endpoints / API prose** — but as `///` XML docs that feed the OpenAPI reference, not as a hand-kept HTTP catalog.
- Build the **onboarding / entry sequence** of the repo (the learning order, the documentation map).

Not for: the contracts/decisions themselves (the OpenAPI surface → `axis-webapi-controllers`; the rule base
→ `axis-rules`; a decision record → the ADR flow, kept outside this repo).

## The owned rules — map, don't restate

Each row is the single source; write the doc AGAINST it and cite the `id`.

### README & the docs hub

| What the method enforces | Rule |
|---|---|
| The README carries only pitch, a pointer to the docs UI, repository layout, run/test instructions, a tooling pointer and a documentation map — domain/architecture/auth prose is never re-explained there; the layout block moves in the same PR as a topology change | [process-readme-docs-policy](../../rules/conventions/process/process-readme-docs-policy.yaml) |
| The rendered documentation UI over the transformer-shaped OpenAPI document is the project's documentation hub; the info-transformer description is the single narrative of the API (the README links to it), and both the raw endpoint and the UI are exposed only in development | [edge-openapi-doc-hub](../../rules/conventions/edge/edge-openapi-doc-hub.yaml) |

### API prose = the `///` that feeds OpenAPI

| What the method enforces | Rule |
|---|---|
| Controller and action `///` XML docs are contract documentation (`<summary>`/`<remarks>`/`<param>`/`<response>`), the sanctioned prose exception under the minimal-comment policy, piped into the generated OpenAPI document by solution-wide doc-file generation — so API docs are written on the code, not as a separate page to drift | [edge-xml-docs-feed-openapi](../../rules/conventions/edge/edge-xml-docs-feed-openapi.yaml) |

## Where the prose gets its single sources (LINK, don't duplicate)

The doc never invents content — it points at the source that owns it:

- **From the rule base.** Every invariant lives once in a rule under `rules/`; a doc page that narrates a
  convention **links the rule `id`** and never re-states the invariant. The rules are the hub, the docs are a
  spoke — authored/maintained by `axis-rules`.
- **From the scaffolds.** Code in a doc is never hand-written: a real, compiled, tested `#region scaffold:{id}`
  in the sample solution is injected into the page's `<!-- scaffold:{id} -->` block by
  `node rules/tooling/sync-scaffolds.mjs` (`--check` reports drift). If a snippet has no scaffold, add the
  scaffold first — do not paste code into prose.
- **From the OpenAPI reference.** The always-current HTTP contract is the rendered docs UI fed by the `///`
  XML docs; the README and any deep-dive **link** it rather than tabulating routes by hand.

## The docs layout

- **`docs/en-us/`** — the canonical English documentation, mirroring the five top-level families
  (`0-Foundations`, `1-Observability`, `2-ApplicationFlow`, `3-Infra`, `4-Edge`); this is also the tree
  `sync-scaffolds` writes into.
- **`docs/pt-br/`** — the PT-BR projection of the same tree (translation, not a divergent source).
- The **root README** stays lean and links into `docs/en-us/**` and the docs UI; a `docs/` deep-dive exists
  only for a walk-through that cannot live in an API reference, and it links out instead of restating.

## Checklist

- [ ] Nothing restated that already has a single source (rule `id`, OpenAPI, ADR) — link it instead.
- [ ] Every code snippet comes from a `#region scaffold:{id}` via `sync-scaffolds` (`--check` clean), not hand-typed.
- [ ] API prose lives as `///` on the driving surface (feeds OpenAPI), not as a separate HTTP page.
- [ ] README limited to the allowed blocks; layout block updated in the same PR as any topology change.
- [ ] English page under `docs/en-us/**`; PT-BR mirror kept in sync under `docs/pt-br/**` where it exists.
- [ ] The doc ships in the SAME pull request as the behavior it documents.

## See also

- `axis-rules` — the canonical rule base every doc derives from; docs link the rule `id`, never restate the invariant.
- `axis-review` — the pre-commit gate that audits the diff; a doc out of sync with the code it maps is a finding.
- `axis-dotnet-architect` — the backend hub that routes here when a change needs its human documentation.
- Sibling single sources this method only points AT, never owns: the **OpenAPI/reference UI** (→ `axis-webapi-controllers`)
  and the **ADRs** (kept outside this repo — referenced as the decision source, not gated in-repo here).
