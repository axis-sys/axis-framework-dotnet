# Conditional step · `ThenWhen`

> Runs the fallible, **same-type transforming** step **only when the predicate is true**. When the predicate is false — the step simply does not apply — the success passes through with its value intact, and `next` never runs.

---

## When to use

Apply a step that only makes sense for some values, where the step **changes the value** but not its type: "convert the order — when its currency is foreign", "apply the welcome discount — when it is a first purchase". The predicate reads the flowing value and answers "does this step apply here?".

`ThenWhen` earns its place when the condition depends on a value **born mid-pipeline** (produced by an earlier fallible step) — a condition known at method entry reads better as a plain guard before the pipeline starts.

## When *not* to use

| You want to…                                            | Use instead             |
|---------------------------------------------------------|-------------------------|
| **fail** when the condition does not hold               | [`Ensure`](ensure.md)   |
| run the step unconditionally                            | [`Then`](then.md)       |
| run a **valueless side effect** unless already done     | [`ThenUnless`](then-unless.md) |
| transform conditionally to a **different type**         | [`Then`](then.md) with the branch inside the lambda |
| recover from a failure when a condition matches         | [`RecoverWhen`](recover.md) |

---

## Operators

| Method | Signature | What it does |
|---|---|---|
| `ThenWhen` | `(Func<T,bool> predicate, Func<T,AxisResult<T>> next)` | predicate false → pass-through; true → runs `next`, whose result **replaces** the flowing one |
| `ThenWhenAsync` | `(Func<T,bool> predicate, Func<T,Task<AxisResult<T>>> next)` | same, with an async step |

Defined only on `AxisResult<TValue>`, and `next` returns the **same** `TValue` — the pass-through branch returns the current result, so the type cannot change. The predicate is always synchronous: it is cheap routing over a value already in memory; the fallible work lives in `next`. `Async` variants exist for `Task`/`ValueTask` and [with `CancellationToken`](cancellation.md), plus the extension lifts over `Task<AxisResult<T>>`/`ValueTask<AxisResult<T>>`.

### `ThenWhen` vs `ThenUnless` (the mirror pair)

| | `ThenUnless` | `ThenWhen` |
|---|---|---|
| `next` returns | `AxisResult` (valueless side effect) | `AxisResult<T>` (same-type transform) |
| runs `next` when predicate is | **false** ("not done yet") | **true** ("applies here") |
| on `next` success, the value | is **preserved** (original flows on) | is **replaced** (next's result flows on) |

---

## Example 1 — currency conversion only when foreign

The order's currency is only known after loading it — the condition depends on a value born mid-pipeline:

```csharp
return orders.GetByIdAsync(command.OrderId)                    // AxisResult<Order> is born here
    .ThenWhenAsync(
        order => order.Currency != settlement.Currency,        // domestic → nothing to convert
        order => fx.ConvertAsync(order, settlement.Currency))  // fallible; REPLACES the order
    .ThenAsync(order => payments.CaptureAsync(order));         // runs on BOTH branches
```

**Why it pays off:** the ternary inside the lambda — `order.Currency != settlement.Currency ? fx.ConvertAsync(...) : AxisResult.Ok(order).AsTaskAsync()` — disappears, and the "applies here?" intent becomes a named, declarative step.

## Example 2 — conditional enrichment recorded into the value

```csharp
return carts.GetByIdAsync(command.CartId)
    .ThenAsync(cart => pricing.PriceAsync(cart))               // Quote is born here
    .ThenWhenAsync(
        quote => quote.UsesLoyaltyPoints,
        quote => loyalty.ReservePointsAsync(quote.CustomerId, quote.PointsToBurn)
            .MapAsync(reservation => quote with { PointsReservationId = reservation.Id }))
    .ThenAsync(quote => orders.SubmitAsync(quote));
```

**Why it pays off:** the conditional step is fallible *and* writes something new into the flowing value — exactly the combination neither `ThenUnless` (discards `next`'s result) nor `Ensure` (can only fail) expresses.

---

## See also

- [Chain · `Then`](then.md) — the unconditional step
- [Conditional guard · `ThenUnless`](then-unless.md) — the mirror: valueless side effect, runs when the predicate is **false**
- [Ensure · `Ensure`](ensure.md) — condition fails → the rail fails
- [Cancellation](cancellation.md) — the `CancellationToken`-aware overloads

---

↩ [Back to AxisResult docs](README.md)
