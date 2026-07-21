---
name: axis-e2e-tests
description: >
  Write E2E tests the Axis way (C# xUnit v3) — the full HTTP journey through the host over
  WebApplicationFactory<Program>, exercising auth, controllers and sagas exactly as a client does. Use when a
  controller is new/changed (E2E is the gate) or when a saga must be proven at the HTTP edge. This skill is a
  MAP: each row points to the canonical rule in `rules/conventions/testing/` — open only the one the context
  asks for. It does NOT cover unit (Core through the Facade → axis-unit-tests) nor integration (repositories
  against a real Postgres → axis-integration-tests); for the whole pyramid, see axis-tests.
---

# Axis E2E tests — rule map (HTTP journey through the host)

The Axis E2E test drives the **full HTTP pipeline** — `WebApplicationFactory<Program>`, real auth, real
controllers, real middleware — and asserts what a client observes. It is the top of the pyramid: the most
expensive layer, reserved for the wire contract and the journey, not for logic a lower project can prove.

This skill **does not restate** the invariants — it **routes**. Each row points to the canonical rule (English)
under `rules/conventions/testing/`; open only the one the context requires.

## Start here — the E2E gate and host ⭐

| Context / what you were about to write | Rule |
|---|---|
| New/changed controller → happy path + 401 + (403 + bypass where permissions apply); every controller activates from the production container | [testing-e2e-controller-coverage-gate](../../rules/conventions/testing/testing-e2e-controller-coverage-gate.yaml) |
| Stand up the factory hermetically: provider pinned at **compile time** (`DefaultProvider` const → `[Fact(Skip)]` per coverage class), connection string pointed at the factory's own Testcontainers instance, resumer disabled, boot-config dictionary for pre-Build() settings | [testing-e2e-hermetic-provider-pinning](../../rules/conventions/testing/testing-e2e-hermetic-provider-pinning.yaml) |
| Saga-backed endpoint → POST 202 + saga id, poll status to terminal, assert terminal payload; deserialize into the **production driving-contract records** | [testing-e2e-saga-edge-202-polling](../../rules/conventions/testing/testing-e2e-saga-edge-202-polling.yaml) |
| Configure the test host's logging (or debugging a flaky ObjectDisposedException on TestServer/EventLogInternal) | [testing-e2e-quiet-logging-providers](../../rules/conventions/testing/testing-e2e-quiet-logging-providers.yaml) |

## The non-obvious "how" (traps that only bite E2E)

- **Startup I/O before `builder.Build()` sees only environment + build-time config** — not `UseSetting` or
  in-memory config layered afterward. That is why the DB-backed factory exports the container-owned connection
  string as a **process environment variable inside `InitConnectionAsync`** (reset to null on dispose), and why
  an E2E factory can otherwise silently run against a developer's real database. The provider itself is a
  **compile-time constant**, not an environment lookup. See `testing-e2e-hermetic-provider-pinning`.
- **Clear the default logger providers.** On Windows the host registers the EventLog provider by default; under
  parallel collections, concurrent host disposal races with request logging and throws
  `ObjectDisposedException` (TestServer / EventLogInternal), masking the real result — a **flaky** failure that
  passes in isolation. Every factory (including inline `using var factory = new ...` throwaways, disposed
  mid-test) calls `builder.ConfigureLogging(l => l.ClearProviders())`. See `testing-e2e-quiet-logging-providers`.
- **A DB-less E2E factory is legitimate:** compose the host's in-memory driven adapter (or swap ports for
  fakes in `ConfigureTestServices`). The DB-backed factory instead combines `WebApplicationFactory<Program>`
  with `IAsyncLifetime` and owns its Testcontainers instance (`testing-integration-fixture-per-collection`
  topology).
- **Use-case files hold plain `static` journeys, no `[Fact]`:** a single coverage class holds the shared
  collection (`ICollectionFixture` over the factory) and fans the statics out as one-line `[Fact]`s — one
  factory and store serve the whole parallel suite, and a provider skips as a whole via its coverage class.
- **Tokens come from the real login flow:** the factory swaps the external-provider schemes' `HandlerType`
  for a test handler (`PostConfigure<AuthenticationOptions>`) that reads an `X-Test-User` header, then obtains
  bearers by POSTing the actual token-exchange endpoint, cached per user.
- **Sagas drain inline at the edge:** the resumer is disabled; the endpoint returns 202 and the test polls the
  status endpoint through a bounded `WaitForTerminalStatus`-style helper — never a fixed `Task.Delay`.

## Cross-cutting (shared with the other layers)

Method naming, AAA, allowed packages and where each test lives are pyramid-wide — see **axis-tests**. Fake
entity data (`Fake{Entity}`) and, for saga endpoints, the saga mechanics come from those skills too.

## See also

- `axis-tests` — the four-project pyramid and the conventions shared by every layer.
- `axis-unit-tests` — the Core-through-the-Facade layer at the base of the pyramid.
- `axis-integration-tests` — repositories/journeys against real Testcontainers, one layer down.
- `axis-review` — the review lens that checks the E2E gate is honored.
- `axis-rules` — how these rules are authored and maintained.
