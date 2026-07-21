# Chain · `Then`

> **The heart of the railway.** `Then` chains a step that **can fail**. If it fails, every following step is skipped — never again an `if (result.IsFailure) return;`.

On the **success rail**, `Then` runs the next operation with the current value. On the **failure rail**, it does nothing and lets the existing errors flow through untouched.

```csharp
var result = await GetUserAsync(id)            // AxisResult<User>
    .ThenAsync(user => ValidateAsync(user))    // only runs if GetUser succeeded
    .ThenAsync(user => SaveAsync(user));        // only runs if Validate succeeded
```

---

## When to use

Use `Then` when the next step **returns an `AxisResult`** (it can fail) and you want that failure to **short-circuit** the rest of the pipeline.

## When *not* to use

| You want to… | Use instead |
|---|---|
| transform the value with something that **cannot fail** (build a DTO) | [`Map`](map.md) |
| **observe** the value (log, metric) without changing the rail | [`Tap`](tap.md) |
| keep **both** values, the old one and the new one | [`Zip`](zip.md) |
| run the step **only when a condition on the value is false** (skip if already done) | [`ThenUnless`](then-unless.md) |
| run a **same-type transforming** step only when a condition on the value is true | [`ThenWhen`](then-when.md) |

---

## The four forms (the part that confuses the most)

The behavior depends on **what the delegate returns**, not on the method name:

| Form                                 | Delegate returns         | What flows forward                   | Use when                                                            |
|--------------------------------------|--------------------------|--------------------------------------|---------------------------------------------------------------------|
| `Then` / `ThenAsync`                 | `AxisResult<TNew>`       | the **new** value                    | the step produces a new value                                       |
| `Then` / `ThenAsync`                 | `AxisResult` (no value)  | the **original** value               | a failable step with no return (persist, invalidate cache)          |
| `ToAxisResult` / `ToAxisResultAsync` | `AxisResult`             | **nothing** (narrows to non-generic) | the typed value is no longer needed (e.g. a command with no response) |

> **Rule of thumb:** if the next step *returns* a value → it **replaces**. If it returns a plain `AxisResult` → the value **survives**.

---

## Available overloads

Every form exists for the synchronous `AxisResult<T>`, for `Task<AxisResult<T>>` and `ValueTask<AxisResult<T>>` pipelines. Each one also has a [`CancellationToken`-aware](cancellation.md) variant, where the delegate receives the token as its last parameter.

```csharp
// replaces the value
AxisResult<TNew>  Then(Func<T, AxisResult<TNew>> next)
Task<AxisResult<TNew>>  ThenAsync(Func<T, Task<AxisResult<TNew>>> next)

// preserves the value (the delegate returns an AxisResult with no value)
AxisResult<T>  Then(Func<T, AxisResult> next)
Task<AxisResult<T>>  ThenAsync(Func<T, Task<AxisResult>> next)

// discards the value entirely
AxisResult  ToAxisResult(Func<T, AxisResult> next)
AxisResult  ToAxisResult()                       // just narrows AxisResult<T> → AxisResult
```

### Tuple tracks: spread overloads

When the track carries a tuple (after a [`Zip`](zip.md)), `ThenAsync` also exists in **spread** form — the delegate receives the tuple elements as **separate, nameable parameters**, so you never touch `.Value1` / `.Value2`. Available for 2-, 3- and 4-tuples on `Task` pipelines:

```csharp
// valueless spread: runs the step, PRESERVES the whole tuple
Task<AxisResult<(T1, T2)>>  ThenAsync(Func<T1, T2, AxisResult> next)
Task<AxisResult<(T1, T2)>>  ThenAsync(Func<T1, T2, Task<AxisResult>> next)

// value spread: the step produces TNew, which replaces the tuple
Task<AxisResult<TNew>>  ThenAsync(Func<T1, T2, AxisResult<TNew>> next)
Task<AxisResult<TNew>>  ThenAsync(Func<T1, T2, Task<AxisResult<TNew>>> next)
```

```csharp
await LoadUserAsync(id)                                     // AxisResult<User>
    .ZipAsync(user => IssueTokenAsync(user))                // (user, token)
    .ThenAsync((user, token) => store.SaveAsync(token))     // side effect; (user, token) survives
    .MapAsync((user, token) => new Response(user.Name, token.Value));
```

> The spread convention is uniform across the tuple surface: `ZipAsync((a, b) => …)` extends, `ThenAsync((a, b) => …)` runs a failable step, `MapAsync((a, b) => …)` collapses.

---

## Real-world examples

### 1. Command pipeline: regenerate an API secret

Load the entity, change it, persist, invalidate the cache, return the response — five failable steps, zero `try/catch`. If **any** step fails, everything after it (including `SaveChanges`) is skipped.

