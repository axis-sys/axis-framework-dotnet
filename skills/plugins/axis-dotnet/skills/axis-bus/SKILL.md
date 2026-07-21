---
name: axis-bus
description: >
  Publish and consume asynchronous domain events on Axis with `IAxisBus` + `IAxisEvent` +
  `IAxisEventHandler<T>`. Use when decoupling a side effect across bounded contexts (a fact happened,
  notify others without the emitter knowing the consumers), fanning one event out to several handlers in
  parallel, or choosing a durable outbox over in-process delivery. This skill is a MAP: each row points to
  the canonical rule in `rules/` — open only the one the context asks for. It does NOT restate invariants
  nor carry code. It does NOT cover orchestration with compensation (→ axis-saga), the return monad
  (→ axis-result), the ambient context/dispatch (→ axis-mediator), nor the swappable-infra-port pattern in
  the abstract (→ axis-dotnet-architect).
---

# AxisBus — rule map (async event publish/subscribe)

The **bus** is fire-and-topology: a publisher states that a *fact* happened and hands it to `IAxisBus`, which
fans it out to every registered `IAxisEventHandler<TEvent>` and folds the results into one `AxisResult`. The
emitter never names a consumer. The port has one method (`PublishAsync`); the transport is an adapter choice —
in-process (`AxisMemoryBus`), a durable outbox (`AxisBus.Repository` over Postgres/MySQL), or your own broker.
The package is 2-application-flow; `IAxisEvent` / `IAxisEventHandler<T>` live in `AxisMediator.Contracts`.

This skill **does not restate** the invariants nor carry code — it **routes**. Each map row points to the
canonical rule (in English) under `rules/framework/2-application-flow/axis-bus/`; open **only** the rule the
context requires.

## Rule map

### Start here — route by intent ⭐

| Context / what you were about to write | Rule |
|---|---|
| A fact just became true and others may react — which channel? (bus vs mediator vs saga vs Facade) | [bus-publish-not-mediator](../../rules/framework/2-application-flow/axis-bus/bus-publish-not-mediator.yaml) |
| The publishing surface itself — one `PublishAsync`, no subscribe method | [bus-publish-port](../../rules/framework/2-application-flow/axis-bus/bus-publish-port.yaml) |

### The contract — events, handlers, results, topics

| Context | Rule |
|---|---|
| Model an event (record, past-tense, `IAxisEvent` marker) | [bus-event-marker](../../rules/framework/2-application-flow/axis-bus/bus-event-marker.yaml) |
| Write a consumer (`IAxisEventHandler<TEvent>` → `Task<AxisResult>`) | [bus-event-handler-shape](../../rules/framework/2-application-flow/axis-bus/bus-event-handler-shape.yaml) |
| Return a failure on the rail, never throw for control flow | [bus-handler-returns-result](../../rules/framework/2-application-flow/axis-bus/bus-handler-returns-result.yaml) |
| What `params string[] topics` mean (adapter hints, not contract semantics) | [bus-topics-are-adapter-hints](../../rules/framework/2-application-flow/axis-bus/bus-topics-are-adapter-hints.yaml) |
| What the contract does NOT promise (durability / ordering / retries) | [bus-no-delivery-guarantees](../../rules/framework/2-application-flow/axis-bus/bus-no-delivery-guarantees.yaml) |

### Fan-out semantics

| Context | Rule |
|---|---|
| Publish runs every handler in parallel; no cross-handler ordering | [bus-fanout-parallel](../../rules/framework/2-application-flow/axis-bus/bus-fanout-parallel.yaml) |
| Publishing with no handler registered is a trivial success | [bus-no-handlers-ok](../../rules/framework/2-application-flow/axis-bus/bus-no-handlers-ok.yaml) |
| Handler failures aggregate into one `AxisResult` via `Combine` | [bus-failures-aggregate](../../rules/framework/2-application-flow/axis-bus/bus-failures-aggregate.yaml) |
| The emitter does not know its consumers (the decoupling reason) | [bus-emitter-topology-decoupled](../../rules/framework/2-application-flow/axis-bus/bus-emitter-topology-decoupled.yaml) |

### In-process adapter — `AxisMemoryBus`

| Context | Rule |
|---|---|
| Wiring — `AddAxisMemoryBus()` (adapter + handler scan, Scoped) | [bus-memory-registration](../../rules/framework/2-application-flow/axis-bus/bus-memory-registration.yaml) |
| Sharp edge — a throwing handler escapes; wrap risky work in `TryAsync` | [bus-memory-no-exception-isolation](../../rules/framework/2-application-flow/axis-bus/bus-memory-no-exception-isolation.yaml) |

### Writing your own adapter

