# Why AxisLogger? · comparison

> There are other ways to log structured entries in .NET. This page tells you why AxisLogger is different — a direct comparison, no hand-waving.

---

## vs. `ILogger<T>` (directly)

`ILogger<T>` is the substrate AxisLogger sits on. Calling it directly has three rough edges:

1. **Manual enrichment.** Every handler has to remember `BeginScope(new Dictionary { "TraceId", … })` — or worse, no scope at all.
2. **No `LogResult`.** You write `if (result.IsSuccess) logger.LogInformation(...) else logger.LogError(...)` by hand at every pipeline exit.
3. **No mediator integration.** You cannot derive `OriginId`/`TraceId`/`JourneyId` without injecting `IAxisMediator` everywhere.

**AxisLogger** wraps `ILogger<T>` with an automatic enrichment scope and the railway-aware `LogResult`. The sinks remain — Serilog, OpenTelemetry, Datadog, console — but the **plumbing** is centralised.

## vs. `Serilog` (directly)

Serilog has its own `ILogger` interface with destructuring (`{@Order}`). Lovely API, but you lose:

- The `Microsoft.Extensions.Logging.ILogger<T>` category abstraction (so config-driven filters get harder).
- Cross-stack adapters in the .NET ecosystem (most NuGet packages log to `ILogger<T>`).

**AxisLogger** keeps the abstraction and lets you plug Serilog underneath via `Microsoft.Extensions.Logging.Serilog`. You can still destructure — pass an object as `("Order", order)` and Serilog will render it according to its config.

## vs. `LogContext.PushProperty` / Serilog enrichers

Per-call `PushProperty` invites mistakes (forgetting to dispose, double-pushes, race conditions). Enrichers are global, which is fine for static fields like machine name, but wrong for per-request fields like `TraceId`. **AxisLogger** uses `BeginScope` once per entry — the right tool for per-call enrichment.

## vs. a bespoke `IMyLogger`

DIY. Same shape, but you also write the auto-enrichment, the level-selection in `LogResult`, the open-generic pipeline behaviours, and the tests. `IAxisLogger<T>` saves the cost — and integrates with the rest of Axis for free.

---

## The comparison

| Feature | AxisLogger | `ILogger<T>` direct | Serilog direct | Bespoke `IMyLogger` |
|---|:--:|:--:|:--:|:--:|
| Structured properties via `params (Key, Value)[]` | **Yes** | Manual | Yes (destructuring) | Maybe |
| Auto-enrichment `TraceId`/`OriginId`/`JourneyId` | **Yes** | No | Per-context (manual) | Maybe |
| `LogResult` for `AxisResult` outcomes | **Yes** | No | No | Maybe |
| Pipeline behaviour for automatic request logging | **Yes** | No | No | Maybe |
| Plays well with any `ILogger` sink | **Yes** | Yes | Partial | Maybe |
| Tiny — wraps `ILogger<T>` | **Yes** | Yes | No (own pipeline) | Maybe |
| Zero NuGet deps beyond `Microsoft.Extensions.Logging.*` | **Yes** | Yes | No | Maybe |

---

## See also

- [The `IAxisLogger<T>` contract](iaxislogger.md) — the surface
- [`LogResult`](log-result.md) — the operator that justifies the abstraction
- [`LoggingBehavior`](logging-behavior.md) — opt-in pipeline behaviour

---

↩ [Back to AxisLogger docs](README.md)
