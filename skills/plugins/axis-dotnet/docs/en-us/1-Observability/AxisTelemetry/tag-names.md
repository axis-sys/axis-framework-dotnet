# Tag names

> Two static classes — `TelemetryTagNames` and `AuthTelemetryTagNames` — that hold every constant the framework uses when tagging spans and metrics. Use them instead of inline strings to keep sink-side queries predictable and refactor-safe.

```csharp
span.SetTag(TelemetryTagNames.RequestName, "CreatePersonCommand");
```

---

## When to use

Always, when you are tagging spans or metrics with the **framework-defined** signals (request name, trace id, journey id, identity, auth result, etc.). For application-specific tags (e.g. `"order.id"`), use your own constants or inline strings.

## When *not* to use

| You want to… | Use instead |
|---|---|
| invent a new framework-wide tag | extend `TelemetryTagNames` and reuse it; do not scatter new strings |
| name a one-off application tag (`"warehouse"`, `"step"`) | a local `const string` or an inline string |

---

## `TelemetryTagNames`

| Constant | Value | Meaning |
|---|---|---|
| `AxisEntityId` | `"axis.axis_entity_id"` | the authenticated user's `AxisEntityId` |
| `TraceId` | `"axis.trace_id"` | the mediator's `TraceId` |
| `JourneyId` | `"axis.journey_id"` | the mediator's `JourneyId` (saga / long-running) |
| `RequestType` | `"axis.request_type"` | `"command"` or `"query"` |
| `RequestName` | `"axis.request_name"` | the `typeof(TRequest).Name` |
| `ResultSuccess` | `"axis.result_success"` | `true`/`false` from `AxisResult.IsSuccess` |
| `ErrorCodes` | `"axis.error_codes"` | comma-separated `result.Errors[*].Code` |
| `ExceptionType` | `"axis.exception_type"` | `ex.GetType().Name` |

## `AuthTelemetryTagNames`

| Constant | Value | Meaning |
|---|---|---|
| `Scheme` | `"auth.scheme"` | the auth scheme that handled the request (e.g. `"jwt-bearer"`, `"oauth2"`) |
| `Result` | `"auth.result"` | the result of the auth attempt (`"success"`, `"failure"`, etc.) |
| `FailureReason` | `"auth.failure_reason"` | a short reason code when the attempt failed |
| `ApiId` | `"auth.api_id"` | the external API id involved (when applicable) |
| `BruteForceSuspected` | `"auth.brute_force_suspected"` | `true` when the failure is part of a suspected brute-force pattern |

---

## Why constants and not strings

| Without constants | With constants |
|---|---|
| `"axis.request_name"` typed manually at every site → typo risk | `TelemetryTagNames.RequestName` → compiler catches typos |
| Renaming requires grep + manual edit | rename in one place; the compiler propagates |
| Sink-side queries refer to magic strings | sink-side queries refer to a documented, stable list |

---

## Real-world example — tagging from a custom adapter

```csharp
public class CustomAuthHandler
{
    public async Task<AxisResult> HandleAsync(AuthRequest request)
    {
        using var span = telemetry.StartSpan("auth.handle", AxisSpanKind.Server)
            .SetTag(AuthTelemetryTagNames.Scheme, "jwt-bearer")
            .SetTag(AuthTelemetryTagNames.ApiId, request.ApiId);

        var result = await authenticator.AuthenticateAsync(request);

        span.SetTag(AuthTelemetryTagNames.Result, result.IsSuccess ? "success" : "failure");

        if (result.IsFailure)
            span.SetTag(AuthTelemetryTagNames.FailureReason, result.Errors[0].Code);

        return result;
    }
}
```

**Why it pays off:** every dashboard that already filters by `auth.scheme = "jwt-bearer"` keeps working. New tags only need to be added once to the constant file — every site that uses them picks the change up at compile time.

---

## See also

- [`TelemetryBehavior`](telemetry-behavior.md) — uses every `TelemetryTagNames.*` automatically
- [Spans · `IAxisSpan`](spans.md) — `SetTag` is the method these constants feed
- [The contracts](contracts.md) — what carries the tag

---

↩ [Back to AxisTelemetry docs](README.md)
