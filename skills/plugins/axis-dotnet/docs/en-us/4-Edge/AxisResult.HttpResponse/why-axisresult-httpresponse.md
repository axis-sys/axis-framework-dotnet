# Why AxisResult.HttpResponse? · comparison

> A tiny edge package that pays for itself the second time you write a controller. Here is what it replaces and why a dedicated adapter beats the alternatives.

## vs. mapping by hand with `Match`

You *can* translate every result in the controller with `Match`:

```csharp
return result.Match(
    onSuccess: value  => Ok(value),
    onFailure: errors => errors[0].Type switch
    {
        AxisErrorType.NotFound       => NotFound(),
        AxisErrorType.ValidationRule => BadRequest(),
        AxisErrorType.Conflict       => Conflict(),
        // … eleven more, repeated in every controller, easy to get wrong
        _ => StatusCode(500)
    });
```

Three problems: the `switch` is **duplicated** across every endpoint, it inspects `errors[0]` so a `ValidationRule` shadowing an `InternalServerError` returns `400` instead of `500`, and nothing forces the `ProblemDetails`, `traceId` or internal-error suppression to be consistent. `HttpContext.SendAsync` does all of it once — down to reading the `traceId` from `HttpContext.TraceIdentifier` automatically.

## vs. ASP.NET Core `ProblemDetails` / exception filters

The framework's `ProblemDetails` and exception-based middleware assume **exceptions** are your failure channel. AxisResult's whole point is that errors are **values** — they never throw, so there is nothing for an exception filter to catch. This package bridges value-based failures to the RFC 7807 body without resurrecting control-flow-by-exception.

## vs. a generic `Result` → `IActionResult` library (e.g. `Ardalis.Result.AspNetCore`)

Generic adapters map a small, fixed set of statuses and know nothing about your error taxonomy. This package is built **for** `AxisError`: all 12 `AxisErrorType` categories, severity-based selection on multi-error failures, internal-error suppression and a stable `code`/`type` payload — no glue code to keep in sync.

---

## The comparison

| Feature | **AxisResult.HttpResponse** | Hand-rolled `Match` | ASP.NET `ProblemDetails` filter | Generic `Result` adapter |
|---|:--:|:--:|:--:|:--:|
| One call per endpoint | **Yes** | No (per-endpoint switch) | Partial | Yes |
| All 12 `AxisErrorType` mapped | **Yes** | Manual | No | Partial |
| Severity-based status on multi-error | **Yes** | No | No | No |
| RFC 7807 `ProblemDetails` body | **Yes** | Manual | Yes | Partial |
| Internal errors suppressed | **Yes** | Manual | No | No |
| `traceId` always included | **Yes** | Manual | Partial | No |
| Works with value-based (non-throwing) errors | **Yes** | Yes | No | Yes |

---

## See also

- [Convert · `HttpContext.SendAsync`](send-http-response.md) — the one call this is all about
- [Error → status mapping](error-status-mapping.md) — the table and severity rules that make it consistent
- [Getting started](getting-started.md) — install and use it in a controller

---

↩ [Back to AxisResult.HttpResponse docs](README.md)
