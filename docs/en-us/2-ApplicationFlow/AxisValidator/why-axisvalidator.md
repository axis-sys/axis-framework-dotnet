# Why AxisValidator? · comparison

> There are other ways to validate input in .NET. This page tells you why AxisValidator is different — a direct comparison, no hand-waving.

---

## vs. FluentValidation (directly)

FluentValidation is the substrate. Calling it directly has two issues:

1. **Throws.** `validator.ValidateAndThrow(...)` throws `ValidationException`; every handler needs a wrapper to turn it into a typed `Result`.
2. **Error messages are the contract.** `e.ErrorMessage` is a string baked at construction time — not great for code-driven decisions.

`AxisValidator` projects FluentValidation failures into typed `AxisError.ValidationRule(code)` entries and returns `AxisResult`. The error **code** is the contract — messages live in your presentation resolver.

## vs. `IValidatableObject` / Data Annotations

OK for trivial models, painful at scale. No cross-field rules, no async, no DI — and exceptions on the way out. `AxisValidator` gives you the same declarative shape with the railway, with FluentValidation underneath.

## vs. `Result<T>` validators rolled by hand

DIY. Same shape, but you re-derive the helpers (`NotNullOrEmpty`, `RequiredSlug`, `RequiredEmail`), the pipeline integration, the localisation strategy. `AxisValidatorBase<T>` saves the cost.

---

## The comparison

| Feature | AxisValidator | FluentValidation direct | `IValidatableObject` | Bespoke `IValidator` |
|---|:--:|:--:|:--:|:--:|
| Returns `AxisResult` | **Yes** | No | No | Maybe |
| Pipeline behaviour for automatic enforcement | **Yes** | No | No | Maybe |
| Battle-tested helpers (`NotNullOrEmpty`, `RequiredEmail`, `RequiredGuid7`, `RequiredSlug`) | **Yes** | Manual | No | Maybe |
| Cross-field dependent rules | **Yes** | Yes | No | Maybe |
| Localisation packs (CPF/brazilian cellphone, SSN/U.S. phone) | **Yes** (`AxisValidator.Brazil`, `AxisValidator.Usa`) | No | No | No |
| Async + cancellation via `IAxisMediator` | **Yes** | Yes | No | Maybe |
| Code-as-contract (no message templates) | **Yes** | Configurable | No | Maybe |
| Zero NuGet deps in the abstraction | **Yes** | No | n/a | Yes |

---

## See also

- [The `IAxisValidator<T>` contract](iaxisvalidator.md) — the surface
- [Validator base and rules](validator-base.md) — the helpers that justify the abstraction
- [`ValidationBehavior`](validation-behavior.md) — automatic invocation

---

↩ [Back to AxisValidator docs](README.md)
