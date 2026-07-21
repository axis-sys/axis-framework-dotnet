---
name: axis-logger
description: >
  Structured, correlation-aware logging on Axis through `IAxisLogger<T>` — six named log-level methods plus
  the railway-aware `LogResult`, every entry auto-enriched with `UtcTime`/`OriginId`/`TraceId`/`JourneyId`
  from the ambient `IAxisMediator`. Use when logging inside the mediator pipeline (handlers, behaviours,
  adapters), recording an `AxisResult` outcome at the end of a pipeline (`LogResult` or the chainable
  `LogIfSuccess`/`LogIfFailure`), or turning on automatic per-request "Handling X" logging. This skill is a
  MAP: each row points to the canonical rule in `rules/` — open only the one the context asks for. It does
  NOT restate invariants nor carry code. It does NOT cover the monadic composition the outcome logging slots
  into (→ axis-result), the ambient context/correlation ids themselves (→ axis-mediator), nor metrics/traces
  (→ axis-telemetry).
---

# AxisLogger — rule map (structured, correlation-aware logging)

`IAxisLogger<T>` is a thin wrapper over `Microsoft.Extensions.Logging.ILogger<T>` that does two jobs raw
`ILogger` will not: it **auto-enriches** every entry (per-entry `BeginScope` with `UtcTime` plus the ambient
`OriginId`/`TraceId`/`JourneyId` from `IAxisMediator`), and it adds **railway-aware outcome logging**
(`LogResult`, and the chainable `LogIfSuccess`/`LogIfFailure` extensions). `T` is the logging category. The
package is `1-observability`; the concrete adapter is internal, so code depends only on the interface and a
single `AddAxisLogger()` at the composition root. An opt-in `AddLoggingBehavior()` brackets every request
with a "Handling X" entry.

This skill **does not restate** the invariants nor carry code — it **routes**. Each map row points to the
canonical rule (in English) under `rules/framework/1-observability/axis-logger/`; open **only** the rule the
context requires.

## Rule map

### Start here — route by intent ⭐

| Context / what you were about to write | Rule |
|---|---|
| The port surface — six named levels + `LogResult`, no generic `Log(level, ...)` | [logger-contract-named-levels](../../rules/framework/1-observability/axis-logger/logger-contract-named-levels.yaml) |
| Log an `AxisResult` outcome in one line at a pipeline exit | [logger-log-result-outcome](../../rules/framework/1-observability/axis-logger/logger-log-result-outcome.yaml) |
| Log an outcome *inside a Then/Match chain* without a manual `IsFailure` check | [logger-result-log-if](../../rules/framework/1-observability/axis-logger/logger-result-log-if.yaml) |
| Wiring — register the logger, and optionally per-request logging | [logger-registration-scoped](../../rules/framework/1-observability/axis-logger/logger-registration-scoped.yaml) |

### The contract — `IAxisLogger<T>`

| Context | Rule |
|---|---|
| Six named level methods + `LogResult`, contravariant category `T`, internal adapter | [logger-contract-named-levels](../../rules/framework/1-observability/axis-logger/logger-contract-named-levels.yaml) |
| `LogError` has two overloads — only the `Exception` one attaches the stack trace | [logger-error-exception-overload](../../rules/framework/1-observability/axis-logger/logger-error-exception-overload.yaml) |

### Structured logging & always-on enrichment

| Context | Rule |
|---|---|
| Pass values as `(Key, Value)` pairs, never interpolated into the message | [logger-structured-properties](../../rules/framework/1-observability/axis-logger/logger-structured-properties.yaml) |
| Every entry carries `UtcTime`/`OriginId`/`TraceId`/`JourneyId` (per-entry `BeginScope`) | [logger-ambient-scope-enrichment](../../rules/framework/1-observability/axis-logger/logger-ambient-scope-enrichment.yaml) |
| A disabled level short-circuits before any scope is built | [logger-level-guard-short-circuit](../../rules/framework/1-observability/axis-logger/logger-level-guard-short-circuit.yaml) |

### Logging an `AxisResult` outcome

| Context | Rule |
|---|---|
| `LogResult(tag, result)` — Information/Error by outcome, with `Tag`/`RequestName`/`AxisErrorList` | [logger-log-result-outcome](../../rules/framework/1-observability/axis-logger/logger-log-result-outcome.yaml) |
| `LogIfSuccess`/`LogIfFailure` — Tap-family, chainable, caller-chosen severity | [logger-result-log-if](../../rules/framework/1-observability/axis-logger/logger-result-log-if.yaml) |

### Wiring & the request-logging behaviour

| Context | Rule |
|---|---|
| `AddAxisLogger()` — `IAxisLogger<>` as open-generic **Scoped** (+ the `TimeProvider` gotcha) | [logger-registration-scoped](../../rules/framework/1-observability/axis-logger/logger-registration-scoped.yaml) |
| `AddLoggingBehavior()` — `TimeProvider.System` singleton + `LoggingBehavior` as transient behaviours | [logger-behavior-registration](../../rules/framework/1-observability/axis-logger/logger-behavior-registration.yaml) |
| What `LoggingBehavior` logs — "Handling {RequestName}" before `next()`, no outcome, no timing | [logger-behavior-logs-handling](../../rules/framework/1-observability/axis-logger/logger-behavior-logs-handling.yaml) |

## See also

- `axis-result` — the monad `LogResult` / `LogIfSuccess` / `LogIfFailure` observe; outcome logging slots into a `.ThenAsync` / `.TapAsync` chain instead of a manual `if (result.IsFailure)`.
- `axis-mediator` — the ambient `IAxisMediator` the logger reads `OriginId`/`TraceId`/`JourneyId` from, and the request scope the Scoped logger lives in; also owns the pipeline the `LoggingBehavior` plugs into.
- `axis-telemetry` — the sibling `1-observability` port for metrics and traces (not logs).
- `axis-dotnet-architect` — the hub; `IAxisLogger<T>` is the observability face injected across handlers, adapters and services.
