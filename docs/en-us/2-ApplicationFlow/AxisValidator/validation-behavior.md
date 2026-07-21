# `ValidationBehavior` — pipeline enforcement

> An `IAxisPipelineBehavior` that runs `IAxisValidator<TRequest>.ValidateAsync(request)` before every mediator request. On `Failure`, the pipeline short-circuits with the validation errors — the handler is never invoked.

```csharp
services.AddAxisValidator(Assembly.GetExecutingAssembly());   // registers ValidationBehavior automatically
```

---

## When to use

Always — unless you genuinely want to call the validator yourself inside the handler. The behaviour costs nothing when no validator is registered for the request type (`GetService<IAxisValidator<TRequest>>()` returns `null`, the behaviour calls `next()` directly).

## When *not* to use

| You want to… | Use instead |
|---|---|
| validate **only on a subset** of requests | mark the others with no validator (the behaviour is a no-op) |
| run **multiple** validators per request | a custom behaviour that composes them |
| validate **after** other behaviours | reorder pipelines on the mediator side |

---

## What the behaviour does

Reading `ValidationBehavior<TRequest>` and `<TRequest, TResponse>` directly:

```csharp
public async Task<AxisResult> HandleAsync(TRequest request, AxisPipelineContext context, Func<Task<AxisResult>> next)
{
    var validator = serviceProvider.GetService<IAxisValidator<TRequest>>();
    if (validator is null) return await next();          // no validator → pass through

    var result = await validator.ValidateAsync(request);
    return result.IsFailure ? result : await next();     // fail → short-circuit
}
```

The `<TRequest, TResponse>` overload is identical, except the failure is converted back to `AxisResult<TResponse>.Error(errors)` via the implicit array → `AxisResult<TResponse>` conversion.

---

## What gets registered

`AddAxisValidator(...)` (from `DependencyInjection.cs`):

```csharp
services.AddValidatorsFromAssemblies(assemblies, includeInternalTypes: true);
services.AddScoped(typeof(IAxisValidator<>), typeof(FluentValidatorAdapter<>));
services.AddTransient(typeof(IAxisPipelineBehavior<>), typeof(ValidationBehavior<>));
services.AddTransient(typeof(IAxisPipelineBehavior<,>), typeof(ValidationBehavior<,>));
```

- FluentValidation discovers your `AbstractValidator<T>` (and `AxisValidatorBase<T>`) in the given assemblies.
- `IAxisValidator<>` resolves to `FluentValidatorAdapter<>` (scoped, so it can read `IAxisMediator.CancellationToken`).
- Both validation behaviours are registered as open-generic transients for the mediator pipeline.

---

## Real-world example — silent failure handling at the edge

```csharp
public Task<AxisResult<CreatePersonResponse>> HandleAsync(CreatePersonCommand cmd)
{
    // cmd is already valid here — the behaviour gated it.
    return factory.CreateAsync(cmd)
        .ThenAsync(person => writer.CreateAsync(person))
        .MapAsync(_ => new CreatePersonResponse { PersonId = cmd.PersonId });
}
```

```csharp
// At the HTTP edge (your controller / Gateway)
return await mediator.Cqrs.ExecuteAsync<CreatePersonCommand, CreatePersonResponse>(cmd)
    .Match(
        onSuccess: r      => Results.Ok(r),
        onFailure: errors => Results.Problem(
            title: "VALIDATION_FAILED",
            detail: string.Join(",", errors.Select(e => e.Code))));
```

**Why it pays off:** the handler never re-validates. The HTTP edge sees the typed `ValidationRule` errors and maps them to 400 — no `try/catch (ValidationException)` anywhere.

---

## See also

- [The `IAxisValidator<T>` contract](iaxisvalidator.md) — what the behaviour calls
- [Validator base and rules](validator-base.md) — what your validator looks like
- [Brazilian validators](brazil.md) — the localisation pack that plugs into the same pipeline
- [American validators](usa.md) — same idea, for SSN and U.S. phone

---

↩ [Back to AxisValidator docs](README.md)
