# Referência da API

> O catálogo completo, agrupado por responsabilidade. Use para consulta — cada grupo linka de volta à sua página de detalhe.

---

## Contratos

| Tipo | Membros | Descrição |
|---|---|---|
| `IAxisTelemetry` | `IAxisSpan StartSpan(string, AxisSpanKind = Internal)`, `string? CurrentTraceId`, `string? CurrentSpanId` | porta de tracing |
| `IAxisMetrics` | `void RecordHistogram(string, double, params KeyValuePair<string, object?>[])`, `void IncrementCounter(string, long delta = 1, params KeyValuePair<string, object?>[])` | porta de métricas |

→ [Os contratos](contracts.md)

---

## Span — `IAxisSpan`

| Membro | Assinatura | Descrição |
|---|---|---|
| `TraceId` | `string` | o trace id W3C |
| `SpanId` | `string` | o span id W3C |
| `SetTag` | `IAxisSpan SetTag(string key, object? value)` | adiciona uma tag estruturada |
| `SetStatus` | `IAxisSpan SetStatus(AxisSpanStatus status, string? description = null)` | define o status |
| `RecordException` | `IAxisSpan RecordException(Exception)` | adiciona um evento `"exception"` + `SetStatus(Error, ex.Message)` |
| `AddEvent` | `IAxisSpan AddEvent(string name, params KeyValuePair<string, object?>[] attributes)` | adiciona um evento pontual |
| `Dispose` | `void Dispose()` | encerra o span |

→ [Spans · `IAxisSpan`](spans.md)

---

## Enums

| Enum | Valores |
|---|---|
| `AxisSpanKind` | `Internal`, `Server`, `Client`, `Producer`, `Consumer` |
| `AxisSpanStatus` | `Unset`, `Ok`, `Error` |

---

## Adapters

| Tipo | Implementa | Descrição |
|---|---|---|
| `OpenTelemetryAdapter` | `IAxisTelemetry`, `IAxisMetrics` | embrulha `ActivitySource("Axis.AxisMediator")` e `Meter("Axis.AxisMediator")` |
| `OpenTelemetryAdapter.SourceName` | `const string` = `"Axis.AxisMediator"` | o nome compartilhado de source/meter |
| `NullAxisTelemetry` | `IAxisTelemetry`, `IAxisMetrics` | no-op; `NullAxisTelemetry.Instance` |
| `NullAxisSpan` | `IAxisSpan` | span no-op; `NullAxisSpan.Instance` |
| `ActivityAxisSpan` (interno) | `IAxisSpan` | a implementação backed por `Activity` |
| `AzureMonitorDisabledWarning` (interno, `AxisTelemetry.AzureMonitor`) | `IHostedService` | warning de startup quando nenhuma connection string é encontrada e a telemetria degradou para `NullAxisTelemetry` |

A package `AxisTelemetry.AzureMonitor` não adiciona nenhum tipo novo de adapter — ela pareia o `OpenTelemetryAdapter` com a distro oficial do Azure Monitor (caindo para `NullAxisTelemetry` sem connection string).

→ [Adapter OpenTelemetry](opentelemetry-adapter.md) · [Adapter null](null-adapter.md) · [Adapter Azure Monitor](azure-monitor.md)

---

## Pipeline behaviour — `TelemetryBehavior`

| Tipo | Onde se senta | O que faz |
|---|---|---|
| `TelemetryBehavior<TRequest>` | requests sem response tipada | abre um span `AxisMediator.{TRequest.Name}`; tags `TraceId`/`JourneyId`/`RequestType`/`AxisEntityId`/`RequestName`; grava `axis.handler.duration_ms`, `axis.handler.invocations`, `axis.handler.exceptions` |
| `TelemetryBehavior<TRequest, TResponse>` | requests com response tipada | mesmo, mais `RequestType = request is IAxisQuery ? "query" : "command"` |

