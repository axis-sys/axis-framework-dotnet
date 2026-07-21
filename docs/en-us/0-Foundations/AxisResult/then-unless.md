# Conditional guard · `ThenUnless`

> Runs the fallible step **only when the predicate is false**. When the predicate is true — the state you want already holds — the success passes through with its value intact, and `next` never runs.

---

## When to use

Skip work that is already done: "publish the product — unless it already is", "close the batch — unless the policy says it is not active". The predicate reads the flowing value and answers "is there anything left to do?".

Note the scope: `ThenUnless` guards **one step**, not the rest of the chain. The success it returns keeps flowing — a `.ThenAsync` after it runs on both branches. To skip a whole sub-chain, compose it **inside** `next`.

## When *not* to use

| You want to…                                          | Use instead             |
|-------------------------------------------------------|-------------------------|
| **fail** when the condition does not hold             | [`Ensure`](ensure.md)   |
| run the step unconditionally                          | [`Then`](then.md)       |
| transform the value conditionally (**same type**)     | [`ThenWhen`](then-when.md) |
| transform conditionally to a **different type**       | [`Then`](then.md) with the branch inside the lambda |
| recover from a failure when a condition matches       | [`RecoverWhen`](recover.md) |

---

## Operators

| Method | Signature | What it does |
|---|---|---|
| `ThenUnless` | `(Func<T,bool> predicate, Func<T,AxisResult> next)` | predicate true → pass-through; false → runs `next`, propagating its errors |
| `ThenUnlessAsync` | `(Func<T,bool> predicate, Func<T,Task<AxisResult>> next)` | same, with an async step |

Defined only on `AxisResult<TValue>` — the predicate reads the flowing value (enter the rail with [`Rop()`](getting-started.md) when you start from a plain value). The predicate is always synchronous: it is cheap routing over a value already in memory; the fallible work lives in `next`. `Async` variants exist for `Task`/`ValueTask` and [with `CancellationToken`](cancellation.md), plus the extension lifts over `Task<AxisResult<T>>`/`ValueTask<AxisResult<T>>`.

---

## Example 1 — idempotent write ("already flagged? do nothing")

```csharp
return productsReader.GetByIdAsync(productId)                 // AxisResult<IProductEntityProperties>
    .ThenUnlessAsync(
        p => p.IsPublished,                                   // already published → nothing to do
        p => productsWriter.PublishAsync(p.ProductId))
    .ThenAsync(p => notifier.NotifyPublishedAsync(p.ProductId)); // runs on BOTH branches
```

**Why it pays off:** the manual ternary — `p.IsPublished ? AxisResult.Ok().AsTaskAsync() : productsWriter.PublishAsync(...)` — disappears, and the "skip if already done" intent becomes a named, declarative step that keeps the value on the rail.

## Example 2 — guard clause at the top of a method

```csharp
// BEFORE: an early return outside the rail
// if (!OrderBatchClosurePolicy.IsActive(batch.StatusId))
//     return AxisResult.Ok().AsTaskAsync();

return batch.Rop()
    .ThenUnlessAsync(
        b => !OrderBatchClosurePolicy.IsActive(b.StatusId),   // not active → nothing to do
        b => CloseOrderBatchAsync(b));
```

**Why it pays off:** the guard `if` that broke out of Railway-Oriented Programming becomes part of the pipeline — same behavior, no imperative exit.

## Skipping a sub-chain

The returned success keeps flowing. When the condition should skip **several** steps, nest them inside `next`:

```csharp
.ThenUnlessAsync(
    p => p.IsPublished,
    p => productsWriter.PublishAsync(p.ProductId)
            .ThenAsync(() => auditWriter.LogPublicationAsync(p.ProductId)))  // whole sub-chain guarded
```

---

## See also

- [Chain · `Then`](then.md) — the unconditional step
- [Conditional step · `ThenWhen`](then-when.md) — the mirror: same-type transform, runs when the predicate is **true**
- [Ensure · `Ensure`](ensure.md) — the inverse guard: condition fails → the rail fails
- [Cancellation](cancellation.md) — the `CancellationToken`-aware overloads

---

↩ [Back to AxisResult docs](README.md)
