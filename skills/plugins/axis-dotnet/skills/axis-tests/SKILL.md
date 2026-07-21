---
name: axis-tests
description: >
  The Axis testing hub — the four-project pyramid (architecture, unit, integration, E2E) and the conventions
  that apply to EVERY layer: where a new test belongs, method naming, Arrange-Act-Assert, and the allowed
  packages. Use when deciding which project a test goes in, or writing any test regardless of layer. This
  skill is a MAP: each row points to the canonical rule in `rules/conventions/testing/`, and it routes to the
  per-layer skills (axis-unit-tests, axis-integration-tests, axis-e2e-tests) for the layer-specific "how".
---

# Axis tests — the pyramid hub (start here, then pick a layer)

The Axis suite is **four projects, each with one job**. New tests go into the **lowest** project that can
prove the behavior; anything a cheaper layer can assert does not belong higher up.

| Project | Job | Layer skill |
|---|---|---|
| Architecture tests | Executable dependency invariants, no runtime | (NetArchTest — see architecture rules) |
| Unit tests | Handlers, validators, domain rules — no I/O | `axis-unit-tests` |
| Integration tests | Repositories, sagas, journeys vs real Testcontainers — no HTTP | `axis-integration-tests` |
| End-to-end tests | Full HTTP pipeline via `WebApplicationFactory<Program>` — auth + controllers | `axis-e2e-tests` |

This skill **does not restate** the invariants — it **routes**. Each row points to the canonical rule (English)
under `rules/conventions/testing/`; open only the one the context requires.

## Pyramid-wide conventions (every layer) ⭐

| Context / what you were about to write | Rule |
|---|---|
| Which of the four projects does this test belong in? | [testing-pyramid-four-projects](../../rules/conventions/testing/testing-pyramid-four-projects.yaml) |
| Test method name (`{WhatItDoes}When{Condition}Async`, PascalCase) | [testing-test-method-naming](../../rules/conventions/testing/testing-test-method-naming.yaml) |
| Arrange/Act/Assert structure separated by blank lines | [testing-aaa-structure](../../rules/conventions/testing/testing-aaa-structure.yaml) |
| Allowed packages (xUnit v3, Moq, Testcontainers, NetArchTest; never FluentAssertions) | [testing-allowed-packages](../../rules/conventions/testing/testing-allowed-packages.yaml) |
| Seed domain data when the Entity is internal (`Fake{Entity}` implements `I{Entity}EntityProperties`) — serves unit and integration | [testing-fake-entity-property-records](../../rules/conventions/testing/testing-fake-entity-property-records.yaml) |

## Then pick the layer

- **axis-unit-tests** — the Core through the Facade over a `ServiceProvider`, mocking only the driven ports.
- **axis-integration-tests** — repositories/UoW/sagas/journeys against a real Testcontainers database, no HTTP.
- **axis-e2e-tests** — the full HTTP journey through the host, auth and controllers included.

## See also

- `axis-unit-tests`, `axis-integration-tests`, `axis-e2e-tests` — the three runtime layers this hub routes to.
- `axis-review` — the review lens that checks the pyramid and the coverage gates.
- `axis-rules` — how these rules are authored and maintained.
