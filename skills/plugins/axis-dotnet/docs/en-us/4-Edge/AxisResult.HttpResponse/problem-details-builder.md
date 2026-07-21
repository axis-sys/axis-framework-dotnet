# Reuse outside MVC · `AxisProblemDetailsBuilder`

> The same severity-based `ProblemDetails` rendering `HttpContext.SendAsync` uses at the controller, exposed as a standalone type — for middleware and authorization filters that run outside the `IActionResult` pipeline.

```csharp
// In a middleware — no ActionContext, so write the response yourself
await context.WriteProblemDetailsAsync(AxisError.Forbidden("TENANT_NOT_ALLOWED"));

// In an IAsyncAuthorizationFilter — short-circuit MVC by assigning a result
context.Result = context.HttpContext.ToProblemDetailsResult(errors);
```

---

## When to use

Outside the MVC action pipeline — a middleware, an `IAsyncAuthorizationFilter`, or any other adapter that already holds an `AxisResult`/`AxisError` list synchronously and needs the exact same status/`ProblemDetails` rule controllers get from [`HttpContext.SendAsync`](send-http-response.md), without depending on `IActionResult`.

## When *not* to use

| You want to… | Use instead |
|---|---|
| render inside a controller action | [`HttpContext.SendAsync`](send-http-response.md) — same rule, returns `IActionResult` |
| customize the `ProblemDetails` body per endpoint | `Match` + your own `ProblemDetails` |
| a generic handler for *unhandled exceptions* | this only renders an `AxisResult`/`AxisError` you already hold synchronously — see [Why AxisResult.HttpResponse?](why-axisresult-httpresponse.md) |

---

## The three members, split by what they do

The builder **builds**; the two extensions **consume**. Each is named for its effect, so no member surprises you at the call site.

| Member | Signature | Effect |
|---|---|---|
| `AxisProblemDetailsBuilder.Build` | `(int StatusCode, ProblemDetails Details) Build(IReadOnlyList<AxisError> errors, string traceId)` | **Computes.** Pure — no `HttpContext`, no I/O. `SendAsync`'s failure rendering delegates to it. |
| `HttpContext.WriteProblemDetailsAsync` | `Task WriteProblemDetailsAsync(this HttpContext context, IReadOnlyList<AxisError> errors)` | **Writes.** Sets `Response.StatusCode`, serializes the body. The response is sent — don't write it again. |
| `HttpContext.ToProblemDetailsResult` | `ObjectResult ToProblemDetailsResult(this HttpContext context, IReadOnlyList<AxisError> errors)` | **Converts.** Returns an `ObjectResult`; leaves the response untouched for MVC to send. |

Each extension has a single-`AxisError` overload that delegates to the list one. Both derive status and body from `Build`, so neither can drift from what a controller returns. The `traceId` comes from `HttpContext.TraceIdentifier` automatically.

> **Why not `BuildAsync`?** Because `Build` and `BuildAsync` would read as a sync/async pair of the same operation — and they aren't. `Build` hands you a value; the write path performs I/O and returns nothing. Naming each member for its effect keeps `Builder` an honest suffix.

---

## Real-world example — a tenant-header middleware

```csharp
internal sealed class CurrentTenantMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ICurrentTenantAccessor accessor)
    {
        if (!context.Request.Headers.TryGetValue("X-Tenant", out var tenant))
        {
            await context.WriteProblemDetailsAsync(AxisError.ValidationRule("TENANT_HEADER_MISSING"));
            return;
        }

        await accessor.Set(tenant.ToString())
            .Match(onSuccess: () => next(context),
                   onFailure: context.WriteProblemDetailsAsync);   // method group — no lambda needed
    }
}
```

**Why it pays off:** the middleware never reimplements "which error wins" or "how do I suppress an internal error" — it reuses the exact same rule as every controller in the API, so a new call site can't silently diverge from it. Because the extension hangs off `HttpContext`, `onFailure` takes it as a method group. The builder was extracted as a public type for exactly this reason: an earlier reference app had two middleware/filter consumers hand-rolling this rendering outside the MVC pipeline, one of them with a latent severity bug caused by the duplication itself.

## Real-world example — an authorization filter

```csharp
internal sealed class PermissionAuthorizationFilter(IAuthorizationFacade facade) : IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var grants = await facade.GetTenantGrantsAsync(query);

        context.Result = grants.Match(
            onSuccess: response => IsGranted(response) ? null : context.HttpContext.ToProblemDetailsResult(AxisError.Forbidden("PERMISSION_DENIED")),
            onFailure: context.HttpContext.ToProblemDetailsResult);
    }
}
```

**Why it pays off:** a filter must *not* write the response — it short-circuits MVC by assigning `context.Result`. `ToProblemDetailsResult` returns the `ObjectResult` and leaves the response alone, which is exactly the contract the filter needs.

## Why a tuple, not an `IActionResult`

`(int, ProblemDetails)` is plain data — it does not presume an MVC `ActionContext`. A controller wraps it in an `ObjectResult` (that is exactly what `HttpContext.SendAsync` does internally on the failure rail); a middleware writes the two parts straight to `HttpContext.Response`. Either caller gets the same values with no adapter-specific type in between.

---

## See also

- [Convert · `HttpContext.SendAsync`](send-http-response.md) — the controller-facing wrapper over this same builder
- [Error → status mapping](error-status-mapping.md) — the mapping table, severity and `ProblemDetails` shape this builder implements
- [API reference](api-reference.md) — the full public surface at a glance

---

↩ [Back to AxisResult.HttpResponse docs](README.md)
