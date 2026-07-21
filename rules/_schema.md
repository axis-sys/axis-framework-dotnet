# Rule file — schema

This document is the **single source of truth** for the shape of every rule file under `rules/`.
One rule per `.yaml` file. **File name = rule `id`.** English only.

A rule is an atomic, checkable statement of how something must (or should) be done on Axis. It is the
canonical source; skills, hooks, scaffolds and docs **derive from it** (they link to its `id`, they never
restate its invariants). See [rules-guide.md](rules-guide.md) for the model and authoring workflow.

---

## Frontmatter

```yaml
id: <scope>-<kebab-slug>           # stable, globally unique, == file name without extension
title: <short imperative>          # e.g. "A valueless Then preserves the original value"
tier: framework | convention       # framework = intrinsic package contract; convention = how to build on Axis
family: 0-foundations | 1-observability | 2-application-flow | 3-infra | 4-edge | null   # tier=framework only
package: AxisResult | AxisSaga | AxisBus | AxisCache | AxisStorage | AxisEmail | AxisMediator | AxisValidator | AxisRepository | AxisLogger | AxisTelemetry | AxisTypes | AxisDependencyInjections | AxisResult.HttpResponse | null   # tier=framework only
concern: package-mechanics | architecture | domain | persistence | edge | testing | style | process
layer: kernel | domain | application | contracts | driven | driving | tests | build | any   # hexagonal facet
lens: [security, observability, performance, concurrency]   # optional cross-cutting tags — a lens, not a folder
severity: must | should | may      # must = inviolable; should = strong convention; may = recommendation
status: canonical | proposed | stale | superseded
description: >
  What the rule is and why it exists (1-4 sentences).
invariants:
  - Precise, checkable statement. One assertion each.
reference:                          # provenance of the live example (may be none)
  kind: reference-app | framework-source | framework-test | framework-sample | none-yet
  at:                               # checkable anchors; the linter verifies each path exists
    - "path:line — note"
examples:
  correct:
    - "path:line — note"            # illustrative; real code that follows the rule
  incorrect: []                     # real violations, if any (kept even after a fix is planned)
exceptions:
  - Explicit allowed deviations (or empty list).
derives_to:                         # inverted links: rule -> its projections. This is what kills drift.
  skills:    []                     # skill ids that TEACH this rule (e.g. axis-result)
  hooks:     []                     # hook/CI identifiers that ENFORCE it
  scaffolds: []                     # scaffold file paths (relative to rules/) that MATERIALIZE it
  docs:      []                     # framework doc pages (re)generated/synced from this rule
supersedes: <id>                    # optional
superseded_by: <id>                 # optional (set when status: superseded)
ledger:                             # review state — drives incremental, per-cluster review sessions
  reviewed_against: <framework commit sha or version>   # what truth this was last checked against
  reviewed_on: <YYYY-MM-DD>
notes: >
  Optional: nuance, divergence between surfaces, open questions.
```

---

## Field rules

- **`id`** — `<scope>-<kebab-slug>`, unique across the whole tree, equal to the file name stem.
  - `tier: framework` → `<scope>` is the package short name: `result-`, `saga-`, `bus-`, `cache-`,
    `storage-`, `email-`, `mediator-`, `validator-`, `repository-`, `logger-`, `telemetry-`, `types-`,
    `httpresponse-`.
  - `tier: convention` → `<scope>` is the `concern`: `architecture-`, `domain-`, `persistence-`,
    `edge-`, `testing-`, `style-`, `process-`.
- **`tier` / `family` / `package`** — a `framework` rule sets `family` + `package` and lives at
  `framework/<family>/<package-kebab>/<id>.yaml`. A `convention` rule sets `family: null` +
  `package: null` and lives at `conventions/<concern>/<id>.yaml`.
- **`concern`** — the folder for convention rules; for framework rules it is almost always
  `package-mechanics`. It is a location (one value, maps to a folder).
- **`layer` / `lens`** — facets, not folders. `layer` is where in the hexagon the rule bites; `lens` is a
  cross-cutting review angle (a rule can carry several). Query across these; never foldered by them.
- **`reference`** — the provenance of the illustrative code. `kind: none-yet` is legitimate: a rule is
  normative even with no live example (e.g. rich-domain N2 patterns the reference app does not exercise).
  Every path in `reference.at` and `examples.*` is verified to exist by `lint-rules`.
- **`derives_to`** — the inverted link set. A rule declares which skills teach it, which hooks enforce it,
  which scaffolds materialize it, which doc pages are generated from it. Consumers point back by `id`;
  the rule is the origin. This is the mechanism that makes an incongruity a single-point fix.
- **`ledger`** — `reviewed_against` records the framework commit/version this rule was last verified
  against; `check-freshness` flags a rule as needing a session when the framework has moved past it.

---

## Status semantics

| status       | meaning                                                                    |
|--------------|----------------------------------------------------------------------------|
| `canonical`  | verified against the current source of truth; safe to enforce as-is        |
| `proposed`   | authored but not yet ratified against source/tests                         |
| `stale`      | source of truth moved; the rule needs re-review (surfaced by the ledger)   |
| `superseded` | replaced by another rule; keep for history, set `superseded_by`            |

Unlike the reference app's rulebook, there is no `violated-in-places` status: a rule describes the
framework's intended design, not a snapshot of one app's compliance. Real violations found in the
reference app live in `examples.incorrect` on the relevant rule.

---

## Minimal example (framework tier)

```yaml
id: result-then-value-preservation
title: A valueless Then preserves the original value
tier: framework
family: 0-foundations
package: AxisResult
concern: package-mechanics
layer: any
severity: must
status: proposed
description: >
  Then has four forms. The valueless form runs a side-effecting step for its success/failure outcome
  but PASSES THROUGH the original value, so a later step still sees it.
invariants:
  - A Then whose delegate returns AxisResult (not AxisResult<T>) preserves the upstream value.
reference:
  kind: framework-test
  at:
    - "src/0-Foundations/AxisResult/AxisUnitTests/ThenTests.cs:1 — mirrored across AxisResult/Task/ValueTask"
examples:
  correct: []
  incorrect: []
exceptions: []
derives_to:
  skills: [axis-result]
  hooks: []
  scaffolds: []
  docs: ["docs/en-us/0-Foundations/AxisResult/then.md"]
ledger:
  reviewed_against: TBD
  reviewed_on: TBD
notes: >
  The most-missed ROP semantic; the reference app documents it in rop-then-forms-value-preservation.
```
