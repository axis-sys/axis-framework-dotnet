---
name: axis-rules
description: >
  Author and maintain the canonical rule base (`rules/`) of the Axis pattern — extracting EXHAUSTIVELY from
  the code every invariant, behavior, overload and error path (nothing left out), one rule per invariant, in
  the schema of `rules/_schema.md`, and wiring them to the projections (skill-map, tested scaffold, doc). Use
  when creating or evolving rules — in the AxisFramework itself (framework rules per family/package) OR in a
  CLIENT repo of Axis (app conventions + reference to the framework rules). Covers: delimiting the surface,
  enumerating the whole public API using the `*.UnitTests` as an executable spec, authoring the rule, the
  smell→operator discovery layer, wiring the spokes and the gates (lint-rules / gen-index / check-freshness /
  sync-scaffolds), plus bootstrapping `rules/` + tooling in a client repo. It does NOT audit the diff
  (→ axis-review) nor re-teach the monad operators (→ axis-result).
---

# Axis Rules — authoring the canonical rule base

A **rule** is an atomic, checkable statement of how something must be done in Axis. The rules are the
**source of truth**; skills, hooks, scaffolds and docs **derive** from them (they link the `id`, never
restate the invariants). This skill is the **method** of authoring: how to extract rules from the code
without leaving anything out, write them in the schema and wire them to the projections.

> **Anchoring.** The SHAPE of the rule lives in `rules/_schema.md`; the MODEL and taxonomy in
> `rules/rules-guide.md`. This skill does NOT re-copy the schema — it teaches the process. Where a
> `{placeholder}` appears, swap it for the real name; `rules/`, `derives_to`, `#region scaffold:` are fixed.

## When to use

- Create a framework package's rule cluster (e.g. AxisSaga, AxisRepository) from the code.
- Author the conventions of an Axis client repo (hexagonal, CQRS, naming, persistence, tests) by extracting
  from the app's own code.
- Evolve/review rules when the API changes (the rule goes `stale` and must be re-extracted).

Not for: auditing a diff against the already-written rules (→ `axis-review`); teaching the AxisResult
operators (→ `axis-result`).

## Two tiers, two sources of truth

| Tier | What it governs | Source of truth | Organized by |
|---|---|---|---|
| **framework** | the intrinsic contract of a primitive | the package's public `.cs` + `*.UnitTests` | family → package (`framework/{family}/{package}/`) |
| **convention** | how to **build an app** on Axis | the intended design; the client repo is a primary non-exhaustive example | concern (`conventions/{concern}/`) |

In **AxisFramework** you author both. In a **client repo** you author only the app's `conventions/` (the
framework tier belongs to AxisFramework — reference it, don't rewrite it).

## The method — EXHAUSTIVE extraction from code (the heart)

The failure mode to eliminate is "the rule came out incomplete because someone read only part of the API".
Therefore:

1. **Delimit the surface.** One package, one layer, one BC, or one concern. One round = one surface.
2. **Enumerate from the SOURCE, not from memory.** List and read:
   - **every public type** (classes, records, enums, interfaces) and **every public member/operator**;
   - **every overload** of each operator, and the **async** (`Task`, `ValueTask`) and **CT-aware**
     variants — mark which ones REALLY exist and which don't;
   - **error paths** and **implicit conversions** (what becomes a failure, what is thrown, what is a sentinel);
   - use the **`*.UnitTests` as an executable spec** — confirm EACH behavior in a test before asserting it
     in an invariant;
   - use the **existing docs** for intent (when to use / when NOT to use).
3. **Produce a CATALOG first** (table of types + operators, each row with a `path:line` anchor), and only
   then author. The catalog is the draft the invariants come out of.
4. **Capture the non-obvious**: gotchas (e.g. "valueless Then PRESERVES the value"; "Try default leaks
   `ex.Message` as the code") and **GAPS** — API that exists but the app doesn't use (a candidate for a
   discovery rule, because the AI/dev "didn't know it existed").
5. **Completeness GATE — cross the catalog against a MECHANICAL inventory.** E.g. list the public members
   by regex (`grep -rhoE 'public (static )?[A-Za-z<>,\[\] ]+ [A-Z][A-Za-z0-9]+\('`) and check that each
   member/behavior produced **≥1 invariant**. Whatever is left with no rule is either explicitly covered by
   another invariant, or a missing rule. **Nothing left out.**

## Authoring the rule (one per invariant)

- **Mirror an adjacent rule**: glob the cluster and copy the structure before writing.
- Fill in the `_schema.md` fields: `id` (= file name), `tier`/`family`/`package` (framework) or
  `concern` (convention), `severity`, `status: proposed`, `description`, **one `invariant` per checkable
  statement**, `reference` (a real `path:line` anchor, or `kind: none-yet` when the pattern has no living
  example yet), `exceptions`, `derives_to`, `ledger`.
- **One invariant = one assertion.** If a bullet has an "and"/"or" with two facts, split it in two.
- Canonical rules are in English (this skill is too; the PT-BR projection lives only in `docs/pt-br/`).

## Discovery layer (smell → operator/pattern)

For surfaces with an imperative temptation, write a **selection** rule: each invariant maps a *smell* to the
canonical operator/pattern (e.g. `if/else` on `IsSuccess` → operator; `try/catch` → boundary; raw `.Value` →
`Match`). This is what makes the AI route BEFORE writing the imperative code. See `result-operator-selection`
as reference.

## Wiring the spokes (the rule is the hub)

**Every new or changed rule updates ALL its projections in the SAME step — never just the `.yaml`.** Edit
`derives_to`, never duplicate the content:

