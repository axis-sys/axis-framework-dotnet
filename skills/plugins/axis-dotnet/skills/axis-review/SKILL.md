---
name: axis-review
description: >
  Pre-commit/pre-push review gate the Axis way — audits the DIFF (not the whole repo) against the rules in
  `rules/conventions/`. Philosophy: **deterministic first, AI last**. The deterministic gates (build with the
  `AXIS####` analyzers, `lint-rules`, tests) run BEFORE and cheaper; this skill covers only what needs
  judgment or non-Roslyn tooling — topology/boundaries (NetArchTest), forbidden packages (PackageReference
  check), folder/namespace layout and semantic invariants. Use ALWAYS before `git commit`/push. Each finding
  cites the rule `id`; a `critical` severity blocks the commit. It does NOT re-run the analyzers (the build
  already does) nor author rules (→ `axis-rules`).
---

# AxisReview — pre-commit gate (deterministic first, AI last)

Before `git commit`/push, audit the **DIFF** against the conventions. Do not review the whole repo nor what
the machine already catches — focus on what needs judgment.

## Mandatory order

1. **Deterministic gates (run FIRST — fix everything before moving on):**
   ```
   dotnet build {Solution} --nologo   # fails on the AXIS#### analyzers (see below)
   dotnet test  {Solution} --nologo
   node rules/tooling/lint-rules.mjs  # in AxisFramework; in an app, the equivalent gates
   ```
   If any of them fails, fix it **first** — the AI does not review what the analyzer/the build already blocks.

2. **Agentic layer (this skill):** audit the diff against the rules **no analyzer covers** (§ tables).

## What the analyzers ALREADY cover — do NOT re-check by hand

`Axis.Conventions.Analyzers` (opt-in): **AXIS0600** handler `internal sealed` · **AXIS0601** validator
`internal sealed` · **AXIS0602** controller `sealed` · **AXIS0603** test naming PascalCase+`Async`.
Framework (they travel with the packages): **AXIS0001–0007** (ROP: `.Value`, flow-if, try/catch on the track,
`Try` without a handler, exception code, deconstruct-on-rail, forgiven deconstructed value), **AXIS0200**
(VO struct `readonly`), **AXIS0300** (bus: outbox `PublishAsync` after `SaveChangesAsync` in the same member),
**AXIS0400–0403** (mediator: ambient CancellationToken, dispatch only in the Facade, context accessor,
pipeline key). If it showed up in the build, it's already caught — don't repeat it in the review.

## Agentic layer — audit the diff by verification method

### A. Topology & boundaries → NetArchTest (or diff inspection)

Where the app has architecture tests (NetArchTest), they are the **deterministic** enforcement — confirm they
exist and pass for the diff. Where there are none, inspect the diff manually.

| Check in the diff | Rule |
|---|---|
| Dependencies point inward; no Core/Contracts project references an adapter; only the root composes; `Contracts/` is a **sibling** folder of `Core/` (never nested), and facade impls live in `{App}.Adapters.Driving.Facade`, never in the Application | [architecture-hexagonal-topology](../../rules/conventions/architecture/architecture-hexagonal-topology.yaml) |
| `public` only when crossing a project boundary (interface or dumb record); implementation `internal sealed` | [style-access-modifiers](../../rules/conventions/style/style-access-modifiers.yaml) |
| No BC injects another BC's `*WritePort`/repo/service; cross-BC only via Facade, `IAxisBus` or saga | [architecture-cross-bc-communication](../../rules/conventions/architecture/architecture-cross-bc-communication.yaml) |
| Only the composition root picks an adapter via `Add*`/`AddAxis*`; the call-site depends on an abstraction | [architecture-di-registration](../../rules/conventions/architecture/architecture-di-registration.yaml) · [architecture-composition-root](../../rules/conventions/architecture/architecture-composition-root.yaml) |

### B. Forbidden packages → check `PackageReference` (not the code)

| Check in the diff | Rule |
|---|---|
| No new `<PackageReference Include="FluentAssertions" />`; Moq is the only mock framework (`grep -r FluentAssertions **/*.csproj`) | [testing-allowed-packages](../../rules/conventions/testing/testing-allowed-packages.yaml) |

### C. Folder & namespace layout → diff inspection

