# Why AxisMediator? · comparison

> There are other ways to do CQRS / request mediation in .NET. This page tells you why AxisMediator is different — a direct comparison, no hand-waving.

---

## vs. MediatR

`MediatR` is the canonical library. AxisMediator differs deliberately:

1. **Returns `AxisResult`.** MediatR's `Send` returns `TResponse` and you wrap it yourself; AxisMediator returns `AxisResult<TResponse>`.
2. **Marker interfaces per shape.** `IAxisCommand`, `IAxisCommand<TResponse>`, `IAxisQuery<TResponse>`, `IAxisStreamQuery<TItem>`, `IAxisEvent` — each has its own typed handler and dispatch method. MediatR collapses commands and queries into `IRequest<T>`.
3. **Ambient context built-in.** `IAxisMediator.TraceId`/`OriginId`/`JourneyId`/`AxisEntityId`/`CancellationToken` come for free.
4. **Pipeline context.** `AxisPipelineContext` makes behaviour-to-behaviour value passing a first-class operation. MediatR makes you smuggle through DI.
5. **No notifications in the mediator.** Events go through [`AxisBus`](../AxisBus/README.md). That keeps the mediator focused on request/response and lets you swap the bus for an outbox without touching the mediator.

## vs. MassTransit's `IMediator`

`MassTransit.Mediator` is great if you already use MassTransit. AxisMediator is **smaller**, **opinionated about CQRS shapes**, and integrated with the rest of Axis (logger, validator, telemetry, repository, saga). If you do not need MassTransit for the bus, AxisMediator + AxisBus is lighter.

## vs. a hand-rolled service per use case

DIY. Same shape but you re-derive: behaviours, ambient context, the scanner, the `AxisPipelineContext`, the logger / validator / telemetry plumbing. AxisMediator saves the cost — and keeps the pipeline consistent across teams.

---

## The comparison

| Feature | AxisMediator | MediatR | MassTransit.Mediator | Hand-rolled |
|---|:--:|:--:|:--:|:--:|
| Returns `AxisResult` | **Yes** | No | No | Maybe |
| Typed `Command`/`Query`/`Stream`/`Event` markers | **Yes** | Partial | Yes | Maybe |
| Ambient `TraceId`/`OriginId`/`JourneyId`/`AxisEntityId` | **Yes** | No | Partial | Maybe |
| Open-generic pipeline behaviours | **Yes** | Yes | Yes | Maybe |
| Per-call pipeline context (`AxisPipelineContext`) | **Yes** | No | No | Maybe |
| Events live in a separate package (`AxisBus`) | **Yes** | Notifications inside the mediator | Yes | Maybe |
| Assembly scanner (`AddCqrsMediator`) | **Yes** | Yes | Yes | Maybe |
| In-box behaviours: log / validate / telemetry / performance | **Yes** (own packages) | No | Yes | Maybe |

---

## See also

- [Getting started](getting-started.md) — install and dispatch
- [CQRS · commands, queries, streams, events](cqrs.md) — the request shapes
- [Pipeline behaviours](pipeline-behaviors.md) — the open-generic extension point

---

↩ [Back to AxisMediator docs](README.md)
