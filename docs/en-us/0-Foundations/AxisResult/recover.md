# Recover · `Recover`

> The opposite of [`Then`](then.md): it operates on the **failure rail**. `Recover` and its relatives bring the pipeline back to success — always in a **deliberate and explicit** way.

---

## When to use

Provide a default when something wasn't found, fall back to an alternative source when a service is down, try a second path.

## When *not* to use

| You want to… | Use instead |
|---|---|
| just **observe** the error (log) without recovering | [`TapError`](tap.md) |
| **rewrite** the error, not recover | [`MapError`](map-errors.md) |

---

## Operators

| Method | Recovers when… |
|---|---|
| `Recover(value)` / `Recover(func)` | **any** failure → default value |
| `RecoverWhen(AxisErrorType, func)` | the errors are of a **type** |
| `RecoverWhen(code, func)` | the errors have a **code** |
| `RecoverWhen(predicate, func)` | a **predicate** over the errors |
| `RecoverNotFound(func)` | **all** errors are `NotFound` |
| `RecoverConflict(func)` | **all** errors are `Conflict` |
| `OrElse(fallback)` | tries an alternative **operation** |
| `OrElse(fallback, combineErrors: true)` | alternative, **accumulating** the errors from both sides |

All have `Async` variants (`Task`/`ValueTask`).

---

## Example 1 — default when it doesn't exist

```csharp
var settings = await GetUserSettingsAsync(userId)
    .RecoverNotFoundAsync(() => DefaultSettings.Create());
```

**Why it pays off:** "no settings saved? use the default" stops being a `catch (NotFoundException)` and becomes an explicit intent — and it only recovers from `NotFound`, not from real errors (e.g. a database failure).

## Example 2 — read-mostly ensure: `RecoverNotFound` outside, `RecoverConflict` inside

```csharp
public Task<AxisResult<Customer>> EnsureAsync(string externalId)
    => customers.GetByExternalIdAsync(externalId)      // the common case: it already exists — one read
        .RecoverNotFoundAsync(() => CreateAsync(externalId));

private Task<AxisResult<Customer>> CreateAsync(string externalId)
    => BuildCustomer(externalId).Rop()
        .ThenAsync(customers.CreateAsync)
        .ThenAsync(_ => unitOfWork.SaveChangesAsync())
        .RecoverConflictAsync(() => customers.GetByExternalIdAsync(externalId)); // lost the race -> fetch the winner
```

**Why it pays off:** the hot path of a get-or-create is the *get*, so it stays a single read with no
failed insert. Creation is the rare branch — and only there the check-then-act window exists, which
`RecoverConflict` closes: the loser of a concurrent first-sight race re-reads the row the winner
committed. Both recoveries are fallible (`Func<Task<AxisResult<T>>>`), and any non-NotFound /
non-Conflict failure still surfaces.

> **Get-or-create vs. guard — two duals of "create when absent."** `RecoverConflict` / `RecoverNotFound`
> above are **idempotent**: a duplicate is absorbed and the existing value returns as **success**. The
> dual is the **guard** — read first and let a duplicate become a **conflict failure** — see
> [`RequireNotFound`](ensure.md). Choose by intent. The scaffold shows both on the same operation —
> `RegisterProduct` (recover) alongside `CreateProduct` (guard).

## Example 3 — conditional fallback by type

```csharp
var data = await FetchFromPrimaryAsync()
    .RecoverWhenAsync(AxisErrorType.ServiceUnavailable, () => FetchFromFallbackAsync());
```

**Why it pays off:** it only falls back to the secondary when the primary is **unavailable** (transient); a validation error, for instance, keeps failing as it should.

## Example 4 — alternative path with error accumulation

```csharp
var user = await FindByEmailAsync(email)
    .OrElseAsync(_ => FindByPhoneAsync(phone), combineErrors: true);
// if BOTH fail, you get the errors from both attempts
```

---

## Beyond Recover · `ElseNotFound`

`ElseNotFound<TNew>` looks like a relative of `RecoverNotFound` but **is not part of this family** —
every operator above returns the source unchanged when it is already successful. `ElseNotFound` can't
make that promise: it exists specifically to converge a *found* value and a *NotFound* default into a
**new type**, so it has to run on both rails.

```csharp
var response = await factory.GetByIdAsync(externalApiId)          // AxisResult<ExternalApi>
    .ElseNotFoundAsync(
        api => BuildResponse(api),                                // found → map to the response type
        () => ExternalApiResponse.Empty());                        // NotFound → a default of that SAME new type
// any other failure (e.g. a database error) still propagates as AxisResult<ExternalApiResponse>
```

**Why it pays off:** without it, the same outcome needs two calls —
`.MapAsync(BuildResponse).RecoverNotFoundAsync(() => ExternalApiResponse.Empty())` — which is still the
right choice whenever either branch can itself fail (swap in `ThenAsync<TNew>` for the found branch, or
`RecoverNotFound(Func<AxisResult<TNew>>)` for the recovery branch). Reach for `ElseNotFound` only for
the plain, non-failing map-on-both-sides shape; reach for `RecoverNotFound(func)` (no type parameter)
when the recovered value stays the **same** type as the source.

---

## See also

- [Errors and types](errors-and-types.md) — `IsTransient`, types and codes to condition the recovery
- [Remap errors · `MapError`](map-errors.md) — transform the error instead of recovering
- [Ensure · `Ensure`](ensure.md) — the opposite: take from success to failure

---

↩ [Back to AxisResult docs](README.md)
