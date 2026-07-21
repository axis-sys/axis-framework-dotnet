# Ergonomics · deconstruction and debugger

> Small quality-of-life details: deconstruct results with no `try/catch` and see the state directly in the debugger, without expanding private fields.

---

## Deconstruction

Both `AxisResult` and `AxisResult<T>` support positional deconstruction:

```csharp
var (isSuccess, errors) = AxisResult.Ok();                 // non-generic
var (isSuccess, value, errors) = AxisResult.Ok(42);        // generic

// Also usable in positional patterns:
if (result is (true, var v, _))
    Console.WriteLine($"got {v}");
```

On a failed `AxisResult<T>`, `value` is `default(T)` (not an exception) — the deconstruction **never throws**, so it's safe to use in pattern matching without checking `IsSuccess` first.

---

## Debugger experience

`AxisResult`, `AxisResult<T>` and `AxisError` all carry `[DebuggerDisplay]` attributes. In the debugger you see:

- `Ok` / `Ok(42)` for success
- `Error[2]: USER_NOT_FOUND, INVALID_EMAIL` for failures
- `NotFound USER_NOT_FOUND` for individual errors

**Why it pays off:** no more inspecting private fields or expanding every node — the relevant state shows up directly in the default view.

---

## See also

- [Exit · `Match`](match.md) — the other way to extract the value safely
- [Errors and types](errors-and-types.md) — what an `AxisError`'s `[DebuggerDisplay]` shows
- [API reference](api-reference.md) — the deconstruction table

---

↩ [Back to AxisResult docs](README.md)
