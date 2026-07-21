# Errors and types · `AxisError`

> An error in AxisResult is a **value**, not an exception. Each `AxisError` carries only two things: a stable **`Code`** and a **`Type`** (category).

```csharp
AxisError err = AxisError.NotFound("USER_NOT_FOUND");
// err.Code  → "USER_NOT_FOUND"            (stable key: logs, metrics, retry)
// err.Type  → AxisErrorType.NotFound
```

---

## The 12 categories

Every `AxisError` has an `AxisErrorType`. They map naturally to HTTP status codes — which makes translation at the API edge trivial:

| Factory | Type | HTTP |
|---|---|:--:|
| `AxisError.ValidationRule(code)` | `ValidationRule` | 400 |
| `AxisError.Unauthorized(code)` | `Unauthorized` | 401 |
| `AxisError.Forbidden(code)` | `Forbidden` | 403 |
| `AxisError.NotFound(code)` | `NotFound` | 404 |
| `AxisError.Conflict(code)` | `Conflict` | 409 |
| `AxisError.BusinessRule(code)` | `BusinessRule` | 422 |
| `AxisError.TooManyRequests(code)` | `TooManyRequests` | 429 |
| `AxisError.InternalServerError(code)` | `InternalServerError` | 500 |
| `AxisError.Mapping(code)` | `Mapping` | 500 |
| `AxisError.ServiceUnavailable(code)` | `ServiceUnavailable` | 503 |
| `AxisError.Timeout(code)` | `Timeout` | 504 |
| `AxisError.GatewayTimeout(code)` | `GatewayTimeout` | 504 |

---

## Why is there no `Message` field?

A deliberate decision: `AxisError` carries **only** `Code` + `Type`.

- **The `Code` is the primary key.** Stable across versions, so that logs, metrics, alerts and retry policies can pivot on it without *parsing* strings.
- **A message is a different responsibility.** Localization, tone, and the decision of whether (or not) to expose an internal detail to the end user don't belong in a value that travels through the pipeline.

The recommended pattern is a **`code → message` resolver at the presentation edge** (controller, gRPC interceptor), with the texts in resource files:

```csharp
return result.Match(
    onSuccess: value  => Ok(value),
    onFailure: errors => Problem(
        title:  errors[0].Type.ToString(),
        detail: messageResolver.Resolve(errors[0], CultureInfo.CurrentUICulture)));
```

This way: small, canonical codes (`USER_NOT_FOUND`); multiple UIs (REST, gRPC, CLI) render the same code in different ways; tests assert on **codes**, not on English prose; no personal data leaks into the error payload.

> Need to pass **details** (id, attempted quantity)? Emit **multiple `AxisError`** — the error list is already the natural collection for that. See [Aggregate · `Combine`/`All`](aggregate.md).

---

## Transient errors (retry)

```csharp
if (error.IsTransient)   // true for ServiceUnavailable, Timeout, TooManyRequests, GatewayTimeout
    await RetryAsync();
```

`IsTransient` is built into the type. *Circuit breakers*, retry policies and *health checks* inspect this without *parsing* messages or maintaining string lists.

**Why it pays off:** "is this worth trying again?" becomes a property of the type system, not a fragile text-based convention.

### `IsTransientFailure` — the same question, on the whole `AxisResult`

A poll loop or background worker rarely has one error to inspect — it has a whole `AxisResult`. Rather than
re-deriving `result.IsFailure && result.Errors.All(e => e.IsTransient)` at every call site, ask the result
directly:

```csharp
if (result.IsTransientFailure)
    await BackoffAndRetryAsync();     // dependency not ready yet — not a terminal failure
```

`IsTransientFailure` is a **different classification axis** than `IsSuccess`/`IsFailure` — it distinguishes
*transient-vs-terminal* (retry policy), not *success-vs-failure* (the ROP rail). Branching on it is the
sanctioned way to drive retry/poll-loop control flow; the ROP-composition rule (no `if`/`else` on
`IsSuccess`/`IsFailure`, [Railway-Oriented Programming](railway-oriented-programming.md)) does not apply to
it, because that rule is about the two-rail *pipeline* shape, and a retry loop's "keep waiting or move on"
decision is not a pipeline — it is a stateful loop over a stream of outcomes across time.

---

## See also

- [Remap errors · `MapError`](map-errors.md) — rewrite codes/types when crossing layers
- [Recover · `Recover`](recover.md) — return from the failure rail to the success rail
- [Exit · `Match`](match.md) — convert the final result into an HTTP response

---

↩ [Back to AxisResult docs](README.md)
