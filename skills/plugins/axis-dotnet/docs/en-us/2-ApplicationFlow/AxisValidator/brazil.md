# Brazilian validators · `AxisValidator.Brazil`

> A localisation package: a CPF validator, a brazilian cellphone formatter, the `BrazilValidator` static facade for `FormatCellphone` and `ValidateCpf`, plus a `RandomBrazilianDataHelper` for generating test data.

```csharp
using AxisValidator.Brazil;

CpfValidator.Validate("123.456.789-00");              // true / false
CellphoneValidator.Format("11987654321");             // "(11) 98765-4321"
BrazilValidator.ValidateCpf("...");                   // AxisResult<string>
BrazilValidator.FormatCellphone("11987654321");       // AxisResult<string>
```

---

## When to use

Anywhere your code accepts brazilian inputs and you want **typed validation** + **canonical formatting**. The static methods on `BrazilValidator` return an `AxisResult<string>` and plug straight into a pipeline; the underlying validators are pure functions you can call from a `RequiredTryParse` in `AxisValidatorBase`.

## When *not* to use

| You want to… | Use instead |
|---|---|
| validate non-brazilian documents (US SSN, EU IDs) | [American validators](usa.md) or a different localisation package |
| extract details from the document (gender, region) | not the goal of this package — write your own helper |

---

## `CpfValidator`

| Member | Signature | Description |
|---|---|---|
| `Validate(cpf)` | `static bool Validate(string? cpf)` | true if `cpf` is a syntactically + check-digit-valid CPF; rejects the known invalid sequences (e.g. `"00000000000"`, `"12345678909"`) |

Pure function — no allocation beyond a single normalised string of digits.

## `CellphoneValidator`

| Member | Signature | Description |
|---|---|---|
| `Format(cellphone)` | `static string? Format(string? cellphone)` | returns `"(DD) 9NNNN-NNNN"` or `null` if the input cannot be normalised to a valid brazilian mobile |
| `OnlyNumbers(cellphone)` | `static string? OnlyNumbers(string? cellphone)` | normalises a phone string into an 11-digit local format or `null` |
| `TryFormat(cellphone, out formatted)` | `static bool TryFormat(string?, out string?)` | non-throwing companion of `Format` |

## `BrazilValidator`

The static facade that wraps the pure validators into the `AxisResult<string>` railway.

| Member | Signature | Behaviour |
|---|---|---|
| `FormatCellphone(phone)` | `static AxisResult<string> FormatCellphone(string? phone)` | returns `AxisResult.Ok(formatted)` with the canonical `"(DD) 9NNNN-NNNN"`; `AxisError.ValidationRule("CELLPHONE_NUMBER_NULL_OR_NOT_VALID")` for unparseable input |
| `ValidateCpf(document)` | `static AxisResult<string> ValidateCpf(string? document)` | returns `AxisResult.Ok(document)` when the CPF is valid; `AxisError.ValidationRule("DOCUMENT_INVALID")` if the CPF is bad |

## `RandomBrazilianDataHelper`

> Lives in the `AxisValidator.Brazil.Helpers` namespace — a level deeper than `CpfValidator`/`CellphoneValidator`/`BrazilValidator` (all in `AxisValidator.Brazil`). Add `using AxisValidator.Brazil.Helpers;` to use it.

| Member | Signature | Description |
|---|---|---|
| `GenerateCpf(format = false)` | `static string GenerateCpf(bool format = false)` | a valid random CPF (raw or `"123.456.789-09"`) for tests |

---

## Real-world examples

### 1. CPF inside a validator

```csharp
public class CreatePersonValidator : AxisValidatorBase<CreatePersonCommand>
{
    public CreatePersonValidator()
    {
        RequiredTryParse(x => x.Cpf, "PERSON_CPF_INVALID", cpf => CpfValidator.Validate(cpf as string));
    }
}
```

**Why it pays off:** the CPF check rides the same pipeline behaviour as everything else. A bad CPF becomes `AxisError.ValidationRule("PERSON_CPF_INVALID")`, exactly like every other field.

### 2. Cellphone formatting in a pipeline

```csharp
return await uow.InTransactionAsync(() =>
    BrazilValidator.FormatCellphone(input.Phone)        // AxisResult<string>
        .ThenAsync(formatted => contactWriter.UpsertAsync(personId, formatted)));
```

**Why it pays off:** the format rule and the persistence step share the railway. If the phone is invalid, the writer is never called.

### 3. Realistic CPF in unit tests

```csharp
using AxisValidator.Brazil.Helpers;   // RandomBrazilianDataHelper lives one namespace deeper

var fake = RandomBrazilianDataHelper.GenerateCpf();    // raw 11 digits
Assert.True(CpfValidator.Validate(fake));
```

**Why it pays off:** tests do not hard-code a single CPF that may eventually collide with seed data; every run uses a fresh, valid one.

---

## See also

- [Validator base and rules](validator-base.md) — `RequiredTryParse` is the bridge to these validators
- [The `IAxisValidator<T>` contract](iaxisvalidator.md) — what your validator returns

---

↩ [Back to AxisValidator docs](README.md)