→ [`TelemetryBehavior`](telemetry-behavior.md)

---

## Constantes de tag-name

### `TelemetryTagNames`

`AxisEntityId` · `TraceId` · `JourneyId` · `RequestType` · `RequestName` · `ResultSuccess` · `ErrorCodes` · `ExceptionType`

### `AuthTelemetryTagNames`

`Scheme` · `Result` · `FailureReason` · `ApiId` · `BruteForceSuspected`

→ [Tag names](tag-names.md)

---

## Métricas gravadas (pelo `TelemetryBehavior`)

| Métrica | Tipo | Tags |
|---|---|---|
| `axis.handler.duration_ms` | histogram | `RequestName`, `ResultSuccess` |
| `axis.handler.invocations` | counter | `RequestName`, `ResultSuccess` |
| `axis.handler.exceptions` | counter | `RequestName`, `ExceptionType` |

→ [`TelemetryBehavior`](telemetry-behavior.md)

---

## Extensão DI

| Extensão | Efeito |
|---|---|
| `services.AddOpenTelemetryAxis()` | registra `OpenTelemetryAdapter` (singleton) e amarra tanto `IAxisTelemetry` quanto `IAxisMetrics` nela |
| `services.AddAzureMonitorAxis(IConfiguration, Action<AzureMonitorAxisOptions>? = null)` (`AxisTelemetry.AzureMonitor`) | com connection string: `AddOpenTelemetryAxis()` + `AddOpenTelemetry().UseAzureMonitor(...)` assinando `"Axis.AxisMediator"`; sem: `NullAxisTelemetry` nas duas portas + warning de startup — nunca lança |

---

## Options — `AzureMonitorAxisOptions` (`AxisTelemetry.AzureMonitor`)

| Propriedade | Tipo | Default | Efeito |
|---|---|---|---|
| `ConnectionString` | `string?` | `null` | override programático de `APPLICATIONINSIGHTS_CONNECTION_STRING` / `ConnectionStrings:ApplicationInsights` |
| `SamplingRatio` | `float` | `1.0f` | fração de traces exportados (0.0–1.0); ignorada quando `TracesPerSecond` está setado |
| `TracesPerSecond` | `double?` | `null` | teto de traces a taxa fixa em vez de fração; quando setado, `SamplingRatio` é ignorada |
| `EnableLiveMetrics` | `bool` | `true` | o stream de Live Metrics (gratuito, mantém um canal aberto) |
| `ServiceName` | `string?` | `null` | resource attribute `service.name` (cloud role name no portal) |
| `ServiceVersion` | `string?` | `null` | resource attribute `service.version`; ignorado quando `ServiceName` não está setado |
| `ResourceAttributes` | `IDictionary<string, object>` | vazio | attributes extras carimbados em cada span, métrica e registro de log |
| `EnableLogExport` | `bool` | `true` | `false` → nenhuma entrada de `ILogger` chega ao Azure Monitor (traces/métricas continuam fluindo) |
| `MinimumLogLevel` | `LogLevel` | `Information` | piso das entradas exportadas — só o pipeline de export, providers locais intactos |
| `CategoryLogLevels` | `IDictionary<string, LogLevel>` | vazio | overrides por categoria do `MinimumLogLevel` para o pipeline de export |
| `IncludeScopes` | `bool` | `false` | scopes de log nas entradas exportadas — mais contexto, mais bytes |
| `IncludeFormattedMessage` | `bool` | `false` | mensagem renderizada além do template — mais legível, mais bytes |

→ [Adapter Azure Monitor](azure-monitor.md)

---

## Veja também

- [Primeiros passos](getting-started.md) — instale, registre, trace
- [Por que AxisTelemetry?](why-axistelemetry.md) — o argumento pela abstração
- [Documentação completa](README.md) — o mapa de toda a documentação

---

↩ [Voltar à documentação do AxisTelemetry](README.md)
