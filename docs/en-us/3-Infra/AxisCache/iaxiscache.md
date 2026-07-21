# Contract · `IAxisCache`

> The single interface every adapter implements. Five methods, every one async, every one returning an `AxisResult`. The semantics are simple on purpose — there is no `Refresh`, no batch, no tags.

```csharp
public interface IAxisCache
{
    Task<AxisResult<T?>>   GetAsync<T>(string key);
    Task<AxisResult>       SetAsync<T>(string key, T value, TimeSpan? expiration = null);
    Task<AxisResult<T>>    GetOrCreateAsync<T>(string key, Func<Task<AxisResult<T>>> factory, TimeSpan? expiration = null);
    Task<AxisResult>       RemoveAsync(string key);
    Task<AxisResult<bool>> ExistsAsync(string key);
}
```

---

## When to use

Anywhere your application reads a value that is **expensive to compute** and **acceptable to return from a memoised snapshot**. Person profiles, configuration, lookups, aggregates that change rarely. Cache keys are plain strings — choose a convention (`"<entity>:<id>"`) and stick to it.

## When *not* to use

| You want to… | Use instead |
|---|---|
| share **session state** between web nodes | a dedicated session store |
| store values that **must survive a crash**, or be shared across instances | the bundled [SQL adapter](sql-adapter.md) (Postgres/MySQL) — or a real database |
| coordinate cross-process locks | a distributed lock primitive (Redis `SET NX EX`, etc.) |
| invalidate by **tag** or **pattern** | extend the interface in your adapter, or use a vendor SDK directly behind it |

---

## The five operations

| Method | Success means | Returns `IsFailure` when |
|---|---|---|
| `GetAsync<T>(key)` | the call completed; `Value` may be `null` (miss) | the adapter threw |
| `SetAsync<T>(key, value, expiration?)` | the value was stored (overwriting any existing entry) | the adapter threw |
| `GetOrCreateAsync<T>(key, factory, expiration?)` | the value came from cache **or** the factory ran and stored its `Value` | the adapter threw, the operation was cancelled, **or the factory itself returned a failure** (its `Value` is never cached) |
| `RemoveAsync(key)` | the key is no longer in the cache (a removal of a missing key is still success) | the adapter threw |
| `ExistsAsync(key)` | the call completed; `Value` tells you whether the key was there | the adapter threw |

Implicit cancellation: every method in the bundled `MemoryCacheAdapter` calls `_cancellationToken.ThrowIfCancellationRequested()` before touching the store (the token comes from `mediatorAccessor.AxisMediator?.CancellationToken`). The four `TryAsync`-wrapped methods — `GetAsync`, `SetAsync`, `RemoveAsync`, `ExistsAsync` — **rethrow** the resulting `OperationCanceledException`, because `AxisResult.TryAsync` treats cancellation as critical rather than converting it to a failed `AxisResult`; a cancelled call there *throws*. Only `GetOrCreateAsync`, which wraps its body in its own `try/catch (Exception)`, catches it and surfaces cancellation as a failed `AxisResult`.

---

## Real-world examples

### 1. Cache-aside in a query handler

```csharp
public Task<AxisResult<GetPersonResponse>> HandleAsync(GetPersonQuery query)
    => cache.GetOrCreateAsync(
            key:        $"person:{query.PersonId}",
            factory:    () => readerPort.GetByIdAsync(query.PersonId),
            expiration: TimeSpan.FromMinutes(10))
        .MapAsync(p => new GetPersonResponse { PersonId = p.PersonId, DisplayName = p.DisplayName });
```

**Why it pays off:** one line decides whether the answer comes from cache or repository, and the failure rail covers both paths — `NotFound` from the repository turns into a cache miss that is *not* cached.

### 2. Write-through invalidation in a command handler

```csharp
public Task<AxisResult<UpdatePersonResponse>> HandleAsync(UpdatePersonCommand cmd)
    => factory.GetByIdAsync(cmd.PersonId)
        .ThenAsync(person => person.UpdateDisplayNameAsync(cmd.DisplayName))
        .ThenAsync(_ => unitOfWork.SaveChangesAsync())
        .ThenAsync(_ => cache.RemoveAsync($"person:{cmd.PersonId}"))   // invalidate
        .MapAsync(_ => new UpdatePersonResponse { PersonId = cmd.PersonId });
```

**Why it pays off:** the cache is invalidated **only after** the write has committed. If `SaveChangesAsync` fails, the railway short-circuits and the cache stays warm with the (still correct) old value.

### 3. Existence check before an expensive operation

```csharp
public async Task<AxisResult> RebuildIfStaleAsync(AxisEntityId id)
{
    var exists = await cache.ExistsAsync($"projection:{id}");
    if (exists.IsSuccess && exists.Value)
        return AxisResult.Ok();

    return await rebuilder.RebuildAsync(id)
        .ThenAsync(value => cache.SetAsync($"projection:{id}", value, TimeSpan.FromHours(1)));
}
```

**Why it pays off:** the check is cheap and explicit. The expensive `RebuildAsync` is only paid when the projection isn't already warm.

---

## See also

- [Get-or-create pattern](get-or-create.md) — the headline operator in depth
- [`AxisMemoryCache` adapter](memory-adapter.md) — the in-box single-process implementation
- [SQL adapter](sql-adapter.md) — the in-box implementation that survives a restart and is shared across instances
- [Custom adapter](custom-adapter.md) — implement `IAxisCache` for your storage
- [API reference](api-reference.md) — every method, in one place

---

↩ [Back to AxisCache docs](README.md)
