# API reference

> The complete operator catalog, grouped by responsibility. Use it for lookup — each group has a detail page with examples.

---

## Creating results

| Method | Description |
|--------|-------------|
| `AxisResult.Ok()` | success with no value |
| `AxisResult.Ok(value)` | success with a value |
| `AxisResult.Error(error)` | failure from a single error |
| `AxisResult.Error(errors)` | failure from multiple errors |
| `AxisResult.Try(action)` | wrap an action that might throw |
| `AxisResult.Try(func)` | wrap a function that might throw |
| `AxisResult.TryAsync(action)` | async version of `Try` |
| `AxisResult.TryAsync(func)` | async version of `Try` with return value |
| `AxisResult.TryBind(func)` | like `Try`, but the func returns an `AxisResult` — caught and **flattened** (not nested) |
| `AxisResult.TryBindAsync(func)` | async version of `TryBind` |
| Implicit: `value` | assign any value where `AxisResult<T>` is expected |
| Implicit: `AxisError` | assign an error where `AxisResult` is expected |

→ [Getting started](getting-started.md) · [Exceptions at the boundary · `Try`](boundary-and-try.md)

---

## Transforming (success rail)

| Method | Signature | Description |
|--------|-----------|-------------|
| `Map` | `T -> TNew` | transform the value (pure, cannot fail) |
| `MapAsync` | `T -> Task<TNew>` | async transform |
| `Then` | `T -> AxisResult<TNew>` | chain to a failable operation returning a new value |
| `Then` | `T -> AxisResult` | chain to a failable side effect, **preserves original value** |
| `ThenAsync` | async versions of both `Then` overloads | |
| `ToAxisResult` | `T -> AxisResult` | chain to a failable side effect, returns an `AxisResult` with no value |
| `ToAxisResultAsync` | async version of `ToAxisResult` | |

→ [Transform · `Map`](map.md) · [Chain · `Then`](then.md)

---

## Side effects

| Method | Description |
|--------|-------------|
| `Tap(action)` | run side effect on success, return original result |
| `TapAsync(func)` | async side effect on success |
| `TapError(action)` | run side effect on failure (logging, metrics) |
| `TapErrorAsync(func)` | async side effect on failure |

→ [Side effects · `Tap`](tap.md)

---

## Validation

| Method | Description |
|--------|-------------|
| `Ensure(predicate, error)` | guard clause — fails if predicate is false |
| `Ensure(func)` | delegated validation — `func` returns `AxisResult` |
| `EnsureAsync` | async versions |
| `ThenUnless(predicate, next)` | success guard — runs the fallible `next` only when the predicate is false; passes through otherwise |
| `ThenUnlessAsync` | async versions |
| `ThenWhen(predicate, next)` | conditional step — runs the same-type transforming `next` only when the predicate is true; passes through otherwise |
| `ThenWhenAsync` | async versions |

→ [Ensure · `Ensure`](ensure.md) · [Conditional guard · `ThenUnless`](then-unless.md) · [Conditional step · `ThenWhen`](then-when.md)

---

## Failable side effects

| Method | Description |
|--------|-------------|
| `ActionAsync(func)` | run a failable operation (`T -> ValueTask<AxisResult>`) and **preserve the original value** on success. Available on `ValueTask<AxisResult<T>>` only. Unlike `ThenAsync`, which replaces the value, `ActionAsync` keeps it — ideal for domain validation, persistence, or any step where you need the value downstream |

→ [Chain · `Then`](then.md)

---

## Combining values

| Method | Description |
|--------|-------------|
| `Zip(func)` | combine current value with a new one into a tuple |
| `ZipAsync(func)` | async version |
| Chained: `.Zip().Zip()` | build tuples up to `(T1, T2, T3, T4)` |
| `MapAsync((a, b) => ...)` | destructure tuples in the mapper |
| `ThenAsync((a, b) => ...)` | failable step over the tuple, spread parameters — valueless form preserves the tuple, value form replaces it (2-, 3- and 4-tuples) |

→ [Combine · `Zip`](zip.md)

---

## Aggregation

| Method | Description |
|--------|-------------|
| `Combine(results...)` | join N results — collects **all** errors |
| `CombineAsync(tasks)` | async version — **parallel** (`Task`) or sequential (`ValueTask`) |
| `All(results)` | join N typed results into `IReadOnlyList<T>` |
| `AllAsync(tasks)` | async version — **parallel** (`Task`) or sequential (`ValueTask`) |
| `AllAsync(items, operation)` | sequential: one call at a time, typed result per item |
| `CombineAsync(items, operation)` | sequential: one call at a time, no typed value |
| `ZipParallelAsync(() => other)` | run an independent op concurrently, zip into tuple, accumulate errors if both fail |
| `ZipParallelAsync(ct => other, ct)` | CT-aware variant |

→ [Aggregate · `Combine`/`All`](aggregate.md) · [Combine · `Zip`](zip.md)

---

