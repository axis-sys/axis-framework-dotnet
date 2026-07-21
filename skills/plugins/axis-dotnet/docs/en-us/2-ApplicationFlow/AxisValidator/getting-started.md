# Getting started · installation and usage

> Install the packages, declare a validator, turn on the pipeline behaviour — and your handlers receive only valid input.

---

## Installation

```
dotnet add package AxisValidator                        # the abstraction + behaviour
dotnet add package AxisValidator.FluentValidation       # the base + adapter
dotnet add package AxisValidator.Brazil                 # optional: CPF + cellphone helpers
dotnet add package AxisValidator.Usa                     # optional: SSN + phone helpers
```

`AxisValidator` depends on `AxisResult` and `AxisMediator.Contracts`. The FluentValidation adapter brings `FluentValidation`.

---

## Registering

```csharp
using AxisValidator;
using System.Reflection;

builder.Services
    .AddAxisMediator()
    .AddAxisValidator(Assembly.GetExecutingAssembly());
```

`AddAxisValidator(...)` does four things:

1. `services.AddValidatorsFromAssemblies(assemblies, includeInternalTypes: true)` — discovers every `AbstractValidator<T>` in the given assemblies.
2. Registers `IAxisValidator<>` as scoped, bound to `FluentValidatorAdapter<>`.
3. Registers `IAxisPipelineBehavior<>` as transient, bound to the open-generic `ValidationBehavior<>` (void-command pipeline).
4. Registers `IAxisPipelineBehavior<,>` as transient, bound to the open-generic `ValidationBehavior<,>` (typed-request pipeline).

If you have validators in another assembly, pass it too: `AddAxisValidator(typeof(X).Assembly, typeof(Y).Assembly)`.

---

## Declaring a validator

```csharp
using AxisValidator;
using FluentValidation;

public class CreatePersonValidator : AxisValidatorBase<CreatePersonCommand>
{
    public CreatePersonValidator()
    {
        RequiredEmail (x => x.Email,    "PERSON_EMAIL_INVALID");
        RequiredSlug  (x => x.Username, "PERSON_USERNAME_INVALID", length: 32);
        RequiredGuid7 (x => x.TenantId, "TENANT_ID_INVALID");

        // standard FluentValidation works too
        RuleFor(x => x.Age).GreaterThanOrEqualTo(18).WithErrorCode("PERSON_AGE_BELOW_MIN");
    }
}
```

The class inherits from `AxisValidatorBase<T>` (which inherits from `FluentValidation.AbstractValidator<T>`), so every FluentValidation API still works.

---

## What the pipeline does

```csharp
public Task<AxisResult<CreatePersonResponse>> HandleAsync(CreatePersonCommand cmd)
{
    // by the time this runs, cmd has already passed CreatePersonValidator
    return factory.CreateAsync(cmd)
        .ThenAsync(person => writer.CreateAsync(person))
        .MapAsync(_ => new CreatePersonResponse { PersonId = cmd.PersonId });
}
```

If the request fails validation, the `ValidationBehavior` short-circuits the pipeline with `Error(errors)` — the handler is never called. → **[`ValidationBehavior`](validation-behavior.md)**

**Why it pays off:** the handler reads as if input were always valid, because by the time it runs it is. Validation lives once, where it belongs.

---

## See also

- [The `IAxisValidator<T>` contract](iaxisvalidator.md) — the surface
- [Validator base and rules](validator-base.md) — every helper on `AxisValidatorBase<T>`
- [`ValidationBehavior` — pipeline enforcement](validation-behavior.md) — opt-in mediator behaviour
- [Brazilian validators](brazil.md) — CPF, cellphone, `BrazilValidator` static facade
- [American validators](usa.md) — SSN, phone, `UsaValidator` static facade
- [Why AxisValidator?](why-axisvalidator.md) — the case against throwing validators
- [API reference](api-reference.md) — every member in one place

---

↩ [Back to AxisValidator docs](README.md)
