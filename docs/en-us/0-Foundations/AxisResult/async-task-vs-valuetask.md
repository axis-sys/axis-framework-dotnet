# `Task` vs `ValueTask` · which async to use

> Every async method in AxisResult has two variants: `Task` and `ValueTask`. **When in doubt, use `Task`.** This page tells you exactly when `ValueTask` pays off — because not every dev has this at the tip of their tongue.

---

## The short rule

- **Default: `Task`.** It's safe: it can be awaited (`await`) more than once, combined in `Task.WhenAll`, and stored.
- **`ValueTask`** only when the method **frequently completes synchronously** (e.g. *cache hit*, value already in memory) **and** is on a *hot path* proven by *profiling*. The gain: **zero allocation** on the synchronous path.

## When *not* to use `ValueTask`

A misused `ValueTask` is **worse** than a `Task`. Don't use it if you're going to:

| Situation | Why |
|---|---|
| await (`await`) the same value **more than once** | a `ValueTask` can only be consumed once |
| **store** it in a field/property to use later | same reason — it wasn't made to be kept |
| pass it to **`Task.WhenAll` / `WhenAny`** | it requires `.AsTask()` first (and then you lose the gain) |
| you **haven't measured** that the path is hot | premature optimization; start with `Task` |

---

## Why the lib offers both

AxisResult exposes `ThenAsync`, `MapAsync`, `TapAsync`, etc. for **both** — and the pipeline is **identical**:

```csharp
// Task (default)
public Task<AxisResult<User>> GetUserAsync(UserId id) => ...;

// ValueTask (hot path: zero allocation when it completes synchronously)
public ValueTask<AxisResult<User>> GetUserAsync(UserId id) => ...;
```

Every composition (`ThenAsync`, `MapAsync`, `TapAsync`…) works the same in both. You start with `Task` and migrate only the hot spots to `ValueTask` **without rewriting** the pipeline.

**Why it pays off:** in a cache resolver that hits 95% of the time synchronously, `ValueTask` eliminates the `Task` allocation on almost every call — without changing a line of the composition logic above.

> Reference: the zero-allocation behavior of `ValueTask<T>` on synchronous completions is documented by the .NET team — see Stephen Toub, [*Understanding the Whys, Whats, and Whens of ValueTask*](https://devblogs.microsoft.com/dotnet/understanding-the-whys-whats-and-whens-of-valuetask/).

---

## See also

- [Chain · `Then`](then.md) — the pipeline that gets the async variants
- [Cancellation](cancellation.md) — the variants with `CancellationToken`

---

↩ [Back to AxisResult docs](README.md)
