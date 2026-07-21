# Axis Rules Guide

This document is the **single source of truth** for how Axis rules are written and organized. It is to
`rules/` what [`docs/axis-documentation.md`](../docs/axis-documentation.md) is to `docs/`: the governing
pattern every rule cluster follows.

A **rule** is an atomic, checkable statement of how something must (or should) be done on Axis. Rules are
canonical. Skills, hooks, scaffolds and docs **derive from** them — they link to a rule's `id`, they never
restate its invariants. The field-level shape lives in [`_schema.md`](_schema.md); this guide is the model,
the taxonomy and the workflow.

> **Golden reference:** `framework/0-foundations/axis-result/` is the living template. When in doubt about
> how to write or split a rule, open that cluster and match it.

---

## Why this exists

Knowledge about "how to build correctly on Axis" used to live in four places at once — framework docs,
skill prose (`## Unbreakable rules`), the reference app's rulebook, and empirical learnings — so every
change had to be made in N places and nothing stayed in sync. Rules fix that by being the **one origin**:

- An incongruity is a **single-point fix** (edit the rule; its projections regenerate/point back).
- Review is **tractable and incremental** (open one cluster, check its ledger, run a session).
- A rule can be **normative without a live example** (`reference.kind: none-yet`), so patterns the
  reference app does not exercise — e.g. rich-domain N2 — are still first-class.

---

## Hub-and-spoke: the rule is the hub

```
rules/  ← SOURCE OF TRUTH (atomic rule files, one per .yaml)
   ├──▶ skills/     teach & route — LINK rule ids (no restated invariants)
   ├──▶ hooks / CI  enforce deterministically — reference rule ids
   ├──▶ src/scaffolds/  materialize a rule as a code template
   └──▶ docs/       (re)generated / synced from the rule's invariants + examples
```

**Knowledge lifecycle** (the derivation, made explicit):

```
empirical learning (produto/learnings)  →  normative rule (rules/)  →   · skill teaches
                                                                        · hook/CI enforces
                                                                        · scaffold materializes
                                                                        · docs narrate
                                                                        · reference app / test exemplifies }
```

Every spoke points back by `id` through the rule's `derives_to`. Adding a projection means editing the
rule's `derives_to`, not duplicating its content.

---

## Two tiers

Not every rule belongs to a package: most "how to build an app on Axis" rules are cross-cutting. So rules
split into two tiers, each with its own spine.

| Tier | What it governs | Source of truth | Organized by |
|---|---|---|---|
| **framework** | the intrinsic contract of a primitive (how `AxisResult` composes, the saga DSL, the VO generator) | framework `docs/` + `*.UnitTests` (executable spec) | **family → package** (the 5 families) |
| **convention** | how to **build an application** on Axis (hexagonal, CQRS, testing, style, domain) | the framework's intended design; the reference app is a primary but **non-exhaustive** example | **concern** |

---

## Folder layout

```
rules/
├─ rules-guide.md          # this file
├─ _schema.md              # rule frontmatter schema
├─ README.md               # GENERATED master index (by family · by concern · by status) — never hand-edit
├─ tooling/                # lint-rules.mjs · gen-index.mjs · check-freshness.mjs · check-enforcement.mjs · sync-scaffolds.mjs
│
├─ framework/              # TIER 1 — mirrors src/ and docs/ families
│  ├─ 0-foundations/       { axis-result/ · axis-types/ · axis-mediator-contracts/ · axis-dependency-injections/ }
│  ├─ 1-observability/     { axis-logger/ · axis-telemetry/ }
│  ├─ 2-application-flow/  { axis-mediator/ · axis-saga/ · axis-validator/ · axis-bus/ }
│  ├─ 3-infra/             { axis-cache/ · axis-email/ · axis-migrations/ · axis-repository/ · axis-storage/ }
│  └─ 4-edge/              { axis-result-httpresponse/ }
│
└─ conventions/            # TIER 2 — cross-cutting, one folder per concern
   ├─ architecture/  domain/  persistence/  edge/  testing/  style/  process/
```

A `framework` rule lives at `framework/<family>/<package-kebab>/<id>.yaml`; a `convention` rule at
`conventions/<concern>/<id>.yaml`. The folder is the rule's `concern`/location; `layer` and `lens` are
facets you query across, never folders.

---

## The Axis package families (taxonomy)

Inherited verbatim from the framework so `rules/`, `src/` and `docs/` share one spine. Confirm a package's
family by reading its code.

