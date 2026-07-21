# AxisValidator — Documentation

> 🌐 [Português (documentação navegável)](../../../pt-br/2-ApplicationFlow/AxisValidator/README.md)

**Declarative validation that returns `AxisResult`** — `IAxisValidator<T>` with `Validate` / `ValidateAsync`, a bundled `FluentValidation` adapter, an `AxisValidatorBase<T>` with battle-tested rule helpers (`NotNullOrEmpty`, `RequiredEmail`, `RequiredSlug`, `RequiredGuid7`, dependent rules), an opt-in mediator pipeline behaviour that short-circuits on `Failure`, a `AxisValidator.Brazil` package for CPF / brazilian cellphone validation, and a `AxisValidator.Usa` package for SSN / U.S. phone validation.

```csharp
public class CreatePersonValidator : AxisValidatorBase<CreatePersonCommand>
{
    public CreatePersonValidator()
    {
        RequiredEmail (x => x.Email,    "PERSON_EMAIL_INVALID");
        RequiredSlug  (x => x.Username, "PERSON_USERNAME_INVALID", length: 32);
        RequiredGuid7 (x => x.TenantId, "TENANT_ID_INVALID");
    }
}
```

Use this page as a **map**: read the trunk below (~5 min) and jump straight to the detail of the group you need — without reading hundreds of lines.

---

## The trunk (read first)

### The interface in 60 seconds

```csharp
public interface IAxisValidator<in T>
{
    AxisResult       Validate(T instance);
    Task<AxisResult> ValidateAsync(T instance);
}
```

A success returns `AxisResult.Ok()`. A failure returns `AxisResult.Error(errors)` where every error has `Type = AxisErrorType.ValidationRule` and the `Code` is the rule's error code. → **[The `IAxisValidator<T>` contract](iaxisvalidator.md)**

### The base — `AxisValidatorBase<T>`

A `FluentValidation.AbstractValidator<T>` with `Axis`-flavoured helpers. You inherit it, call helpers in the constructor, and the framework discovers your validator by assembly scan. → **[Validator base and rules](validator-base.md)**

### `ValidationBehavior` — pipeline-level enforcement

An opt-in `IAxisPipelineBehavior` that runs the validator (if any) before every mediator request. On `Failure`, the pipeline short-circuits with the validation errors — the handler never executes. → **[`ValidationBehavior` — pipeline enforcement](validation-behavior.md)**

### Brazilian localisation — `AxisValidator.Brazil`

CPF, brazilian cellphone, plus the `BrazilValidator` static facade for "format this cellphone" and "validate this CPF". → **[Brazilian validators](brazil.md)**

### American localisation — `AxisValidator.Usa`

SSN, U.S. (NANP) phone, plus the `UsaValidator` static facade for "format this phone" and "validate this SSN". → **[American validators](usa.md)**

### Installation

```
dotnet add package AxisValidator                        # the abstraction + behaviour
dotnet add package AxisValidator.FluentValidation       # the AbstractValidator base + adapter
dotnet add package AxisValidator.Brazil                 # CPF + brazilian cellphone helpers
dotnet add package AxisValidator.Usa                     # SSN + U.S. phone helpers
```

→ Full guide: **[Getting started](getting-started.md)**

---

## The map (jump to what you need)

| Group | You want to… | Detail |
|---|---|---|
| **Contract · `IAxisValidator<T>`** | run a validator and get an `AxisResult` | [iaxisvalidator.md](iaxisvalidator.md) |
| **Base · `AxisValidatorBase<T>`** ⭐ | declare a validator with `Axis` helpers | [validator-base.md](validator-base.md) |
| **Pipeline · `ValidationBehavior`** | enforce validation before every handler | [validation-behavior.md](validation-behavior.md) |
| **Brazil · `AxisValidator.Brazil`** | CPF, cellphone, country extensions | [brazil.md](brazil.md) |
| **USA · `AxisValidator.Usa`** | SSN, phone, country extensions | [usa.md](usa.md) |
| **Why?** | the case for `Result`-returning validation | [why-axisvalidator.md](why-axisvalidator.md) |
| **Reference** | every member at a glance | [api-reference.md](api-reference.md) |

**Start here:** [Getting started](getting-started.md) · [The `IAxisValidator<T>` contract](iaxisvalidator.md) · [Why AxisValidator?](why-axisvalidator.md)

**Fundamentals:** [Validator base and rules](validator-base.md) · [`ValidationBehavior`](validation-behavior.md) · [Brazilian validators](brazil.md) · [American validators](usa.md)

**Reference & extras:** [API reference](api-reference.md)

---

## Design principles

1. **Validation returns `AxisResult`.** A failed rule is a value on the rail, not an exception in the handler.
2. **One error code = one rule.** Each rule fails with a stable `Code` (`"PERSON_EMAIL_INVALID"`). No prose, no message templates leaking into the contract.
3. **Pipeline-level enforcement.** Register the behaviour and the handler stops worrying about input — by the time it runs, the request is valid.
4. **FluentValidation underneath.** Battle-tested library; the adapter translates failures into typed `AxisError.ValidationRule(code)` entries.
5. **Localisation is a separate package.** Country-specific rules (CPF, brazilian cellphone; SSN, U.S. phone) live in `AxisValidator.Brazil` and `AxisValidator.Usa` so the core stays neutral.

---

## License

Apache 2.0
