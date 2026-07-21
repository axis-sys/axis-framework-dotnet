# AxisLogger — Documentation

> 🌐 [Português (documentação navegável)](../../../pt-br/1-Observability/AxisLogger/README.md)

**Request-scoped structured logging for C#** — `IAxisLogger<T>` wraps `Microsoft.Extensions.Logging`'s `ILogger<T>` and **auto-enriches** every entry with the ambient `OriginId`, `TraceId` and `JourneyId` pulled from the current `IAxisMediator`. Plus `LogResult(tag, AxisResult)` for one-line structured logging of every railway outcome, and an opt-in `LoggingBehavior` that logs each mediator request automatically.

```csharp
public class CreatePersonHandler(IAxisLogger<CreatePersonHandler> logger, ...)
{
    public Task<AxisResult<CreatePersonResponse>> HandleAsync(CreatePersonCommand cmd)
    {
        logger.LogInformation("Creating person", ("Document", cmd.Document));

        return factory.CreateAsync(cmd)
            .ThenAsync(person => uow.SaveChangesAsync())
            .TapAsync(r => logger.LogResult("CreatePerson", r))   // one line, structured outcome
            .MapAsync(_ => new CreatePersonResponse { PersonId = cmd.PersonId });
    }
}
```

Use this page as a **map**: read the trunk below (~5 min) and jump straight to the detail of the group you need — without reading hundreds of lines.

---

## The trunk (read first)

### The interface in 60 seconds

```csharp
public interface IAxisLogger<in T>
{
    void LogDebug(string message, params (string Key, object? Value)[] properties);
    void LogInformation(string message, params (string Key, object? Value)[] properties);
    void LogWarning(string message, params (string Key, object? Value)[] properties);
    void LogError(string message, params (string Key, object? Value)[] properties);
    void LogError(Exception exception, string message, params (string Key, object? Value)[] properties);
    void LogCritical(string message, params (string Key, object? Value)[] properties);
    void LogResult(string tag, AxisResult result);
}
```

Six familiar levels, every overload takes `params (string Key, object? Value)[] properties` for **structured** log enrichment. Every entry is wrapped in an `ILogger.BeginScope(...)` carrying `UtcTime`, `OriginId`, `TraceId`, `JourneyId` and your properties. → **[The `IAxisLogger<T>` contract](iaxislogger.md)**

### Why a generic? — `IAxisLogger<T>`

The `T` is the **category** for `Microsoft.Extensions.Logging` (same role as `ILogger<T>`). It tags the log entry's source so filters and sinks can route by class. → **[Categories and structured properties](categories.md)**

### `LogResult` — the railway companion

```csharp
logger.LogResult("CreatePerson", result);
```

`Success` → `Information` + `Tag`/`RequestName`. `Failure` → `Error` + the same plus `AxisErrorList`. One call, one structured entry, the right level. → **[`LogResult` — structured outcomes](log-result.md)**

### `LoggingBehavior` — log every request automatically

Opt-in mediator behaviour that logs `Handling {RequestName}` at the top of every handler:

```csharp
services
    .AddAxisLogger()
    .AddLoggingBehavior();    // pipeline behaviour over IAxisMediator
```

→ **[`LoggingBehavior` — automatic request logging](logging-behavior.md)**

### Installation

```
dotnet add package AxisLogger
```

`AxisLogger` depends directly on `AxisMediator.Contracts` (for the ambient identifiers); `AxisResult` (used by `LogResult`) comes transitively through it.

→ Full guide: **[Getting started](getting-started.md)**

---

## The map (jump to what you need)

| Group | You want to… | Detail |
|---|---|---|
| **Contract · `IAxisLogger<T>`** ⭐ | log structured entries with auto-enrichment | [iaxislogger.md](iaxislogger.md) |
| **`LogResult`** | log an `AxisResult` outcome in one line | [log-result.md](log-result.md) |
| **`LoggingBehavior`** | log every mediator request automatically | [logging-behavior.md](logging-behavior.md) |
| **Categories · `IAxisLogger<T>`** | how the `T` flows into structured props | [categories.md](categories.md) |
| **Why?** | the case against `ILogger<T>` directly | [why-axislogger.md](why-axislogger.md) |
| **Reference** | every member at a glance | [api-reference.md](api-reference.md) |

**Start here:** [Getting started](getting-started.md) · [The `IAxisLogger<T>` contract](iaxislogger.md) · [Why AxisLogger?](why-axislogger.md)

**Fundamentals:** [`LogResult` — structured outcomes](log-result.md) · [`LoggingBehavior` — automatic request logging](logging-behavior.md) · [Categories and structured properties](categories.md)

**Reference & extras:** [API reference](api-reference.md)

---

## Design principles

1. **Structured first.** Every public overload takes `(Key, Value)` pairs — never an interpolated string with values baked in. Search and aggregation rely on it.
2. **Auto-enrichment is non-negotiable.** `OriginId`, `TraceId`, `JourneyId` always travel with the entry. If you forget to pass them, the logger does not.
3. **`LogResult` is the railway's logger.** A typed outcome deserves a typed log entry, with the right level chosen for you.
4. **The behaviour is opt-in.** `LoggingBehavior` wires structured logging into the mediator pipeline with one line; turn it off when you do not need it.
5. **Sinks stay free.** AxisLogger does not pick the sink — `ILogger<T>` does. Use Serilog, NLog, OpenTelemetry-Logs, anything that plugs into `Microsoft.Extensions.Logging`.

---

## License

Apache 2.0
