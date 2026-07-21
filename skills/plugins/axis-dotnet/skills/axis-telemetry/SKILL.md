---
name: axis-telemetry
description: >
  Instrument code on Axis through `IAxisTelemetry` (spans) and `IAxisMetrics` (counters/histograms) — the
  two narrow observability ports — plus the opt-in `TelemetryBehavior` that auto-traces every mediator
  request and the framework tag-name constants. Use when opening a span around a unit of work, recording a
  counter or histogram, wiring the OpenTelemetry sink, turning telemetry off in tests with the Null adapter,
  or tagging spans with the framework signals. This skill is a MAP: each row points to the canonical rule in
  `rules/` — open only the one the context asks for. It does NOT restate invariants nor carry code. It does
  NOT cover the mediator pipeline itself (→ axis-mediator), the return monad (→ axis-result), nor structured
  logging (→ axis-logger).
---

# AxisTelemetry — rule map (spans, metrics, auto-instrumentation)

**Telemetry** is two deliberately small swappable ports: `IAxisTelemetry` opens spans (`StartSpan` →
`IAxisSpan`, plus `CurrentTraceId`/`CurrentSpanId`) and `IAxisMetrics` records counters and histograms.
Neither returns `AxisResult` — telemetry is fire-and-forget side effect. In-box there are two sinks:
`NullAxisTelemetry` (the no-op you register to turn telemetry off) and `OpenTelemetryAdapter` (production,
over the BCL `ActivitySource` + `Meter` named `"Axis.AxisMediator"`); the companion package
`AxisTelemetry.AzureMonitor` pairs the OpenTelemetry sink with the official Azure Monitor distro in one
call (`AddAzureMonitorAxis`), degrading to the Null sink when no connection string is configured.
`TelemetryBehavior` is the opt-in
pipeline behavior that traces and meters every mediator request automatically. The package is
1-observability; the tag keys are a sink-side contract pinned by a golden test.

This skill **does not restate** the invariants nor carry code — it **routes**. Each map row points to the
canonical rule (in English) under `rules/framework/1-observability/axis-telemetry/`; open **only** the rule
the context requires.

## Rule map

### Start here — route by intent ⭐

| Context / what you were about to write | Rule |
|---|---|
| About to call `ActivitySource.StartActivity` / a raw `Meter` — reach for the port first | [telemetry-prefer-port-over-activitysource](../../rules/framework/1-observability/axis-telemetry/telemetry-prefer-port-over-activitysource.yaml) |
| The surface — spans (`IAxisTelemetry`) vs counters/histograms (`IAxisMetrics`) | [telemetry-two-ports](../../rules/framework/1-observability/axis-telemetry/telemetry-two-ports.yaml) |
| Auto-trace + meter every mediator request with one behavior | [telemetry-behavior-wraps-dispatch](../../rules/framework/1-observability/axis-telemetry/telemetry-behavior-wraps-dispatch.yaml) |
| Turn telemetry **off** without touching call sites | [telemetry-null-default-noop](../../rules/framework/1-observability/axis-telemetry/telemetry-null-default-noop.yaml) |

### The span — `IAxisSpan`

| Context | Rule |
|---|---|
| Fluent mutators + `using var` disposal — one span per unit of work | [telemetry-span-fluent-disposable](../../rules/framework/1-observability/axis-telemetry/telemetry-span-fluent-disposable.yaml) |
| `RecordException` adds an `"exception"` event **and** marks the span Error | [telemetry-record-exception](../../rules/framework/1-observability/axis-telemetry/telemetry-record-exception.yaml) |
| `AxisSpanKind` — the five kinds, defaulting to `Internal` | [telemetry-span-kind](../../rules/framework/1-observability/axis-telemetry/telemetry-span-kind.yaml) |
| `AxisSpanStatus` — `Unset` / `Ok` / `Error` and its mapping | [telemetry-span-status](../../rules/framework/1-observability/axis-telemetry/telemetry-span-status.yaml) |

### Adapters & wiring

