# Spans · `IAxisSpan`

> The span you actually work with. Fluent — every mutator returns `this` so you can chain. Implements `IDisposable` — wrap with `using var` and the span ends automatically.

```csharp
public interface IAxisSpan : IDisposable
{
    string TraceId { get; }
    string SpanId { get; }

    IAxisSpan SetTag(string key, object? value);
    IAxisSpan SetStatus(AxisSpanStatus status, string? description = null);
    IAxisSpan RecordException(Exception exception);
    IAxisSpan AddEvent(string name, params KeyValuePair<string, object?>[] attributes);
}
```

---

## When to use

Wherever a unit of work deserves its own row in the trace UI: a database call, a downstream HTTP, an in-process step that may take time, an integration boundary. Open one span per operation; do not nest unless the work is genuinely nested.

## When *not* to use

| You want to… | Use instead |
|---|---|
| count something | [`IAxisMetrics.IncrementCounter`](contracts.md) |
| record a duration distribution | [`IAxisMetrics.RecordHistogram`](contracts.md) |
| log a structured message | [`AxisLogger`](../AxisLogger/README.md) |

---

## The four mutators

| Method | What it does | Returns |
|---|---|---|
| `SetTag(key, value)` | adds a structured tag to the span | `this` (fluent) |
| `SetStatus(status, description?)` | marks the span `Ok` / `Error` / `Unset` | `this` (fluent) |
| `RecordException(ex)` | adds an `"exception"` event with `type`/`message`/`stacktrace`, then `SetStatus(Error, ex.Message)` | `this` (fluent) |
| `AddEvent(name, attrs...)` | adds a point-in-time event inside the span | `this` (fluent) |

Reading `ActivityAxisSpan` directly:

- `SetTag` calls `activity?.SetTag(key, value)`.
- `SetStatus` calls `activity?.SetStatus(MapStatus(status), description)`.
- `RecordException` adds an `ActivityEvent("exception", …)` and calls `SetStatus(Error, ex.Message)`.
- `AddEvent` adds an `ActivityEvent(name, tags)`.

The null-activity case is a no-op — the same code works when `ActivitySource` returns `null` because nothing is listening.

---

## The disposal pattern

```csharp
public Task<AxisResult> CommitAsync()
{
    using var span = telemetry.StartSpan("db.postgres.commit", AxisSpanKind.Client);
    span.SetTag("db.system", "postgresql");

    try { /* work */ span.SetStatus(AxisSpanStatus.Ok); }
    catch (Exception ex) { span.RecordException(ex); throw; }

    return AxisResult.Ok();
}
```

`using var` ensures the span ends when the method exits — success path, exception path, early return, all covered.

---

## Real-world examples

### 1. Tagging a database call

```csharp
public async Task<AxisResult> StartAsync()
{
    using var span = telemetry.StartSpan("db.postgres.connect", AxisSpanKind.Client);
    span.SetTag("db.system", "postgresql");

    try
    {
        _connection ??= await dataSource.OpenConnectionAsync(ct);
        _transaction = await _connection.BeginTransactionAsync(ct);
        span.SetStatus(AxisSpanStatus.Ok);
        return AxisResult.Ok();
    }
    catch (Exception ex)
    {
        span.RecordException(ex);
        return AxisError.InternalServerError("POSTGRES_ERROR_STARTING_CONNECTION");
    }
}
```

**Why it pays off:** the bundled `PostgresUnitOfWork` already does exactly this. Every connection / commit / rollback gets a span tagged `db.system = postgresql`, with status + exception captured.

### 2. Fluent chaining

```csharp
using var span = telemetry.StartSpan("orders.import_batch")
    .SetTag("batch.size", batch.Count)
    .SetTag("batch.source", source);

await ProcessBatchAsync(batch);
span.SetStatus(AxisSpanStatus.Ok);
```

**Why it pays off:** the fluent style keeps all the tagging next to the `StartSpan` — no later "did I forget to tag the batch size?" hunt.

### 3. Event inside a span

```csharp
using var span = telemetry.StartSpan("orders.fulfill");

span.AddEvent("payment.captured",
    new("amount",  order.Amount),
    new("gateway", "stripe"));

span.AddEvent("inventory.reserved",
    new("warehouse", warehouse));

span.SetStatus(AxisSpanStatus.Ok);
```

**Why it pays off:** the events show up as point-in-time markers inside the span, with their own attributes — a mini-timeline of the operation, in one trace row.

---

## See also

- [The contracts](contracts.md) — what gives you the span
- [`TelemetryBehavior`](telemetry-behavior.md) — the behaviour that opens spans automatically
- [Tag names](tag-names.md) — canonical constants for common tags

---

↩ [Back to AxisTelemetry docs](README.md)
