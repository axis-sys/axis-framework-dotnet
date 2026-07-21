# Why AxisCache? · comparison

> There are other ways to cache in .NET. This page tells you why AxisCache is different — a direct comparison, no hand-waving.

---

## vs. `IMemoryCache` (directly)

`IMemoryCache` is the workhorse and AxisCache uses it internally. Calling it **directly** from application code has three problems:

1. It throws — every site needs `try/catch` or accepts crashes.
2. There is no `GetOrCreate` that returns a typed failure — the `IMemoryCache.GetOrCreate` overload happily caches whatever the factory returns, including null or a partial value.
3. It binds your handlers to `Microsoft.Extensions.Caching.Memory`. Swap to Redis later and every call site changes.

**AxisCache** returns `AxisResult`, makes the factory railway-aware, and lets you swap adapters without touching application code.

## vs. `IDistributedCache`

Same trade-offs as above, plus a flatter API — `IDistributedCache` only deals in `byte[]`, so every caller has to serialise/deserialise by hand. Adapter-side serialisation is the right place to put that concern.

## vs. `FusionCache` / `EasyCaching`

Both are excellent and feature-rich (multi-level, jitter, fail-safe, stampede protection). They are also **bigger** and **more opinionated** about how caching should work. If you need their advanced features, use them. If you want a **small, focused, Axis-shaped** interface that returns `AxisResult` and lives next to `AxisResult`/`AxisLogger`/`AxisMediator`, use `AxisCache`. (Nothing stops you from implementing `IAxisCache` on top of `FusionCache` — that is exactly what custom adapters are for.)

## vs. a bespoke `ICacheService<T>`

The DIY abstraction. Same idea as `IAxisCache`, but you write the contract, the in-memory adapter and the tests yourself. `IAxisCache` saves the cost — and standardises the failure modes across every package in Axis.

---

## The comparison

| Feature | AxisCache | `IMemoryCache` direct | `IDistributedCache` direct | FusionCache | Bespoke |
|---|:--:|:--:|:--:|:--:|:--:|
| Returns `AxisResult` | **Yes** | No | No | No | Maybe |
| `GetOrCreate` does not cache failures | **Yes** | No | n/a | Yes | Maybe |
| Implicit cancellation via `AxisMediator` | **Yes** | No | No | No | Maybe |
| Swap memory ↔ distributed without app changes | **Yes** | No | No | Yes | Maybe |
| Tiny surface, no learning curve | **Yes** | Yes | Yes | No | Yes |
| Bundled in-memory adapter | **Yes** | n/a | n/a | Yes | No |
| Bundled persistent/shared adapter (SQL-backed) | **Yes** | No | No | No | No |
| Zero NuGet deps beyond `Microsoft.Extensions.Caching.*` (memory adapter) | **Yes** | Yes | Yes | No | Yes |

---

## See also

- [The `IAxisCache` contract](iaxiscache.md) — the five methods
- [Get-or-create pattern](get-or-create.md) — the headline operator
- [SQL adapter](sql-adapter.md) — the bundled persistent/shared adapter
- [API reference](api-reference.md) — every method, in one place

---

↩ [Back to AxisCache docs](README.md)
