# Side effects · `Tap`

> Runs a side effect (log, metric, event) and **returns the result untouched**. `Tap` changes neither the rail nor the value — it only observes.

---

## When to use

Write a log, emit a metric, fire a *fire-and-forget* event — without affecting what flows through the pipeline.

## When *not* to use

| You want to… | Use instead |
|---|---|
| an effect that **can fail** and must short-circuit | [`Then`](then.md) |
| transform the value | [`Map`](map.md) |

---

## Operators

| Method | Runs when | Receives |
|---|---|---|
| `Tap` / `TapAsync` | **success** | the value |
| `TapError` / `TapErrorAsync` | **failure** | the list of `AxisError` |

All return the original result and exist in `Task`/`ValueTask` and [with `CancellationToken`](cancellation.md).

---

## Real-world example — observability on both rails

```csharp
return CreateOrderAsync(cmd)
    .TapAsync(order  => logger.LogInformation("Order {OrderId} created", order.OrderId)) // only on success
    .TapErrorAsync(errors => metrics.IncrementFailure(errors[0].Code));                   // only on failure
```

**Why it pays off:** the success log and the failure metric live in the same pipeline, each on its own rail, **without** an `if (result.IsSuccess)` to separate them — and `order` flows forward untouched.

## `Tap` vs `Then` (the difference that matters)

- `Tap` **ignores** the effect's return value and never fails the rail — perfect for logging.
- `Then` **propagates** the effect's failure — use it when the step genuinely needs to short-circuit.

```csharp
.TapAsync(o => auditLog.WriteAsync(o))    // if the log fails, the pipeline CONTINUES
.ThenAsync(o => uow.SaveChangesAsync())   // if the save fails, the pipeline STOPS
```

---

## See also

- [Chain · `Then`](then.md) — when the effect needs to be able to fail
- [Remap errors · `MapError`](map-errors.md) — transform the errors that `TapError` observes
- [Exit · `Match`](match.md) — the failure branch at the end of the rail

---

↩ [Back to AxisResult docs](README.md)
