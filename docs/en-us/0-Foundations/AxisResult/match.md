# Exit · `Match`

> The end of the rail. `Match` collapses an `AxisResult` into a **final value** — running exactly **one** of the two branches (success or failure).

```csharp
string msg = result.Match(
    onSuccess: value  => $"ok: {value}",
    onFailure: errors => $"failed: {errors[0].Code}");
```

---

## When to use

At the edge of the pipeline, to convert the result into an HTTP response, a message, a DTO — any concrete type.

---

## Operators

| Method / syntax | What it does |
|---|---|
| `Match(onSuccess, onFailure)` | runs **one** branch and returns the final type |
| `MatchAsync` | async version (`Task`/`ValueTask`) |
| `var (ok, value, errors) = result` | positional deconstruction |
| `IsSuccess` / `IsFailure` | boolean inspection |
| `Value` | the value (**throws** if accessed on a failure) |
| `Errors` / `JoinErrorCodes()` | the error list / the concatenated codes |

---

## Real-world example — HTTP edge

The canonical place for `Match` is the controller, translating the rail into a status code:

```csharp
return result.Match(
    onSuccess: value  => Ok(value),
    onFailure: errors => Problem(
        title:  errors[0].Type.ToString(),
        detail: messageResolver.Resolve(errors[0], CultureInfo.CurrentUICulture)));
```

**Why it pays off:** success and failure are handled in the **same place**, with no `try/catch`, and it's impossible to forget one of the branches — both are required.

## Safe deconstruction

```csharp
var (isSuccess, value, errors) = await GetUserAsync(id);
// on a failure, `value` is default(T) — the deconstruction NEVER throws
```

Unlike `.Value` (which throws on a failure), the deconstruction is safe without checking `IsSuccess` first — ideal in *pattern matching*.

---

## See also

- [Errors and types](errors-and-types.md) — the `Type` that becomes an HTTP status
- [Chain · `Then`](then.md) — what comes before `Match`
- [Recover · `Recover`](recover.md) — handle the failure **without** leaving the pipeline

---

↩ [Back to AxisResult docs](README.md)