| Context | Rule |
|---|---|
| The no-op `NullAxisTelemetry` / `NullAxisSpan` (tests, CLIs, migration phase) | [telemetry-null-default-noop](../../rules/framework/1-observability/axis-telemetry/telemetry-null-default-noop.yaml) |
| Gotcha — an unsampled / unlistened span is a silent no-op (null `Activity`) | [telemetry-activity-null-safe](../../rules/framework/1-observability/axis-telemetry/telemetry-activity-null-safe.yaml) |
| The production `OpenTelemetryAdapter` over BCL `ActivitySource` + `Meter` | [telemetry-otel-adapter](../../rules/framework/1-observability/axis-telemetry/telemetry-otel-adapter.yaml) |
| Gotcha — register the `"Axis.AxisMediator"` source/meter or nothing flows | [telemetry-otel-source-name](../../rules/framework/1-observability/axis-telemetry/telemetry-otel-source-name.yaml) |
| Metric instruments are created once and cached (any name, any path) | [telemetry-otel-metric-lazy-cache](../../rules/framework/1-observability/axis-telemetry/telemetry-otel-metric-lazy-cache.yaml) |
| Wiring — `AddOpenTelemetryAxis()` (one singleton, both ports) | [telemetry-di-singleton-both-ports](../../rules/framework/1-observability/axis-telemetry/telemetry-di-singleton-both-ports.yaml) |
| Ship to Azure Monitor / App Insights in one call — `AddAzureMonitorAxis` (connection-string precedence, sampling/cost knobs, log-export filters) | [telemetry-azuremonitor-adapter](../../rules/framework/1-observability/axis-telemetry/telemetry-azuremonitor-adapter.yaml) |
| No connection string → Null sink on both ports + one startup warning; the app boots normally | [telemetry-azuremonitor-disabled-fallback](../../rules/framework/1-observability/axis-telemetry/telemetry-azuremonitor-disabled-fallback.yaml) |

### The pipeline behavior — `TelemetryBehavior`

| Context | Rule |
|---|---|
| Opens `AxisMediator.{RequestName}`, tags Trace/Journey/Identity, writes it to context | [telemetry-behavior-wraps-dispatch](../../rules/framework/1-observability/axis-telemetry/telemetry-behavior-wraps-dispatch.yaml) |
| `RequestType` = `"query"` only for `IAxisQuery` (valued overload), else `"command"` | [telemetry-behavior-request-type](../../rules/framework/1-observability/axis-telemetry/telemetry-behavior-request-type.yaml) |
| Tags `ResultSuccess` + status, and `ErrorCodes` on a returned failure | [telemetry-behavior-result-outcome](../../rules/framework/1-observability/axis-telemetry/telemetry-behavior-result-outcome.yaml) |
| Records `duration_ms` / `invocations` always, `exceptions` only on a throw | [telemetry-behavior-metrics](../../rules/framework/1-observability/axis-telemetry/telemetry-behavior-metrics.yaml) |
| On an exception it records and **rethrows** — never swallows | [telemetry-behavior-rethrows](../../rules/framework/1-observability/axis-telemetry/telemetry-behavior-rethrows.yaml) |

### Tag names

| Context | Rule |
|---|---|
| The constants are a sink-side contract — renaming a value breaks dashboards | [telemetry-tag-names-are-contract](../../rules/framework/1-observability/axis-telemetry/telemetry-tag-names-are-contract.yaml) |
| Naming — framework tags are `axis.*`/`auth.*` snake_case constants; app tags stay local | [telemetry-tag-naming-convention](../../rules/framework/1-observability/axis-telemetry/telemetry-tag-naming-convention.yaml) |

## See also

- `axis-mediator` — the pipeline `TelemetryBehavior` plugs into, and the source of the `TraceId` / `JourneyId` / `AxisEntityId` the span is tagged with.
- `axis-result` — the outcome the behavior reads (`IsSuccess`, `Errors[*].Code`) to tag the span; telemetry itself does not return `AxisResult`.
- `axis-logger` — the sibling observability concern (structured logging); AxisTelemetry depends on AxisLogger.
- `axis-unit-tests` — why the TestHost registers a telemetry sink (`NullAxisTelemetry`): the behavior pipeline resolves `IAxisTelemetry`/`IAxisMetrics` at runtime.
- `axis-dotnet-architect` — the hub; the swappable-infra-port pattern telemetry is one instance of (though telemetry does not return `AxisResult`).
