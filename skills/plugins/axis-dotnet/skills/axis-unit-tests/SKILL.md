---
name: axis-unit-tests
description: >
  Write unit tests the Axis way (C# xUnit v3 + Moq) — the use case exercised THROUGH THE FACADE over a
  `ServiceProvider` with the real Core, mocking ONLY the driven ports. Use when testing a handler/use case
  with no I/O (no database, no HTTP): stand up the TestHost, give the ports a permissive default, override
  per scenario and drive the Facade. This skill is a MAP: each row points to the canonical rule in
  `rules/conventions/testing/` — open only the one the context asks for; the compilable "how" lives in the
  `CatalogTestHost` scaffold. It does NOT cover integration (repositories against a real Postgres →
  axis-integration-tests) nor E2E (HTTP journey through the host → axis-e2e-tests); for the whole pyramid, see
  axis-tests.
---

# Axis unit tests — rule map (unit test through the Facade)

The Axis unit test exercises the **Core** (domain + application) by standing up a `ServiceProvider` with the
real wiring and driving the use case **through the Facade** — so the mediator pipeline (validation + behaviors)
actually runs. The only test double are the **driven ports**; **driving is never mocked nor unit-tested**.
Adapters get no unit test: their coverage is integration (Testcontainers).

This skill **does not restate** the invariants — it **routes**. Each row points to the canonical rule (English)
under `rules/conventions/testing/`; open only the one the context requires. The **compilable, green** example
lives in the scaffold — read it before writing.

## Start here — stand up the test ⭐

| Context / what you were about to write | Rule |
|---|---|
| Stand up the TestHost (ServiceProvider + real Core), drive through the Facade, mock only the driven ports | [testing-unit-serviceprovider-mocks](../../rules/conventions/testing/testing-unit-serviceprovider-mocks.yaml) |
| Where each test lives (unit only in the Core; adapters = integration, never unit) | [testing-pyramid-four-projects](../../rules/conventions/testing/testing-pyramid-four-projects.yaml) |
| Test method name (`{WhatItDoes}When{Condition}Async`, PascalCase) | [testing-test-method-naming](../../rules/conventions/testing/testing-test-method-naming.yaml) |
| Arrange/Act/Assert structure | [testing-aaa-structure](../../rules/conventions/testing/testing-aaa-structure.yaml) |
| Allowed packages (Moq is the only mock framework; never FluentAssertions) | [testing-allowed-packages](../../rules/conventions/testing/testing-allowed-packages.yaml) |
| Entity test data when the Entity is internal (`Fake{Entity}` implements the property interface) | [testing-fake-entity-property-records](../../rules/conventions/testing/testing-fake-entity-property-records.yaml) |

## The TestHost recipe (the non-obvious "how")

The canonical example, compiling and tested, is
[`src/scaffolds/Tests/Scaffolds.ECommerce.UnitTests/CatalogTestHost.cs`](../../scaffolds/Tests/Scaffolds.ECommerce.UnitTests/CatalogTestHost.cs)
(+ the `*HandlerTests.cs` that inherit it). Open it. The points that are **not obvious** and you should not re-derive:

- **The base registers ALL driven ports with a permissive default.** Without it, DI cannot provision the
  application pipeline (the point of the test). The test **overrides only the port it exercises** and calls
  `Build()` — which builds the provider **after** the override.
- **`AddLogging()` is MANDATORY.** The `AxisMediatorHandler` resolves `ILogger<>` on EVERY dispatch; without it, it throws.
- **Minimal wiring:** `AddLogging()` · `AddCqrsMediator(coreAssembly)` (scans the handlers) ·
  `AddAxisMediator()` (mediator + accessors) · `AddSingleton(drivenPortMock.Object)` · `AddScoped<{BC}Facade>()`.
- **Mediator and Facade are Scoped** → resolve the Facade inside a `CreateScope()`.
- **Identity is AMBIENT, not a parameter:** set via `IAxisMediatorContextAccessor.AxisEntityId` (default =
  authenticated; one test sets `Identity = null` to exercise the anonymous/`Unauthorized` path).
- **Side effects** are asserted with `Mock.Verify(...)`; assertions use xUnit's `Assert` (never FluentAssertions).

## Other layers (routing)

The unit test is the base of the pyramid. For the layers above, use the sibling skills — each maps its own rules:

- **axis-integration-tests** — repositories, UoW, sagas and journeys against a real Testcontainers database (no HTTP), plus dual-DB parity and concurrency invariants.
- **axis-e2e-tests** — the full HTTP journey through the host (auth + controllers), and the controller E2E gate.
- **axis-tests** — the four-project pyramid and the conventions shared by every layer (naming, AAA, packages).

## See also

- `axis-use-case-cqrs` — the use case + the Facade the test drives.
- `axis-mediator` — the dispatch and the ambient context (`IAxisMediatorContextAccessor`) the TestHost sets.
- `axis-result` — the monad the handlers return and the assertions inspect (`IsSuccess`/`Errors`/`Match`).
- `axis-rules` — how these rules are authored and maintained.