```csharp
public Task<AxisResult<GenerateNewSecretResponse>> HandleAsync(GenerateNewSecretCommand cmd)
{
    var plain  = ExternalApiSecret.Generate();
    var hashed = ExternalApiSecret.Hash(plain);

    return factory.GetByIdAsync(cmd.ExternalApiId)  // AxisResult<ExternalApi> → NotFound if it doesn't exist
        .ThenAsync(api => api.UpdateSecretAsync(hashed))  // AxisResult → preserves the api
        .ThenAsync(_ => uow.SaveChangesAsync())  // AxisResult → preserves the api
        .ThenAsync(_ => cacheResolver.RemoveAsync(cmd.ExternalApiId))  // → preserves the api
        .MapAsync(_ => new GenerateNewSecretResponse { ExternalApiId = cmd.ExternalApiId, Secret = plain });
}
```

**Why it pays off:** the three middle steps return an `AxisResult` with no value, so the `App` keeps flowing — the final `MapAsync` still has everything it needs, and an `UpdateSecret` that fails never reaches `SaveChanges`.

### 2. Create if it doesn't exist: preserve to validate, then persist

```csharp
public Task<AxisResult<IPersonAggregateApplication>> CreateAsync(NewArgs args)
    => readerPort.GetByDocumentAsync(args.Document)
        .RequireNotFoundAsync(AxisError.ValidationRule("DOCUMENT_ALREADY_EXISTS")) // found → fail
        .WithValueAsync(new PersonEntity(args.Document, args.DisplayName)) // AxisResult → AxisResult<Entity>
        .MapAsync(NewInstance) // Entity → New Instance of Entity Application (cannot fail → Map, not Then)
        .ThenAsync(app => app.IsValidAsync()) // validates; PRESERVES the app
        .ThenAsync(writePort.CreateAsync); // persists, but doesn't save (may be saved by a unit of work later)
```

**Why it pays off:** `ThenAsync` vs `MapAsync` — validation can fail (so it's on the railway and preserves the value - ThenAsync), while mapping the entity does not fail (it's a plain `Map`). If there's an exception in the mapping, it's a programming error and should be propagated as such.

### 3. No response? Discard the value with `ToAxisResult`

A command with no payload narrows the typed pipeline back to a plain `AxisResult`:

```csharp
public Task<AxisResult> HandleAsync(DeleteExternalApiCommand cmd)
    => factory.GetByIdAsync(cmd.ExternalApiId) // AxisResult<ExternalApiApplication>
        .ToAxisResultAsync(app => app.DeleteAsync());  // the operation finishes and the app is discarded, returning an AxisResult
```

**Why it pays off:** the handler signature is `Task<AxisResult>` (no response), and `ToAxisResultAsync` makes the pipeline end exactly at that type — no fake value, no `Map(_ => Unit)`.

### 4. Checkout: the product survives the stock reservation

A valueless `ThenAsync` (reserving stock) runs its step but flows the **original product** forward, so the final `MapAsync` still has it. This snippet is extracted verbatim from the compiled, unit-tested [`Scaffolds` sample](../../../../src/scaffolds/Core/Scaffolds.ECommerce.Application/Catalog/UseCases/Checkout/v1/CheckoutHandler.cs) — it cannot drift from the real API.

<!-- scaffold:checkout -->

```csharp
public Task<AxisResult<CheckoutResponse>> HandleAsync(CheckoutCommand command)
{
    return products
        .GetByIdAsync(command.ProductId)
        .EnsureAsync(product => product.Stock >= command.Quantity, AxisError.BusinessRule(CatalogErrors.InsufficientStock))
        .ThenAsync(product => products.ReserveStockAsync(product.ProductId, command.Quantity))
        // Publish before SaveChangesAsync: the atomic outbox drains the enqueued event in the very same
        // transaction (architecture-events-published-in-unit-of-work). The cart consumer picks this up
        // out of the band and associates the reserved product with the cart (architecture-bus-events).
        .ThenAsync(product => bus.PublishAsync(
                new ProductCheckedOutEvent(
                    command.CartId,
                    product.ProductId.ToString(),
                    command.Quantity
                ), ProductCheckedOutEvent.Topic))
        .ThenAsync(_ => unitOfWork.SaveChangesAsync())
        .MapAsync(product => new CheckoutResponse
        {
            Customer = mediator.AxisEntityId!.Value,
            ProductId = product.ProductId,
        });
}
```

<!-- /scaffold -->

**Why it pays off:** `EnsureAsync` guards the invariant, the valueless `ThenAsync` reserves stock while preserving the product, and `MapAsync` (which cannot fail) builds the response — one railway, zero `if`/`try`.

---

## See also

- [`Map`](map.md) — transform a value that cannot fail
- [`Ensure`](ensure.md) — guarantee an invariant inline (`RequireNotFound`, `WithValueAsync`)
- [`Zip`](zip.md) — keep the old value *and* a new one
- [Errors and types](errors-and-types.md) — what an `AxisError` carries and the 12 categories
- [`Task` vs `ValueTask`](async-task-vs-valuetask.md) — which async form to chain

---

↩ [Back to AxisResult docs](README.md)