| Context | Rule |
|---|---|
| The contract a custom `IAxisBus` (Kafka, broker, outbox, test double) must honour | [bus-adapter-contract](../../rules/framework/2-application-flow/axis-bus/bus-adapter-contract.yaml) |

### Durable atomic outbox adapter — `AxisBus.Repository` (Postgres / MySQL)

The write path is atomic: publishing **enqueues** on a request-scoped queue, and the unit of work **drains** it into its own transaction at commit — the event and the business state change commit together, so there is no publishing without a UoW (see also [architecture-events-published-in-unit-of-work](../../rules/conventions/architecture/architecture-events-published-in-unit-of-work.yaml)). There is no status column, no per-row attempts/retry and no retention — a row's presence is its "pending" state and delivery is its deletion. (The earlier autocommit / Pending-status / attempts / retention rules are superseded, kept only for history.)

| Context | Rule |
|---|---|
| The shape — write path enqueues, the unit of work drains at commit, read path dispatches | [bus-outbox-write-read-split](../../rules/framework/2-application-flow/axis-bus/bus-outbox-write-read-split.yaml) |
| Atomicity — publishing enqueues; the unit of work drains the queue into **its own transaction** at commit, so the event and the business state commit together (no publish without a UoW) | [bus-outbox-enqueue-in-uow-transaction](../../rules/framework/2-application-flow/axis-bus/bus-outbox-enqueue-in-uow-transaction.yaml) |
| Payload (de)serialization never throws — exceptions become an `AxisError` | [bus-outbox-serializer-no-throw](../../rules/framework/2-application-flow/axis-bus/bus-outbox-serializer-no-throw.yaml) |
| The dispatcher reproduces the in-memory fan-out (via reflection) | [bus-outbox-dispatch-mirrors-fanout](../../rules/framework/2-application-flow/axis-bus/bus-outbox-dispatch-mirrors-fanout.yaml) |
| One fresh service scope per row (handler isolation) | [bus-outbox-scope-per-row](../../rules/framework/2-application-flow/axis-bus/bus-outbox-scope-per-row.yaml) |
| A throwing handler never escapes the poll loop (unlike in-memory) | [bus-outbox-dispatch-swallows-handler-exception](../../rules/framework/2-application-flow/axis-bus/bus-outbox-dispatch-swallows-handler-exception.yaml) |
| Failure handling — a failed delivery releases the row; the **worker** backs off exponentially and raises a critical alert (no per-row retry, no terminal state) | [bus-outbox-worker-backoff-alert](../../rules/framework/2-application-flow/axis-bus/bus-outbox-worker-backoff-alert.yaml) |
| Claim-by-lease — at-least-once, one runner per partition head; a dispatched row is deleted | [bus-outbox-lease-claim](../../rules/framework/2-application-flow/axis-bus/bus-outbox-lease-claim.yaml) |
| One storage adapter per process (double-registration throws) | [bus-outbox-single-storage-per-process](../../rules/framework/2-application-flow/axis-bus/bus-outbox-single-storage-per-process.yaml) |
| Wiring — `AddAxisBusPostgres` / `AddAxisBusMySql` + core lifetimes | [bus-outbox-di-registration](../../rules/framework/2-application-flow/axis-bus/bus-outbox-di-registration.yaml) |
| The two hosted workers (schema bootstrap + poll loop), opt-out | [bus-outbox-hosted-workers](../../rules/framework/2-application-flow/axis-bus/bus-outbox-hosted-workers.yaml) |
| The `AXIS_OUTBOX.OUTBOX_EVENTS` schema (one table, no status column, delete-on-dispatch) | [bus-outbox-schema](../../rules/framework/2-application-flow/axis-bus/bus-outbox-schema.yaml) |
| Dialect divergence — only INSERT for publish, full divergence for claim | [bus-outbox-dialect-divergence](../../rules/framework/2-application-flow/axis-bus/bus-outbox-dialect-divergence.yaml) |

## See also

- `axis-saga` — orchestration across 2+ bounded contexts with compensation and persisted state; the bus is fire-and-forget, the saga names and sequences its stages.
- `axis-result` — the monad both the port and its handlers return (`AxisResult.Combine` aggregates the fan-out).
- `axis-use-case-cqrs` — the command/query the publisher usually runs before it publishes the resulting fact.
- `axis-mediator` — the scoped ambient `IAxisMediator` the durable bus components resolve their `IAxisLogger<T>` from (why they are Scoped), and the `AddCqrsMediator` scan `AddAxisMemoryBus` uses to discover handlers.
- `axis-dotnet-architect` — the hub; the swappable-infra-port pattern (`IAxis*` + `AxisResult` + `AddAxis*`) the bus adapters are one instance of.
