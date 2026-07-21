# `LogResult` — structured outcomes

> Log an `AxisResult` outcome in one line. The level is picked for you — `Information` on success, `Error` on failure — and the entry carries `Tag`, `RequestName` and (on failure) the full `AxisErrorList` as structured properties.

```csharp
logger.LogResult("CreatePerson", result);
// Information "CreatePerson Handled CreatePersonHandler" + Tag/RequestName       (on success)
// Error       "CreatePerson Handled CreatePersonHandler" + Tag/RequestName/...   (on failure)
```

---

## When to use

At the **end of a pipeline** to record what happened. Pair it with `TapAsync` so the railway keeps flowing:

```csharp
return factory.CreateAsync(cmd)
    .ThenAsync(person => uow.SaveChangesAsync())
    .TapAsync(r => logger.LogResult("CreatePerson", r))
    .MapAsync(_ => new CreatePersonResponse { … });
```

## When *not* to use

| You want to… | Use instead |
|---|---|
| log the **start** of a request automatically | [`LoggingBehavior`](logging-behavior.md) |
| log a non-`AxisResult` value | `LogInformation` / `LogError` |
| log the value the railway carried | `Tap` with a manual `LogInformation` |

---

## What ends up in the entry

Reading `AxisLogger<T>.LogResult` directly:

| Field | Success | Failure |
|---|---|---|
| `LogLevel` | `Information` | `Error` |
| Message | `$"{tag} Handled {typeof(T).Name}"` | same |
| Property `Tag` | the `tag` you passed | same |
| Property `RequestName` | `typeof(T).FullName` | same |
| Property `AxisErrorList` | — | `result.Errors` (the full list as a structured array) |

Plus the always-on enrichment from `BuildScope`: `UtcTime`, `OriginId`, `TraceId`, `JourneyId`.

> The `T` in `IAxisLogger<T>` is what determines `RequestName` — that is why you inject `IAxisLogger<CreatePersonHandler>` (or your handler / behaviour / adapter type) instead of a non-generic logger.

---

## Real-world examples

### 1. End of a command pipeline

```csharp
public Task<AxisResult<CreatePersonResponse>> HandleAsync(CreatePersonCommand cmd)
    => factory.CreateAsync(cmd)
        .ThenAsync(person => uow.SaveChangesAsync())
        .TapAsync(r => logger.LogResult("CreatePerson", r))
        .MapAsync(_ => new CreatePersonResponse { PersonId = cmd.PersonId });
```

**Why it pays off:** the success entry includes `Tag="CreatePerson"` and `RequestName="CreatePersonHandler"`; if the pipeline fails, the same entry comes out at `Error` with the full `AxisErrorList` attached. The query "how many CreatePerson commands failed in the last hour" becomes a sink-side aggregation.

### 2. Per-step outcome inside a saga

```csharp
return await reserveStock.ExecuteAsync(cmd)
    .TapAsync(r => logger.LogResult("ReserveStock", r))
    .ThenAsync(_ => chargeCard.ExecuteAsync(cmd))
    .TapAsync(r => logger.LogResult("ChargeCard",  r))
    .ThenAsync(_ => createShipment.ExecuteAsync(cmd))
    .TapAsync(r => logger.LogResult("CreateShipment", r));
```

**Why it pays off:** every step produces a single structured row; the trace timeline reads like the saga's storyboard, and a failure at any step lights up at `Error` with its `AxisErrorList`.

### 3. Logging a query that does not actually fail

```csharp
return await reader.GetByIdAsync(id)
    .TapAsync(r => logger.LogResult("GetPerson", r))
    .MapAsync(person => new GetPersonResponse { … });
```

**Why it pays off:** even a `NotFound` is **not** an exception — it is an error on the rail. `LogResult` records it at `Error` with the typed error list, which is exactly the right severity for "we asked for an id that does not exist".

---

## See also

- [The `IAxisLogger<T>` contract](iaxislogger.md) — every overload
- [`LoggingBehavior`](logging-behavior.md) — automatic request logging
- [Categories](categories.md) — why `T` matters

---

↩ [Back to AxisLogger docs](README.md)
