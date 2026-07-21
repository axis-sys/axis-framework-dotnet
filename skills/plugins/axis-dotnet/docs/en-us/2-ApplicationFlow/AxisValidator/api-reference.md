# API reference

> The complete catalogue, grouped by responsibility. Use it for lookup — each group links back to its detail page.

---

## The contract — `IAxisValidator<T>`

| Method | Signature | Description |
|---|---|---|
| `Validate` | `AxisResult Validate(T instance)` | synchronous validation |
| `ValidateAsync` | `Task<AxisResult> ValidateAsync(T instance)` | async validation (honours `IAxisMediator.CancellationToken` in the bundled adapter) |

→ [The `IAxisValidator<T>` contract](iaxisvalidator.md)

---

## Base — `AxisValidatorBase<T>` (FluentValidation adapter)

| Helper | Signature | Description |
|---|---|---|
| `NotNullOrEmpty` | `void NotNullOrEmpty<TProperty>(Expression<Func<T, TProperty?>>, string errorCode)` | not null, not default, not whitespace |
| `NotNullOrEmpty` | `void NotNullOrEmpty<TProperty>(Expression<Func<T, TProperty?>>, string errorCode, Action dependentRules)` | + nested rules when present |
| `NotNullOrEmpty` | `void NotNullOrEmpty<TProperty>(Expression<Func<T, TProperty?>>, string errorCode, Action<TProperty> dependentRules)` | + nested rules over the typed value |
| `DependentRules` | `void DependentRules<T1, T2>(Expression<Func<T, T1?>>, string codeA, Expression<Func<T, T2?>>, string codeB, Func<T1, T2, AxisResult> rule)` | cross-field rule returning `AxisResult` |
| `RequiredGuid7` | `void RequiredGuid7(Expression<Func<T, string?>>, string errorCode)` | not empty + valid UUID v7 string |
| `RequiredWithMaxLength` | `void RequiredWithMaxLength(Expression<Func<T, string?>>, string errorCode, int? length = 255)` | not empty + length constraint |
| `RequiredSlug` | `void RequiredSlug(Expression<Func<T, string?>>, string errorCode, int? length = 255)` | not empty + `[a-zA-Z0-9_-]` + length |
| `RequiredEmail` | `void RequiredEmail(Expression<Func<T, string?>>, string errorCode)` | not empty + valid email |
| `RequiredTryParse` | `void RequiredTryParse(Expression<Func<T, string?>>, string errorCode, Func<object?, bool> parse)` | not empty + custom parser |
| `Range` | `void Range<TValue>(Expression<Func<T, TValue?>>, string errorCode, TValue? min = null, TValue? max = null) where TValue : struct, IComparable<TValue>` | struct value within `[min, max]`; skipped when `null` |
| `RequiredCollection` | `void RequiredCollection<TProperty>(Expression<Func<T, TProperty?>>, string errorCode) where TProperty : IEnumerable` | collection not null and has at least one item |
| `MaxCount` | `void MaxCount<TProperty>(Expression<Func<T, TProperty?>>, string errorCode, int max) where TProperty : IEnumerable` | collection has at most `max` items (`null` passes) |
| `Satisfies` | `void Satisfies<TProperty>(Expression<Func<T, TProperty?>>, string errorCode, Func<TProperty?, bool> predicate)` | custom predicate over the property's value |
| `Satisfies` | `void Satisfies<TProperty>(Expression<Func<T, TProperty?>>, string errorCode, Func<T, TProperty?, bool> predicate)` | custom predicate with access to the whole instance |
| `EachSatisfies` | `void EachSatisfies<TItem>(Expression<Func<T, IEnumerable<TItem>?>>, string errorCode, Func<TItem?, bool> predicate)` | every item of the collection satisfies a predicate |
| `EachUsesValidator` | `void EachUsesValidator<TItem>(Expression<Func<T, IEnumerable<TItem>?>>, AxisValidatorBase<TItem> itemValidator)` | every item validated by a nested validator |
| `UsesValidator` | `void UsesValidator<TProperty>(Expression<Func<T, TProperty?>>, AxisValidatorBase<TProperty> validator) where TProperty : class` | reference-type property validated by a nested validator |