- **skill-map** (`derives_to.skills`): the package's skill becomes a MAP that LINKS the rules' `id`s; drop
  the normative prose. The skill routes; the rule is the norm.
- **scaffold** (`derives_to.scaffolds`): the example is **real, compiled and tested** code in a solution
  (`src/scaffolds/Scaffolds.slnx`), marked by `#region scaffold:{scenario-id}` … `#endregion`. The scaffold name
  is **contextual** (the scenario, e.g. `place-order`), not the rule id — one scenario serves several rules.
  `node rules/tooling/sync-scaffolds.mjs` injects the region into the doc's `<!-- scaffold:{id} -->` block.
- **doc** (`derives_to.docs`): the page that narrates the invariant (the code comes from the scaffold, not
  hand-written).
- **hook/analyzer** (`derives_to.hooks`): if the invariant is **mechanically decidable**, write a Roslyn
  analyzer that enforces it and cite the `AXISNNNN` here. Not every rule has an analyzer — only the decidable
  ones (see below).

## Enforcement — deterministic analyzer (AXISNNNN)

A subset of the rules is **mechanically decidable** and becomes a Roslyn analyzer (ADR-0004), packaged
INSIDE the primitive's package (`{Axis*}.Analyzers`), a no-op when the primitive's type is not in the
compilation. Each diagnostic cites the rule slug in the `helpLinkUri`; the rule points back in
`derives_to.hooks: [AXISNNNN]`. `check-enforcement.mjs` guarantees the descriptor ↔ hook bijection.

**Numbering — a range reserved per package** (minimum interval of 100, sized for years of growth):

| Package | Range |
|---|---|
| AxisResult | AXIS0001–AXIS0199 |
| AxisTypes | AXIS0200–AXIS0399 |
| AxisMediator.Contracts | AXIS0400–AXIS0599 |
| Axis.Conventions.Analyzers (convention-tier, opt-in) | AXIS0600–AXIS0799 |
| (next package) | AXIS0800–AXIS0999 |
| … | +200 per package |

Pick the next FREE number within the package's range. Severity: `must`→Warning (upgradable to Error in the
`.editorconfig`), `should`→Info, `may`→Hidden. Each family uses its own `Category` (`Axis.Result`,
`Axis.Types`, …). Before moving on to the next package, **cover all the current package's decidable rules
with an analyzer**.

**Decidable** (worth an analyzer): forbidden access/branch/try-catch; a call that omits a required safety
argument (e.g. `Try` without `errorHandler`); code built unsafely (e.g. `AxisError` with `ex.Message`); a
checkable structural shape (e.g. a `[ValueObject]` struct that is not `readonly`). **Not decidable**:
semantic facts ("valueless Then preserves the value"), intentional use ("recover on purpose"), or a trust
distinction (trusted vs untrusted input) — leave those without an analyzer; the rule + doc + scaffold cover them.

Mirror the reference cluster `{Axis*}.Analyzers` (netstandard2.0, `IsRoslynComponent`, a descriptor with
`helpLinkUri` on the slug, **semantic** detection, an `AnalyzerHarness.RunAsync<T>` harness with cases that
fire AND cases that don't).

## Gates (run before marking `canonical`)

```
node rules/tooling/lint-rules.mjs        # schema, id==file, reference paths exist
node rules/tooling/gen-index.mjs         # regenerates the README (never edit by hand)
node rules/tooling/sync-scaffolds.mjs --check   # the doc == the sample's region
node rules/tooling/check-freshness.mjs   # rules behind the framework HEAD = backlog
node rules/tooling/check-enforcement.mjs # derives_to.hooks (AXISNNNN) ↔ Roslyn descriptor bijection
```
All green → flip `status` to `canonical` and stamp `ledger.reviewed_against` (the commit) and `reviewed_on`.

## In an Axis CLIENT repo (bootstrap)

1. **If there is no `rules/`**: copy `rules/_schema.md`, `rules/rules-guide.md` and `rules/tooling/` from an
   Axis repo; create the tree `rules/conventions/{architecture,domain,persistence,edge,testing,style,process}`.
2. **Do not rewrite the framework rules** — they live in AxisFramework; the package's skill already maps them.
   The client authors the **app conventions**, extracting from its OWN code (that's the `convention` tier).
3. **Examples**: a `scaffolds/` in the client consumes the client's code (same `#region scaffold:` +
   `sync-scaffolds` pattern). An invariant with no example yet in the app uses `reference.kind: none-yet`.
4. Run the same gates; `check-freshness` becomes the incremental per-cluster review board.

## Completeness checklist

- [ ] Surface delimited; catalog crossed against the mechanical inventory of public members (nothing left out).
- [ ] One rule per invariant; `lint-rules` green; `id` == file name; folder matches `tier`/`concern`.
- [ ] Each `reference` has a real anchor (or `none-yet`); `derives_to` filled in.
- [ ] Discovery layer where there is an imperative temptation.
- [ ] Real, tested scaffold + doc extracted with no drift; `ledger` stamped; `status: canonical`.
- [ ] Projections updated in the SAME step: skill-map, doc, scaffold and — if decidable — analyzer
      (`AXISNNNN` in the package's range), with `check-enforcement` green.

## See also

- `axis-review` — audits the DIFF against the already-written rules (this skill AUTHORS; that one ENFORCES).
- `axis-result` — the reference cluster (the "living template" of what a finished cluster looks like).
- `axis-tests` — the `*.UnitTests` that serve as an executable spec during extraction, and the scaffold tests.