| Check in the diff | Rule |
|---|---|
| New file in the right folder: `{BC}/{Aggregate}/{Feature}` across all layers; one subfolder per feature | [architecture-one-folder-per-feature](../../rules/conventions/architecture/architecture-one-folder-per-feature.yaml) · [architecture-vertical-slice-layout](../../rules/conventions/architecture/architecture-vertical-slice-layout.yaml) |
| Namespace mirrors the folder path, including the version segment `.v1` | [architecture-contracts-versioned-layout](../../rules/conventions/architecture/architecture-contracts-versioned-layout.yaml) |
| Command/Query/Response in `{App}.Contracts.Driving` (not the Application); primitive contract DTOs | [architecture-contracts-location](../../rules/conventions/architecture/architecture-contracts-location.yaml) · [architecture-contracts-primitive-dtos](../../rules/conventions/architecture/architecture-contracts-primitive-dtos.yaml) |
| `///` only in `Contracts/`/endpoints (what becomes OpenAPI); no noisy `//` in internal code | [process-xml-doc-policy](../../rules/conventions/process/process-xml-doc-policy.yaml) · [process-comment-policy](../../rules/conventions/process/process-comment-policy.yaml) |

### D. Semantic judgment → read the diff against the rule

| Check in the diff | Rule |
|---|---|
| Repository composes over `IAxisDbRepository` + dialect seam (no base inheritance); named params; columns from `{Entities}Columns.All` | [persistence-repository-composition-dialect-seam](../../rules/conventions/persistence/persistence-repository-composition-dialect-seam.yaml) |
| Pure parameterized SQL (no concatenation/interpolation of input into SQL) | [persistence-sql-parameterized-pure](../../rules/conventions/persistence/persistence-sql-parameterized-pure.yaml) |
| Handler: VO casts at the top, then a single railway chain; no logic outside the chain | [architecture-handler-shape](../../rules/conventions/architecture/architecture-handler-shape.yaml) |
| Handler does not re-check auth the driving edge already enforced (reads ambient identity for business use only) | [architecture-handler-no-authorization](../../rules/conventions/architecture/architecture-handler-no-authorization.yaml) |
| Swappable infra port: interface `IAxis*` returns `AxisResult`, `try/catch` only in the adapter | [architecture-swappable-infra-ports](../../rules/conventions/architecture/architecture-swappable-infra-ports.yaml) |
| Cache-aside composed with `AxisResult`; invalidation after mutation | [architecture-cache-aside](../../rules/conventions/architecture/architecture-cache-aside.yaml) |
| Saga: `Define<T>` DSL, framework-hosted resumer by config, reverse compensation | [architecture-saga-definition](../../rules/conventions/architecture/architecture-saga-definition.yaml) · [architecture-saga-stage-handlers](../../rules/conventions/architecture/architecture-saga-stage-handlers.yaml) |
| Controller injects only Facade(s); action ends with `HttpContext.SendAsync` (`AxisResult` rendered at the edge) | [edge-controller-facade-injection](../../rules/conventions/edge/edge-controller-facade-injection.yaml) · [edge-axisresult-render](../../rules/conventions/edge/edge-axisresult-render.yaml) |
| Correct aggregate level (N0/N1/N2); the 1st business rule promotes to N2 in the same PR | [domain-aggregate-levels](../../rules/conventions/domain/domain-aggregate-levels.yaml) |
| Unit test drives through the Facade via `ServiceProvider`; mocks only driven ports | [testing-unit-serviceprovider-mocks](../../rules/conventions/testing/testing-unit-serviceprovider-mocks.yaml) |
| New/changed controller has E2E (happy path + 401 + 403) | [testing-e2e-controller-coverage-gate](../../rules/conventions/testing/testing-e2e-controller-coverage-gate.yaml) |

## Verdict

Emit findings **by severity**, each citing the rule `id` and the `path:line` of the diff:
- **critical** — a `must` rule violated → **blocks the commit**; fix before proceeding.
- **warning** — a `should` violated or a strong doubt → record it; the author decides.
- **info** — `may`/style.

If nothing survives the review, say "0 findings" and clear the commit.

## Enforcement roadmap (to make it more deterministic over time)

The goal is to migrate items from this skill to deterministic gates, reducing judgment:
- **§A topology/boundaries** → **NetArchTest** in the app's test project (asserts dependency direction and
  BC boundary at build time). Where it exists, § A becomes deterministic and the AI only confirms it passed.
- **§B forbidden packages** → an **MSBuild target** that fails the build if a banned package (FluentAssertions)
  is referenced — or a `rules/tooling` that sweeps the `.csproj` files.
- **§C/§D** stay agentic (they depend on layout/intent that a single-compilation analyzer cannot decide),
  except cases that become a new analyzer in `Axis.Conventions.Analyzers` (see `axis-rules` § Enforcement).

## See also

- `axis-rules` — authors/maintains the rules and decides what is decidable (becomes an `AXIS####` analyzer) vs.
  what stays for this review.
- `Axis.Conventions.Analyzers` (`src/Conventions/`) — the analyzers that cover the decidable subset.
- Broader pre-push boundary gates (out of scope for code convention): a new BC without LikeC4, a structural
  decision without an ADR, a diagram out of sync — audited in the same pass when the app adopts them.
