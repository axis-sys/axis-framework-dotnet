# Cancellation · `CancellationToken`

> Every async operator has a **`CancellationToken`-aware** overload: the delegate receives the token as its last parameter and the operator forwards it onward — the token flows through the pipeline without polluting *closures*.

---

## When to use

When you need to pass a `CancellationToken` explicitly through the whole chain (cancellable requests, *timeouts*).

---

## How it works

The overload hands the token to your delegate and forwards it:

```csharp
public Task<AxisResult<CreateOrderResponse>> HandleAsync(CreateOrderCommand cmd, CancellationToken ct)
    => customerFactory.GetByIdAsync(cmd.CustomerId, ct)
        .ThenAsync((customer, ct) => orderFactory.CreateAsync(new()
        {
            CustomerId = customer.CustomerId,
            ProductId  = cmd.ProductId,
            Quantity   = cmd.Quantity
        }, ct), ct)
        .ThenAsync((order, ct) => unitOfWork.SaveChangesAsync(ct), ct)
        .MapAsync((order, ct) => Task.FromResult(new CreateOrderResponse { OrderId = order.Id }), ct);
```

## Operators with a CT variant

`ThenAsync` · `MapAsync` · `TapAsync` · `EnsureAsync` · `ZipAsync` · `ZipParallelAsync` — on `Task<AxisResult<T>>` **and** `ValueTask<AxisResult<T>>`.

`ToAxisResultAsync` — **`Task<AxisResult<T>>` only** (no `ValueTask` variant).

The overloads **without** CT still exist: you can close over a token via a lambda, or mix both styles in the same chain.

## Preferred pattern (DI)

In apps with dependency injection, register the request's `CancellationToken` as a *scoped* service (resolved from `IHttpContextAccessor` or an ambient context) — that way handlers don't have to thread the token through every method. The CT-aware overloads exist for cases where explicit *threading* is preferred, or when the ambient pattern isn't available.

---

## See also

- [`Task` vs `ValueTask`](async-task-vs-valuetask.md) — the two async families that get CT variants
- [Chain · `Then`](then.md) — the most common operator to receive the token

---

↩ [Back to AxisResult docs](README.md)
