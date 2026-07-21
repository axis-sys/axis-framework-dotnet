---
name: axis-mediator
description: >
  The Axis ambient execution context and the CQRS dispatch — `IAxisMediator` (identity, tracing, ambient
  `CancellationToken` and `Cqrs`) plus the contracts from the `AxisMediator.Contracts` package: request/response
  markers, handlers per message type, pipeline behaviors and `AxisPipelineContext`. Use when writing a handler
  (command/query/event/stream), dispatching via the Facade, reading the authenticated identity (`AxisEntityId`,
  null = anonymous) or the ambient `CancellationToken` (NEVER a parameter), or adding a pipeline behavior. This
  skill is a MAP: each row points to the canonical rule in `rules/` — open only the one the context asks for.
  It does NOT cover the Facade/vertical slice in detail (→ axis-use-case-cqrs, owner of the Facade), validation
  (→ axis-validator), the return monad (→ axis-result) nor event publish/subscribe (→ axis-bus).
---

# AxisMediator — rule map (ambient context + CQRS dispatch)

The **mediator** is the heart of the Axis execution context: it carries identity, tracing and cancellation
for the current request and exposes `Cqrs` for dispatch. The contracts (markers, handlers, pipeline) live
in `AxisMediator.Contracts` (0-foundations family); the implementation (dispatch, accessors, built-in
behaviors) in the `AxisMediator` package (2-application-flow) — one primitive, two packages for layering.

This skill **does not restate** the invariants nor carry code — it **routes**. Each map row points to the
canonical rule (in English) under `rules/framework/0-foundations/axis-mediator-contracts/`; the AI opens
**only** the rule the context requires.

## Rule map

### Start here — write/dispatch a use case ⭐

| Context / what you were about to write | Rule |
|---|---|
| Declare a command/query/event/stream handler (shape + `Task<AxisResult<…>>` return) | [mediator-handler-shape](../../rules/framework/0-foundations/axis-mediator-contracts/mediator-handler-shape.yaml) |
| Dispatch a command/query/stream — via `mediator.Cqrs`, and **only in the Facade** | [mediator-dispatch-surface](../../rules/framework/0-foundations/axis-mediator-contracts/mediator-dispatch-surface.yaml) |

### Ambient context

| Context | Rule |
|---|---|
| About to pass/receive a `CancellationToken` in a handler or port — **don't**, it's ambient | [mediator-cancellation-is-ambient](../../rules/framework/0-foundations/axis-mediator-contracts/mediator-cancellation-is-ambient.yaml) |
| Read `AxisEntityId`/`TraceId`/`OriginId`/`JourneyId` (via `IAxisMediator`, with a null guard) | [mediator-ambient-context-access](../../rules/framework/0-foundations/axis-mediator-contracts/mediator-ambient-context-access.yaml) |

### Types and markers

| Context | Rule |
|---|---|
| The request/response marker hierarchy and the command-vs-query asymmetry | [mediator-request-response-markers](../../rules/framework/0-foundations/axis-mediator-contracts/mediator-request-response-markers.yaml) |

### Events

| Context | Rule |
|---|---|
| About to "publish" a domain event through the mediator — **don't**, use the bus (out-of-band) | [mediator-events-out-of-band](../../rules/framework/0-foundations/axis-mediator-contracts/mediator-events-out-of-band.yaml) |

### Pipeline

| Context | Rule |
|---|---|
| Add a cross-cutting concern (behavior, `next()` once, two overloads) | [mediator-pipeline-behavior](../../rules/framework/0-foundations/axis-mediator-contracts/mediator-pipeline-behavior.yaml) |
| Pass state between behaviors (`AxisPipelineContext`, keys via `AxisPipelineContextKeys`) | [mediator-pipeline-context](../../rules/framework/0-foundations/axis-mediator-contracts/mediator-pipeline-context.yaml) |

## Runtime (dispatch, accessors, DI)

The rows above map the **contracts** (`AxisMediator.Contracts`, 0-foundations). The rows below map the
**runtime** (`AxisMediator`, 2-application-flow) — how the dispatch engine, accessors, lifetimes and
embedded behaviors actually behave. Open only the one the context asks for.

| Context / what you were about to do | Rule |
|---|---|
| Wire the mediator once — `AddAxisMediator` (mediator+handler **Scoped**, accessors **Singleton**) | [mediator-runtime-di-registration](../../rules/framework/2-application-flow/axis-mediator/mediator-runtime-di-registration.yaml) |
| Register handlers by assembly scan — `AddCqrsMediator(assembly)` (**Transient**, six interface shapes) | [mediator-runtime-cqrs-scan](../../rules/framework/2-application-flow/axis-mediator/mediator-runtime-cqrs-scan.yaml) |
| Dispatch a command/query with no handler registered — get `AxisError.NotFound` (stream **throws** instead) | [mediator-runtime-handler-not-found](../../rules/framework/2-application-flow/axis-mediator/mediator-runtime-handler-not-found.yaml) |
| Reason about behavior **order** — fresh `AxisPipelineContext`, `Reverse()`+fold ⇒ registration order | [mediator-runtime-behavior-pipeline](../../rules/framework/2-application-flow/axis-mediator/mediator-runtime-behavior-pipeline.yaml) |
| Add the in-box `PerformanceBehavior` — opt-in, transparent, warns past 500ms | [mediator-runtime-performance-behavior](../../rules/framework/2-application-flow/axis-mediator/mediator-runtime-performance-behavior.yaml) |
| Understand per-dispatch logging — `LogResult` resolves `ILogger<>` on every call | [mediator-runtime-log-result](../../rules/framework/2-application-flow/axis-mediator/mediator-runtime-log-result.yaml) |
| The mediator's own `AsyncLocal` accessor — published on ctor, cleared on `Dispose` | [mediator-runtime-mediator-accessor](../../rules/framework/2-application-flow/axis-mediator/mediator-runtime-mediator-accessor.yaml) |
| Where the ambient context values live — `AsyncLocal` fields, set by app middleware | [mediator-runtime-context-accessor](../../rules/framework/2-application-flow/axis-mediator/mediator-runtime-context-accessor.yaml) |
| How `TraceId` is chosen — once, from `Activity.Current` or a new `Guid` | [mediator-runtime-traceid-resolution](../../rules/framework/2-application-flow/axis-mediator/mediator-runtime-traceid-resolution.yaml) |

## See also

- `axis-use-case-cqrs` — owner of the **Facade** that calls `mediator.Cqrs` and of the full vertical slice
  (Command/Query, Response, Handler, Validator).
- `axis-result` — the monad the handlers return (`ThenAsync`/`EnsureAsync`/`MapAsync`); reinforces the same
  rule that the `CancellationToken` is ambient.
- `axis-validator` — the pipeline's `ValidationBehavior` in detail.
- `axis-bus` — event publish/subscribe (`IAxisEvent`/`IAxisEventHandler`), the out-of-band channel.
- `axis-rules` — how these rules are authored and maintained (the extraction method from code).