## Recovery and fallbacks

| Method | Description |
|--------|-------------|
| `Recover(value)` | on failure, replace with a default |
| `Recover(func)` | on failure, compute a fallback |
| `RecoverWhen(predicate, func)` | recover only if errors match a condition |
| `RecoverWhen(AxisErrorType, func)` | recover only for a specific error type |
| `RecoverWhen(code, func)` | recover only for a specific error code |
| `RecoverNotFound(func)` | recover only if all errors are `NotFound` |
| `RecoverConflict(func)` | recover only if all errors are `Conflict` (value or fallible result recovery) |
| `OrElse(fallback)` | try an alternative operation |
| `OrElse(fallback, combineErrors: true)` | alternative with error accumulation |

→ [Recover · `Recover`](recover.md)

---

## Type-changing NotFound convergence

Not part of the `Recover` family — it also runs on success (see [Beyond Recover · `ElseNotFound`](recover.md#beyond-recover--elsenotfound)):

| Method | Description |
|--------|-------------|
| `ElseNotFound<TNew>(map, recovery)` | converge into a **new type**: `map` on success, `recovery` only if all errors are `NotFound`; any other failure propagates |

→ [Beyond Recover · `ElseNotFound`](recover.md#beyond-recover--elsenotfound)

---

## Existence guards

| Method | Description |
|--------|-------------|
| `RequireNotFound(error)` | found → error, `NotFound` → `Ok`, other errors → propagate |
| `RequireNotFoundAsync(error)` | async version |
| `WithValueAsync(value)` | promote `AxisResult` to `AxisResult<T>` with a value |

→ [Ensure · `Ensure`](ensure.md)

---

## Error transformation

| Method | Description |
|--------|-------------|
| `MapError(func)` | transform each error individually |
| `MapError(func<list>)` | transform/filter the entire error list |
| `MapErrorAsync` | async versions |

→ [Remap errors · `MapError`](map-errors.md)

---

## Terminal

| Method | Description |
|--------|-------------|
| `Match(onSuccess, onFailure)` | convert to a final type — runs exactly one branch |
| `MatchAsync` | async version |

→ [Exit · `Match`](match.md)

---

## LINQ

| Syntax | Equivalent |
|--------|------------|
| `from x in result select f(x)` | `result.Map(f)` |
| `from x in r1 from y in r2 select ...` | `r1.Then(x => r2).Map(...)` |
| `SelectManyAsync` | async LINQ chaining |

→ [LINQ query syntax](linq-query-syntax.md)

---

## Conversion

| Method | Description |
|--------|-------------|
| `AsTaskAsync()` | wrap a sync result in `Task` |
| `AsValueTaskAsync()` | wrap a sync result in `ValueTask` |

→ [`Task` vs `ValueTask`](async-task-vs-valuetask.md)

---

## Cancellation

Every core async operator has a CT-aware overload whose delegate receives the token as a second parameter. Available on both `Task<AxisResult<T>>` and `ValueTask<AxisResult<T>>`:

| Method | Delegate shape |
|--------|----------------|
| `ThenAsync` | `(T, CancellationToken) => Task<AxisResult<TNew>>` |
| `ThenAsync` | `(T, CancellationToken) => Task<AxisResult>` (preserves value) |
| `ToAxisResultAsync` | `(T, CancellationToken) => Task<AxisResult>` |
| `MapAsync` | `(T, CancellationToken) => Task<TNew>` |
| `TapAsync` | `(T, CancellationToken) => Task` |
| `EnsureAsync` | `(T, CancellationToken) => Task<bool>` |
| `EnsureAsync` | `(T, CancellationToken) => Task<AxisResult>` |
| `ThenUnlessAsync` | `(T, CancellationToken) => Task<AxisResult>` (predicate stays synchronous; preserves value) |
| `ThenWhenAsync` | `(T, CancellationToken) => Task<AxisResult<T>>` (predicate stays synchronous; replaces value when it runs) |
| `ZipAsync` | `(T, CancellationToken) => Task<TNew>` |
| `ZipAsync` | `(T, CancellationToken) => Task<AxisResult<TNew>>` |
| `ActionAsync` | `(T, CancellationToken) => ValueTask<AxisResult>` (preserves value, `ValueTask` only) |
| `ZipParallelAsync` | `(CancellationToken) => Task<AxisResult<TNew>>` |

→ [Cancellation](cancellation.md)

---

## Deconstruction

| Syntax | Yields |
|--------|--------|
| `var (isSuccess, errors) = result` | `AxisResult` |
| `var (isSuccess, value, errors) = result` | `AxisResult<T>` (`value` is `default` on failure) |

→ [Ergonomics](ergonomics.md)

---

## See also

- [Getting started](getting-started.md) — how to create and inspect results
- [Chain · `Then`](then.md) — the central railway operator
- [Full documentation](README.md) — the map of the whole documentation

---

↩ [Back to AxisResult docs](README.md)
