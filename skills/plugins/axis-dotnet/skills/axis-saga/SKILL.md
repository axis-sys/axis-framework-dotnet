---
name: axis-saga
description: >
  Orchestrate a process crossing 2+ BCs with rollback/compensation on Axis — the DSL
  `AxisSagaDefinitions.Define<TPayload>(name, Configure)`, idempotent stage handlers
  `IAxisSagaStageHandler<TPayload>`, `IAxisSagaMediator.StartAsync`, the framework-hosted resumer, and the
  `AddAxisSagaPostgres`/`AddAxisSagaMySql` wiring. Use when a use case coordinates multiple BCs and one ACID
  transaction is impossible (create A -> create B; undo A if B fails). This skill is a MAP: each row points to
  the canonical rule in `rules/` — open only the one the context asks for. It does NOT restate invariants nor
  carry code. It does NOT cover the return monad (→ axis-result), the ambient context/dispatch (→ axis-mediator),
  fire-and-forget events with no rollback (→ axis-bus), the ports the stages call (→ axis-domain-modeling), nor
  the 202 + polling edge (→ axis-webapi-controllers).
---

# AxisSaga — rule map (multi-BC orchestration with compensation)

A **saga** is a small state machine in data form: forward stages (the business progression), error stages
(compensation), and where to route on failure. The dialect-agnostic
runtime (engine, mediator, resumer, janitor) persists that machine to the shared `AXIS_SAGA` schema; a storage
adapter (`AxisSaga.Postgres`, `AxisSaga.MySql`) supplies only the SQL. The package is 2-application-flow.

This skill **does not restate** the invariants nor carry code — it **routes**. Each map row points to the
canonical rule (in English) under `rules/framework/2-application-flow/axis-saga/`; open **only** the rule the
context requires.

## Rule map

### Start here — is a saga the right tool, and how to declare one ⭐

| Context / what you were about to write | Rule |
|---|---|
| About to hand-roll orchestration, a recovery loop, an engine call, or manual compensation — route the intent first | [saga-route-imperative-smells](../../rules/framework/2-application-flow/axis-saga/saga-route-imperative-smells.yaml) |
| Declare a saga topology | [saga-define-entrypoint](../../rules/framework/2-application-flow/axis-saga/saga-define-entrypoint.yaml) |
| Add forward stages vs compensation stages | [saga-configurator-forward-and-error-stages](../../rules/framework/2-application-flow/axis-saga/saga-configurator-forward-and-error-stages.yaml) |

### Building the definition (the fluent configurator)

| Context | Rule |
|---|---|
| Naming stages (non-empty, unique) | [saga-stage-names-unique-and-nonempty](../../rules/framework/2-application-flow/axis-saga/saga-stage-names-unique-and-nonempty.yaml) |
| A saga needs an entry point | [saga-at-least-one-forward-stage](../../rules/framework/2-application-flow/axis-saga/saga-at-least-one-forward-stage.yaml) |
| Wire success routing — `NextStageOnSuccess` vs `FinishOnSuccess` | [saga-success-next-xor-finish](../../rules/framework/2-application-flow/axis-saga/saga-success-next-xor-finish.yaml) |
| Wire the compensation chain on failure | [saga-route-to-on-error](../../rules/framework/2-application-flow/axis-saga/saga-route-to-on-error.yaml) |
| Every route target must exist (validated at build) | [saga-routes-validated-at-build](../../rules/framework/2-application-flow/axis-saga/saga-routes-validated-at-build.yaml) |
| Override the transient-retry cap for one stage | [saga-retry-on-transient-override](../../rules/framework/2-application-flow/axis-saga/saga-retry-on-transient-override.yaml) |

### Writing a stage handler

| Context | Rule |
|---|---|
| The handler contract (SagaName, StageName, ExecuteAsync) | [saga-stage-handler-shape](../../rules/framework/2-application-flow/axis-saga/saga-stage-handler-shape.yaml) |
| Why handlers MUST be idempotent | [saga-stage-handler-idempotent](../../rules/framework/2-application-flow/axis-saga/saga-stage-handler-idempotent.yaml) |
| Return a failed `AxisResult` — never throw to signal failure | [saga-stage-handler-returns-result-not-throw](../../rules/framework/2-application-flow/axis-saga/saga-stage-handler-returns-result-not-throw.yaml) |
| How the invoker matches a handler (off the interface type) | [saga-handler-matched-off-interface](../../rules/framework/2-application-flow/axis-saga/saga-handler-matched-off-interface.yaml) |
| Each stage runs in its own DI scope (no faulted-UoW bleed) | [saga-stage-runs-in-own-scope](../../rules/framework/2-application-flow/axis-saga/saga-stage-runs-in-own-scope.yaml) |
| Register handlers — `AddAxisSagaHandlers(assembly)` | [saga-handlers-di-registration](../../rules/framework/2-application-flow/axis-saga/saga-handlers-di-registration.yaml) |
| The payload — reference type, JSON, carried stage to stage | [saga-payload-json-persistence](../../rules/framework/2-application-flow/axis-saga/saga-payload-json-persistence.yaml) |

### Starting and reading a saga (the mediator)