Constant `DefaultMaxLength = 255`.

→ [Validator base and rules](validator-base.md)

---

## Pipeline behaviour — `ValidationBehavior`

| Type | Where it sits | What it does |
|---|---|---|
| `ValidationBehavior<TRequest>` | requests with no response | resolve `IAxisValidator<TRequest>`; if present and `Failure`, short-circuits with the validation errors |
| `ValidationBehavior<TRequest, TResponse>` | requests with a typed response | same, but returns `AxisResult<TResponse>.Error(errors)` on `Failure` |

→ [`ValidationBehavior`](validation-behavior.md)

---

## DI extensions (C# 14 extension members on `IServiceCollection`)

| Extension | Effect |
|---|---|
| `services.AddAxisValidator(params Assembly[] assemblies)` | discovers `AbstractValidator<T>` in `assemblies`, binds `IAxisValidator<>` → `FluentValidatorAdapter<>` (scoped), registers `ValidationBehavior<>` and `ValidationBehavior<,>` as transient `IAxisPipelineBehavior` |

---

## Brazilian validators — `AxisValidator.Brazil`

| Member | Signature | Description |
|---|---|---|
| `CpfValidator.Validate` | `static bool Validate(string? cpf)` | check-digit validation of a CPF |
| `CellphoneValidator.Format` | `static string? Format(string? cellphone)` | canonical `"(DD) 9NNNN-NNNN"` or `null` |
| `CellphoneValidator.OnlyNumbers` | `static string? OnlyNumbers(string? cellphone)` | digits-only normalisation or `null` |
| `CellphoneValidator.TryFormat` | `static bool TryFormat(string? cellphone, out string? formatted)` | non-throwing companion |
| `BrazilValidator.FormatCellphone` | `static AxisResult<string> FormatCellphone(string? phone)` | `Ok(formatted)` or `ValidationRule("CELLPHONE_NUMBER_NULL_OR_NOT_VALID")` |
| `BrazilValidator.ValidateCpf` | `static AxisResult<string> ValidateCpf(string? document)` | `Ok(document)` or `ValidationRule("DOCUMENT_INVALID")` |
| `RandomBrazilianDataHelper.GenerateCpf` | `static string GenerateCpf(bool format = false)` | random valid CPF for tests |

→ [Brazilian validators](brazil.md)

---

## American validators — `AxisValidator.Usa`

| Member | Signature | Description |
|---|---|---|
| `SsnValidator.Validate` | `static bool Validate(string? ssn)` | structural validation of an SSN (no public check-digit algorithm exists) |
| `PhoneValidator.Format` | `static string? Format(string? phone)` | canonical `"(NPA) NXX-XXXX"` or `null` |
| `PhoneValidator.OnlyNumbers` | `static string? OnlyNumbers(string? phone)` | digits-only normalisation or `null` |
| `PhoneValidator.TryFormat` | `static bool TryFormat(string? phone, out string? formatted)` | non-throwing companion |
| `UsaValidator.FormatPhone` | `static AxisResult<string> FormatPhone(string? phone)` | `Ok(formatted)` or `ValidationRule("PHONE_NUMBER_NULL_OR_NOT_VALID")` |
| `UsaValidator.ValidateSsn` | `static AxisResult<string> ValidateSsn(string? document)` | `Ok(document)` or `ValidationRule("DOCUMENT_INVALID")` |
| `RandomUsaDataHelper.GenerateSsn` | `static string GenerateSsn(bool format = false)` | random structurally valid SSN for tests |

→ [American validators](usa.md)

---

## Behaviour contract (for adapters)

| Scenario | Returned `AxisResult` |
|---|---|
| no validator registered | `Ok()` |
| validator says valid | `Ok()` |
| validator says invalid | `Error(errors)` where each error is `AxisError.ValidationRule(code)` (the FluentValidation `ErrorCode`) |

→ [The `IAxisValidator<T>` contract](iaxisvalidator.md)

---

## See also

- [Getting started](getting-started.md) — install, register, validate
- [Why AxisValidator?](why-axisvalidator.md) — the case for the abstraction
- [Full documentation](README.md) — the map of the whole documentation

---

↩ [Back to AxisValidator docs](README.md)
