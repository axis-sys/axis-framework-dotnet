# Categories and structured properties

> The `T` in `IAxisLogger<T>` is the **category** for `Microsoft.Extensions.Logging` — the same role as in `ILogger<T>`. It tags every entry with the source type, so filters and sinks can route by class. Plus it is what `LogResult` reads to populate `RequestName`.

```csharp
public class CreatePersonHandler(IAxisLogger<CreatePersonHandler> logger)
{
    // logger writes entries under category "MyApp.People.CreatePersonHandler"
    // logger.LogResult populates RequestName="MyApp.People.CreatePersonHandler"
}
```

---

## When to use

Always inject `IAxisLogger<MyClass>`. Pick the **most specific** class as `T` — the handler, the behaviour, the adapter. Avoid a generic `IAxisLogger<object>` or shared base type — you lose the per-component routing.

## When *not* to use

| You want to… | Use instead |
|---|---|
| share a logger between two unrelated types | inject each one separately with its own `T` |
| log from a static method | accept `IAxisLogger<TSomething>` as a parameter |
| log from infra **outside** the mediator scope | `ILogger<T>` directly (no `TraceId` enrichment to be had) |

---

## The two roles `T` plays

| Role | Used by | What it produces |
|---|---|---|
| `Microsoft.Extensions.Logging` category | every call (`LogInformation`, `LogResult`, …) | the `Category` field on the log entry — e.g. `"MyApp.People.CreatePersonHandler"` |
| `LogResult`'s `RequestName` | `LogResult(tag, result)` | structured property `"RequestName" = typeof(T).FullName` |

The category drives **filters and sinks**: configure `appsettings.json` to suppress `Debug` from one namespace, or route `Error` from another to PagerDuty. The `RequestName` drives **query and aggregation**: count failures per handler, alert on a specific request, etc.

---

## Always-on properties

In addition to your `(Key, Value)` pairs, every entry gets:

| Property | Source | Description |
|---|---|---|
| `UtcTime` | `TimeProvider.GetUtcNow().ToString("yyyy-MM-dd HH:mm:ss.fff zzz")` | timestamp formatted for log search |
| `OriginId` | `IAxisMediator.OriginId` | the upstream system / channel that started the journey |
| `TraceId` | `IAxisMediator.TraceId` | the per-request correlation id |
| `JourneyId` | `IAxisMediator.JourneyId` | the saga or long-running journey id (if any) |

Your properties **overwrite** these if they share a key — be careful with naming so you do not stomp the defaults.

---

## Real-world example — per-handler routing

```csharp
public class CreatePersonHandler(IAxisLogger<CreatePersonHandler> logger) { /* … */ }
public class CreateOrderHandler (IAxisLogger<CreateOrderHandler>  logger) { /* … */ }
```

```jsonc
// appsettings.json — route via the category
{
  "Logging": {
    "LogLevel": {
      "MyApp.People.CreatePersonHandler": "Information",
      "MyApp.Orders.CreateOrderHandler":  "Debug"
    }
  }
}
```

**Why it pays off:** noisy debug-level entries from one handler do not flood the others. Promotion rules, alerting and sampling can be per-handler.

---

## See also

- [The `IAxisLogger<T>` contract](iaxislogger.md) — what the category is used for
- [`LogResult`](log-result.md) — why `T` shows up as `RequestName`
- [`LoggingBehavior`](logging-behavior.md) — uses `IAxisLogger<TRequest>` so the category is the request type

---

↩ [Back to AxisLogger docs](README.md)
