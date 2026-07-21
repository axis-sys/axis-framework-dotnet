# Contract · `IAxisValidator<T>`

> Two methods. Both return `AxisResult` — `Ok()` when the instance is valid, `Error(errors)` where every error is a typed `AxisError.ValidationRule(code)`.

```csharp
public interface IAxisValidator<in T>
{
    AxisResult       Validate(T instance);
    Task<AxisResult> ValidateAsync(T instance);
}
```

---

## When to use

Wherever you would write `IValidator<T>` from FluentValidation, write `IAxisValidator<T>`. Mostly you do not write it by hand at all — you inherit `AxisValidatorBase<T>` and the framework wires the adapter for you.

## When *not* to use

| You want to… | Use instead |
|---|---|
| validate **business invariants** that depend on data not in the request | the domain entity (return an `AxisError.BusinessRule`) |
| validate **at parse time** (HTTP model binding) | a `TryParse` on a `[ValueObject]` type from [`AxisTypes`](../../0-Foundations/AxisTypes/README.md) |
| validate **before persistence** with database queries | the application layer with `Then(...)` + a repository |

---

## How the bundled adapter works

Reading `FluentValidatorAdapter<T>` directly:

1. Resolve `IValidator<T>` from the `IServiceProvider`. If none is registered, return `AxisResult.Ok()` — *no validator = nothing to validate*.
2. Run `validator.Validate(instance)` (or `ValidateAsync` with `mediator.CancellationToken`).
3. On `IsValid`, return `Ok()`.
4. On failure, project each `ValidationFailure.ErrorCode` into `AxisError.ValidationRule(code)` and return `AxisResult.Error(errors)`.

The adapter throws away `ValidationFailure.ErrorMessage`. **The contract is the `Code`.** Render messages at the presentation edge via a `code → message` resolver.

---

## Real-world examples

### 1. Call a validator manually

```csharp
var result = await validator.ValidateAsync(cmd);
if (result.IsFailure)
    return result.Errors.ToArray();    // implicit conversion → AxisResult<TResponse>.Error
```

**Why it pays off:** in a hand-orchestrated flow (a console app, a non-mediator code path), you still get typed errors on the rail — no `ValidationException` to catch.

### 2. Validate inside the railway

```csharp
return await ParseInputAsync(raw)
    .ThenAsync(cmd => validator.ValidateAsync(cmd).Map(_ => cmd))
    .ThenAsync(cmd => factory.CreateAsync(cmd))
    .MapAsync(_ => new CreatePersonResponse { ... });
```

**Why it pays off:** the parse-then-validate-then-create chain reads as a sentence. If validation fails the rest is skipped, no exception.

### 3. No validator? `Ok()` automatically

```csharp
// no CreatePersonValidator registered
var result = await validator.ValidateAsync(cmd);   // Ok() — nothing to check
```

The default behaviour is deliberate: registering a validator is **opt-in**. The handler still runs.

---

## See also

- [Validator base and rules](validator-base.md) — what you actually write
- [`ValidationBehavior`](validation-behavior.md) — call this method automatically before every handler
- [API reference](api-reference.md) — every member, in one place

---

↩ [Back to AxisValidator docs](README.md)