| Family | Purpose | Packages |
|---|---|---|
| **0 · Foundations** | Typed building blocks used by everything else | `AxisResult`, `AxisTypes`, `AxisMediator.Contracts`, `AxisDependencyInjections` |
| **1 · Observability** | Cross-cutting concerns operators watch | `AxisLogger`, `AxisTelemetry` |
| **2 · Application & Flow** | Use-case orchestration, processes, rules | `AxisMediator`, `AxisSaga`, `AxisValidator`, `AxisBus` |
| **3 · Infrastructure & Integration** | Adapters for external resources | `AxisRepository`, `AxisCache`, `AxisStorage`, `AxisEmail` |
| **4 · Edge** | The HTTP boundary | `AxisResult.HttpResponse` |

---

## Authoring workflow (per cluster)

1. **Read the source of truth.** For a framework cluster: the package `.cs` (public surface) + its
   `*.UnitTests` (the executable spec) + its `docs/` pages. For a convention cluster: the framework's
   intended design + the reference app as an example to cite, not to copy.
2. **Enumerate exhaustively.** A cluster must cover the whole surface it governs — every operator, form and
   overload — not just what the reference app happens to use. Incomplete coverage is the failure mode that
   lets an AI (or a dev) reach for imperative code because it never saw the clean operator.
3. **Add a discovery layer where it applies.** For surfaces with an imperative temptation, write
   `smell → canonical operator` selection rules (e.g. `if/else` on `IsSuccess` → the operator; `try/catch`
   → the boundary primitive; raw `.Value` → `Match`) so routing happens *before* imperative code is written.
4. **Write one rule per invariant.** Small, checkable, English-only. Set `reference` (with real
   `path:line`, or `none-yet`), `derives_to`, and `ledger`.
5. **Wire the spokes.** Point the teaching skill(s) at the rule ids (retire the prose); extract scaffolds
   to files; (re)generate/validate the doc pages.
6. **Validate & stamp.** Run `lint-rules` + `gen-index`; set `status: canonical` and the `ledger` fields.

---

## Quality checklist (run before marking a cluster canonical)

- [ ] Every public operator/form/overload of the governed surface has a rule (checked against source + tests).
- [ ] Where imperative temptation exists, the discovery layer maps each smell to its canonical operator.
- [ ] `id == file name`, unique across the tree; folder matches `tier`/`concern`.
- [ ] Every `reference.at` and `examples.*` path resolves (`lint-rules` green).
- [ ] A `must`-severity rule under `conventions/architecture|edge|testing` carries a concrete anchor —
      `reference.kind` is not `none-yet`, or `examples.correct` is populated. Check `src/scaffolds/` for
      the real project name / folder / shape before writing role-only prose; never guess it.
- [ ] `node skills/scripts/bundle-plugin.mjs` was re-run if the change touched rules, docs or scaffold
      sources (the installable plugin carries a generated copy; CI fails on drift).
- [ ] `derives_to` is filled: the teaching skill links these ids and restates no invariant in prose.
- [ ] Doc pages in `derives_to.docs` regenerate/validate without divergence.
- [ ] `ledger.reviewed_against` = current framework HEAD; `status: canonical`.
- [ ] `gen-index` regenerated; README reflects the cluster.

---

## Incremental review (how sessions get triggered)

The generated `README.md` groups rules by family, by concern and by `status`, and `check-freshness`
compares each rule's `ledger.reviewed_against` to the current framework HEAD. Together they are a dashboard:
anything `stale` or behind HEAD is a candidate cluster, and each cluster is small enough to review in one
focused session. That is what makes "trigger sessions as reviews happen" practical rather than a 111-rule
big-bang.

---

## Tooling

Under `tooling/` (evolved from the skills repo's `scripts/lint-skills.mjs`):

- **`lint-rules.mjs`** — validates each `.yaml` against `_schema.md`: required fields, enum values,
  `id == file name`, uniqueness, folder ↔ `tier`/`concern` agreement, and that every `reference.at` /
  `examples.*` path exists on disk.
- **`gen-index.mjs`** — regenerates `README.md` from frontmatter (by family · by concern · by status).
  The index is generated, never hand-edited — this is what ends count-drift for good.
- **`check-freshness.mjs`** — reports rules whose `ledger.reviewed_against` is behind the current framework
  commit, i.e. the review backlog.
- **`check-enforcement.mjs`** — verifies the rule ↔ analyzer link (ADR-0004): every `derives_to.hooks`
  `AXISNNNN` must have a matching Roslyn diagnostic under `src/**/*.Analyzers`, and every diagnostic must be
  referenced by exactly one rule; reports framework `must` rules with no analyzer yet.
- **`sync-scaffolds.mjs`** — injects real, compiled `#region scaffold:<id>` snippets from the sample solution
  into a doc's `<!-- scaffold:<id> -->` block, so code in docs is never hand-typed; `--check` reports drift
  without writing.

`yaml-lite.mjs` is the strict internal reader for the constrained YAML subset the scripts above use — not a
standalone CLI tool.
