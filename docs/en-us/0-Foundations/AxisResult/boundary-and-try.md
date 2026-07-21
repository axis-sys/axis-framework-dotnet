# Exceptions at the boundary · `Try` and adapters

> "Exceptions at the boundary, results everywhere else." This page shows how to convert exception-throwing infrastructure surfaces (database, HTTP, brokers) into `AxisResult` at a single point — and what `Try` does **not** catch.

---

## When to use

At any point where external code throws exceptions: database drivers, `HttpClient`, message brokers, file I/O. Above that boundary, everything is exception-free.

## When *not* to use

| You want to… | Use instead |
|---|---|
| chain steps that already return `AxisResult` | [`Then`](then.md) |
| recover from a transient failure | [`Recover`](recover.md) |

---

## Real-world example — database access without exceptions

```csharp
protected async Task<AxisResult<T>> GetAsync<T>(
    string sql,
    Action<NpgsqlParameterCollection> addParams,
    Func<NpgsqlDataReader, T> map,
    string notFoundCode)
{
    try
    {
        await using var command = await uow.NewCommandAsync(sql);
        addParams(command.Parameters);
        await using var reader = await command.ExecuteReaderAsync(CancellationToken);
        if (!await reader.ReadAsync(CancellationToken))
            return AxisError.NotFound(notFoundCode);
        return AxisResult.Ok(map(reader));
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "POSTGRES_GET_ERROR");
        return AxisError.InternalServerError("POSTGRES_GET_ERROR");
    }
}
```

**Why it pays off:** the only `try/catch` in the entire architecture lives at the infrastructure boundary. Database exceptions are converted to `AxisResult` once, at the edge. Everything above — handlers, factories, domain rules — is exception-free.

---

## Boundary adapter pattern

For `HttpClient`, database drivers, message brokers and any other infrastructure with a leaky exception surface, the recommended pattern is to write a thin **adapter** whose job is *exclusively* converting the external surface into `AxisResult<Response>`. Every exception the underlying client can throw is mapped to a typed `AxisError` (timeouts → `Timeout`, 5xx → `ServiceUnavailable`, 401 → `Unauthorized`, etc.). The rest of the application consumes only `AxisResult<Response>` and never sees a raw `HttpResponseMessage`. Dedicated helper libraries for the HTTP and repository adapters are on the roadmap.

---

## Flatten a result-returning boundary · `TryBind`

`Try` wraps a function that returns a **plain value**. When the boundary operation *both* throws (network, deserialization) *and* reports its own controlled failures (a non-success HTTP status mapped to an `AxisError`), the callback naturally returns an `AxisResult<T>` — and `Try` would nest it into `AxisResult<AxisResult<T>>`. Use **`TryBind` / `TryBindAsync`** instead: it catches the exceptions *and* flattens the returned result.

```csharp
private Task<AxisResult<List<Role>>> GetRolesAsync(string path)
    => AxisResult.TryBindAsync(
        async () =>
        {
            using var response = await httpClient.GetAsync(path);          // can throw (network/timeout)
            if (!response.IsSuccessStatusCode)                             // controlled failure — returned, not thrown
                return AxisError.ServiceUnavailable("V1_UNAVAILABLE");
            var roles = await response.Content.ReadFromJsonAsync<List<Role>>();  // can throw (malformed JSON)
            return AxisResult.Ok(roles ?? []);
        },
        ex => AxisError.ServiceUnavailable("V1_UNAVAILABLE"));             // both exceptions mapped here
```

A controlled `AxisError` returned by the callback passes straight through; an exception thrown by it is caught and mapped (same `IsCritical` rethrow rules as `Try`). Without `TryBind` you would fall back to a throw-for-control trick (`response.EnsureSuccessStatusCode()`) or a hand-written `try/catch`.

> **Task-only by design.** There is no `ValueTask` overload of `Try` / `TryAsync` / `TryBindAsync`. A static factory receives its callback as a lambda, and `async () => …` is ambiguous between `Func<Task…>` and `Func<ValueTask…>` (CS0121) — a `ValueTask` sibling would break every `…(async () => …)` caller. This is an intentional limit of factory-style helpers, not an oversight.

---

## Note on `Try` / `TryBind`

`AxisResult.Try` and `AxisResult.TryBind` do **not** catch "programmer error" exceptions — `NullReferenceException`, `ArgumentNullException`, `OperationCanceledException`, `OutOfMemoryException`, `StackOverflowException` and `ThreadAbortException` are rethrown. These represent bugs or genuinely unrecoverable situations and should not be silently turned into a result value. If you want a specific exception type captured, pass an `errorHandler` override or catch it manually in your adapter.

---

## See also

- [Chain · `Then`](then.md) — what consumes the `AxisResult` the boundary produces
- [Errors and types](errors-and-types.md) — the types to map each exception to
- [Remap errors · `MapError`](map-errors.md) — translate codes when crossing layers

---

↩ [Back to AxisResult docs](README.md)
