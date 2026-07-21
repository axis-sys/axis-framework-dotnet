---
name: axis-dotnet-architect
description: >
  Entry hub for backend C# on the Axis pattern (Hexagonal + DDD + CQRS + Vertical Slices). START HERE for
  any backend task — plan, implement, review — then load the ONE sub-skill the task calls for. This skill is
  a MAP + ROUTER: it links the cross-cutting unbreakable rules and the swappable-infra-port concept, and
  routes intent → sub-skill. It does NOT restate what the sub-skills own — each row points to a rule `id` or
  a sub-skill; open only the one the context asks for. BC boundaries / where elements live live in
  `axis-systems-architect`.
---

# Axis .NET Architect — the router for backend C#

Every backend feature in Axis is a **vertical slice** through a **hexagonal** boundary: a CQRS use case
(Command/Query → Response → Handler → Validator) exposed by a **Facade**, driving **domain** ports whose
adapters (repository, bus, cache, storage, e-mail…) are chosen only at the composition root. This hub does
**not** teach any of that in depth — it **routes** you to the sub-skill that owns it and reminds you of the
few rules that hold across all of them.

Read this page, pick the row that matches your intent, load that sub-skill, and work from there.

## Unbreakable rules (cross-cutting — links, not prose)

These hold in every slice; the sub-skills assume them. Open the rule only when your diff touches it.

### The railway — everything returns `AxisResult`, nothing throws on the happy path

| The temptation you must not follow | Rule |
|---|---|
| `throw` to signal a failure — return a typed `AxisError` on the failure rail instead | [result-no-throw](../../rules/framework/0-foundations/axis-result/result-no-throw.yaml) |
| `if (result.IsSuccess) … else …` to branch flow — compose with `Then`/`Map`/`Ensure` | [result-no-if-else-flow](../../rules/framework/0-foundations/axis-result/result-no-if-else-flow.yaml) |
| Read `.Value` without proof of success — go through `Match`/composition | [result-value-access-safety](../../rules/framework/0-foundations/axis-result/result-value-access-safety.yaml) |
| `try/catch` on the track — catch only at the true exception boundary via `Try`/`TryAsync` | [result-try-boundary](../../rules/framework/0-foundations/axis-result/result-try-boundary.yaml) |

### Slice shape & layout

| Context | Rule |
|---|---|
| Handler body: VO casts at the top, then a single railway chain — no logic outside it | [architecture-handler-shape](../../rules/conventions/architecture/architecture-handler-shape.yaml) |
| Infrastructure is a neutral `AxisResult` port; the adapter owns every `try/catch`; swap by DI only | [architecture-swappable-infra-ports](../../rules/conventions/architecture/architecture-swappable-infra-ports.yaml) |
| One subfolder per feature: `{BC}/{Aggregate}/{Feature}` mirrored across every layer | [architecture-one-folder-per-feature](../../rules/conventions/architecture/architecture-one-folder-per-feature.yaml) |

### Style & process

| Context | Rule |
|---|---|
| `public` only when crossing a project boundary (interface or dumb record); implementation `internal sealed` | [style-access-modifiers](../../rules/conventions/style/style-access-modifiers.yaml) |
| `///` only where it becomes OpenAPI (Contracts / endpoints); no noisy `//` in internal code | [process-xml-doc-policy](../../rules/conventions/process/process-xml-doc-policy.yaml) |

## The swappable infra port

The one shape every non-repository infra concern shares: a small `IAxis*` interface that sees only kernel
types and its own DTOs, returns `AxisResult` and **never throws**; the adapter catches every SDK exception
and translates it to an `AxisError`; the provider is picked once at the composition root via an `AddAxis…`
extension, so swapping it (in-memory → Redis, R2 → another blob store) is a DI change, never a call-site
change — [architecture-swappable-infra-ports](../../rules/conventions/architecture/architecture-swappable-infra-ports.yaml).

## Routing — when the task is X → load skill Y

Route by **intent**; each sub-skill owns its own rules and scaffolds — don't restate them here.

### Plan & decide

| When the task is… | Load |
|---|---|
| Plan before coding: decide **where** elements live, BC boundaries, subdomain taxonomy, the cross-BC channel (Facade vs bus vs saga) | `axis-systems-architect` |

### Domain & use case

| When the task is… | Load |
|---|---|
| Model an aggregate's domain core — entities, factory, Reader/Writer ports, value objects, N0/N1/N2 levels | `axis-domain-modeling` |
| Add or change a CQRS use case — Command/Query → Response → Handler → Validator — and the **Facade** that exposes it | `axis-use-case-cqrs` |
| Compose values on the railway — `Then`/`Map`/`Ensure`, typed errors, the `Try` boundary | `axis-result` |
| Consume the ambient execution context / CQRS dispatch — identity, tracing, ambient `CancellationToken`, pipeline behaviors | `axis-mediator` |
| Validate the input of a Command/Query/message declaratively | `axis-validator` |

### Persistence

| When the task is… | Load |
|---|---|
| Implement or change a repository against Postgres/MySQL — execution layer, `DbEntity` mapping, keyed multi-DB DI | `axis-repository` |
| Evolve schema/DDL — idempotent migrations, `{Entities}Table`/`{BC}DbInit`, the runner | `axis-migrations` |

### Edge

| When the task is… | Load |
|---|---|
| Expose a use case over HTTP — controller in the host (one folder per BC), render `AxisResult` at the edge | `axis-webapi-controllers` |

### Tests (hub + three layers)

| When the task is… | Load |
|---|---|
| Decide **which** test to write; the cross-cutting test rules and the **E2E gate** (new/changed controller ⇒ E2E) | `axis-tests` (hub) |
| Unit-test a handler / validator / domain rule with mocks, driven through the Facade | `axis-unit-tests` |
| Integration-test a repository against a real Postgres (Testcontainers) | `axis-integration-tests` |
| E2E journey through the host — happy path + 401 + 403 | `axis-e2e-tests` |

### Infra ports (swappable — same port shape above)

| When the task is… | Load |
|---|---|
| Publish/consume async domain events between BCs (fire-and-topology) | `axis-bus` |
| Cache hot read data — cache-aside, invalidate after mutation | `axis-cache` |
| Store and serve blobs (upload/download/presigned URL) | `axis-storage` |
| Send e-mail | `axis-email` |

### Cross-cutting & multi-BC

| When the task is… | Load |
|---|---|
| Emit metrics / traces / spans | `axis-telemetry` |
| Structured logging | `axis-logger` |
| Orchestrate a process across 2+ BCs with rollback/compensation | `axis-saga` |

### Review & docs

| When the task is… | Load |
|---|---|
| Audit the diff against the rules before push | `axis-review` |
| Write or maintain technical docs — README, runbook, API guide (link, don't duplicate) | `axis-docs` |

## See also

- `axis-systems-architect` — BC boundaries, subdomain taxonomy and the cross-BC channel decision (the WHERE).
- `axis-result` — the railway that runs through every row above (handlers, ports, facades, repositories, the edge).
- `axis-rules` — how the linked rules are authored and maintained (the extraction method from code).