| Context | Rule |
|---|---|
| `StartAsync` — persist then fire the engine in the background | [saga-mediator-start](../../rules/framework/2-application-flow/axis-saga/saga-mediator-start.yaml) |
| Why the caller's ambient token can't cancel the saga | [saga-fire-and-forget-suppresses-flow](../../rules/framework/2-application-flow/axis-saga/saga-fire-and-forget-suppresses-flow.yaml) |
| `GetByIdAsync` — poll state, typed or untyped | [saga-mediator-get-by-id](../../rules/framework/2-application-flow/axis-saga/saga-mediator-get-by-id.yaml) |
| `ResumeAsync` — a fire-and-forget signal, harmless on terminal | [saga-resume-is-signal](../../rules/framework/2-application-flow/axis-saga/saga-resume-is-signal.yaml) |

### How the engine drives it (execution semantics)

| Context | Rule |
|---|---|
| The engine is internal — driven only via mediator/resumer | [saga-engine-internal](../../rules/framework/2-application-flow/axis-saga/saga-engine-internal.yaml) |
| Single execution via a heartbeat-renewed lease | [saga-execution-lease](../../rules/framework/2-application-flow/axis-saga/saga-execution-lease.yaml) |
| Every mutation guarded by version + lease ownership | [saga-guarded-mutations](../../rules/framework/2-application-flow/axis-saga/saga-guarded-mutations.yaml) |
| Forward → Completed, or compensate → Compensated (else Failed) | [saga-forward-then-compensation](../../rules/framework/2-application-flow/axis-saga/saga-forward-then-compensation.yaml) |
| Compensation runs in the LISTED order — no auto-reversal | [saga-compensation-listed-order](../../rules/framework/2-application-flow/axis-saga/saga-compensation-listed-order.yaml) |
| Resume skips stages already logged Completed | [saga-skip-completed-stage-on-resume](../../rules/framework/2-application-flow/axis-saga/saga-skip-completed-stage-on-resume.yaml) |
| Transient failures retried in place before compensating | [saga-transient-retry-in-place](../../rules/framework/2-application-flow/axis-saga/saga-transient-retry-in-place.yaml) |
| The per-attempt stage log (Started → Completed \| Failed) | [saga-stage-logs-append](../../rules/framework/2-application-flow/axis-saga/saga-stage-logs-append.yaml) |

### Recovery, lifecycle & the resumer

| Context | Rule |
|---|---|
| The resumer worker is hosted for you (never hand-roll one) | [saga-resumer-hosted-by-framework](../../rules/framework/2-application-flow/axis-saga/saga-resumer-hosted-by-framework.yaml) |
| What "stuck" means — non-terminal + expired lease | [saga-resumer-claims-stale-leases](../../rules/framework/2-application-flow/axis-saga/saga-resumer-claims-stale-leases.yaml) |
| The process-wide concurrency cap in the database | [saga-global-concurrency-cap](../../rules/framework/2-application-flow/axis-saga/saga-global-concurrency-cap.yaml) |
| Retention window + the janitor deleting terminal sagas | [saga-janitor-retention](../../rules/framework/2-application-flow/axis-saga/saga-janitor-retention.yaml) |
| The definition catalogue upsert (auditing) | [saga-definition-initializer-upsert](../../rules/framework/2-application-flow/axis-saga/saga-definition-initializer-upsert.yaml) |

### Wiring, storage & providers

| Context | Rule |
|---|---|
| The store ports are the dialect seam (never throw) | [saga-storage-ports-are-dialect-seam](../../rules/framework/2-application-flow/axis-saga/saga-storage-ports-are-dialect-seam.yaml) |
| `AddAxisSagaCore` — what the runtime registration wires | [saga-add-core-registers-runtime](../../rules/framework/2-application-flow/axis-saga/saga-add-core-registers-runtime.yaml) |
| One saga storage per process (keyless refuses a second) | [saga-single-storage-per-process](../../rules/framework/2-application-flow/axis-saga/saga-single-storage-per-process.yaml) |
| Several stores per process — the keyed overload | [saga-keyed-per-subdomain](../../rules/framework/2-application-flow/axis-saga/saga-keyed-per-subdomain.yaml) |
| The `AXIS_SAGA` schema — declared once, migrated idempotently | [saga-schema-declared-once-idempotent](../../rules/framework/2-application-flow/axis-saga/saga-schema-declared-once-idempotent.yaml) |
| MySQL pins READ COMMITTED and owns its datasource | [saga-mysql-read-committed](../../rules/framework/2-application-flow/axis-saga/saga-mysql-read-committed.yaml) |

### State & error contract

| Context | Rule |
|---|---|
| The status model (`AxisSagaStatus`, `AxisSagaStageStatus`, the instance) | [saga-status-model](../../rules/framework/2-application-flow/axis-saga/saga-status-model.yaml) |
| The saga error-code constants (`AxisSagaErrors`) | [saga-error-code-contract](../../rules/framework/2-application-flow/axis-saga/saga-error-code-contract.yaml) |

## See also

- `axis-bus` — fire-and-forget domain events (the saga's fan-out seam, and the alternative when there is nothing to undo).
- `axis-result` — the monad each stage returns; the failure rail that drives routing/compensation and the transient classification.
- `axis-mediator` — the ambient context/`CancellationToken` model the saga deliberately does NOT flow into its background run.
- `axis-domain-modeling` — the ports/aggregates the stage handlers call.
- `axis-webapi-controllers` — the 202 + GET-status endpoint pattern that exposes a saga.
- `axis-migrations` — the framework migration runner that applies the `AXIS_SAGA` schema.
- `axis-rules` — how these rules are authored and maintained (the extraction method from code).
