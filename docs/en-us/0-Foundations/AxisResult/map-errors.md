# Remap errors · `MapError`

> Rewrites the errors on the failure rail — useful for **translating** codes and types when a result crosses a layer or context boundary.

---

## When to use

An internal service failed with internal codes and you want to expose them with your layer's codes/types, without leaking low-level details.

## When *not* to use

| You want to… | Use instead |
|---|---|
| **recover** from the failure (return to success) | [`Recover`](recover.md) |
| just **observe** the error (log) | [`TapError`](tap.md) |

---

## Operators

| Method | Transforms |
|---|---|
| `MapError(Func<AxisError, AxisError>)` | **each** error, individually |
| `MapError(Func<IReadOnlyList<AxisError>, IEnumerable<AxisError>>)` | the **whole list** (filter/restructure) |
| `MapErrorAsync` | async versions (`Task`/`ValueTask`) |

---

## Real-world example — crossing a layer boundary

```csharp
var result = await internalService.ProcessAsync()
    .MapErrorAsync(errors => errors.Select(e =>
        AxisError.InternalServerError($"PROCESSING_{e.Code}")));
```

**Why it pays off:** the service's internal codes become stable codes of your API (`PROCESSING_*`) at a single point — the boundary. Nothing low-level leaks, and the rest of the pipeline never needs to know the internal details.

---

## See also

- [Errors and types](errors-and-types.md) — what an `AxisError` carries
- [Recover · `Recover`](recover.md) — handle the failure instead of just rewriting it
- [Exit · `Match`](match.md) — where the errors become the final response

---

↩ [Back to AxisResult docs](README.md)
