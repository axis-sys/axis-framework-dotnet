---
name: axis-integration-tests
description: >
  Write integration tests the Axis way (C# xUnit v3 + Testcontainers) — repositories, unit-of-work, sagas and
  multi-step journeys driven against a REAL database, with no HTTP. Use when the behavior under test is
  persistence or a cross-step flow: stand up a per-collection Testcontainers fixture, compose a production-like
  DI container, and drive facades/ports with a fresh scope per step. This skill is a MAP: each row points to
  the canonical rule in `rules/conventions/testing/` — open only the one the context asks for. It does NOT
  cover unit (Core through the Facade, no I/O → axis-unit-tests) nor E2E (HTTP journey through the host →
  axis-e2e-tests); for the whole pyramid, see axis-tests.
---

# Axis integration tests — rule map (real database, no HTTP)

The Axis integration test proves the **adapters and the data** — repositories, unit-of-work, sagas and
journeys — against a **real Testcontainers database**, without the HTTP pipeline. It is where dialect-specific
and concurrency behavior is pinned, because those cannot be faked.

This skill **does not restate** the invariants — it **routes**. Each row points to the canonical rule (English)
under `rules/conventions/testing/`; open only the one the context requires.

## Start here — fixture, container and DI ⭐

| Context / what you were about to write | Rule |
|---|---|
| Share the container: one sealed `{X}Fixture : IAsyncLifetime` per xUnit collection, migrations run once in `InitializeAsync`, classes join via `[Collection(...)]`; isolate by unique keys, never re-migrate/truncate | [testing-integration-fixture-per-collection](../../rules/conventions/testing/testing-integration-fixture-per-collection.yaml) |
| Compose the container via a shared helper mirroring production; always register `IAxisTelemetry` (null) + `TimeProvider.System`; repository-only containers stub the ambient mediator | [testing-integration-di-provider](../../rules/conventions/testing/testing-integration-di-provider.yaml) |
| Multi-step journey (no HTTP): a fresh `IServiceScope` per step so each gets its own UoW/connection and proves the commit; business steps via the facade, deep checks via reader ports in a final scope | [testing-journey-scope-per-step](../../rules/conventions/testing/testing-journey-scope-per-step.yaml) |
| Seed domain data when the Entity is internal: positional record `Fake{Entity}(...) : I{Entity}EntityProperties` fed through write ports | [testing-fake-entity-property-records](../../rules/conventions/testing/testing-fake-entity-property-records.yaml) |

## Multi-provider (conditional — only when the app runs on more than one database)

| Context / what you were about to write | Rule |
|---|---|
| Dialect-agnostic repository behavior (shared SQL seams, seeded lookups, JSON round-trips, counts, cascades): one `[Theory]` body with an `[InlineData]` per provider, not two copies | [testing-dual-db-repository-parity](../../rules/conventions/testing/testing-dual-db-repository-parity.yaml) |
| A read-then-act invariant hammered with `Task.WhenAll`: prove it on every engine, because read-committed vs snapshot/repeatable-read defaults diverge and the snapshot leaks stale reads | [testing-concurrency-invariants-both-dbs](../../rules/conventions/testing/testing-concurrency-invariants-both-dbs.yaml) |

## The non-obvious "how"

- **The Axis behavior pipeline throws at runtime without `IAxisTelemetry` and `TimeProvider`** — any container
  that drives the mediator must register the null telemetry instance and `TimeProvider.System`. A
  repository-only container stubs the ambient mediator instead of booting the real one.
- **Migrate once, isolate by keys.** Migrations run a single time in the fixture's `InitializeAsync` (a
  `ValueTask` in xUnit v3); tests never re-migrate or truncate between cases — they use unique keys so the
  shared container stays cheap and parallel-safe.
- **A scope per step is the point, not ceremony:** reusing one long-lived scope would ride a single
  transaction and hide whether the data actually committed between steps.

## Cross-cutting (shared with the other layers)

Method naming, AAA, allowed packages and where each test lives are pyramid-wide — see **axis-tests**.

## See also

- `axis-tests` — the four-project pyramid and the conventions shared by every layer.
- `axis-unit-tests` — the Core-through-the-Facade layer below this one (no I/O).
- `axis-e2e-tests` — the HTTP journey through the host above this one.
- `axis-rules` — how these rules are authored and maintained.
