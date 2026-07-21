# American validators · `AxisValidator.Usa`

> A localisation package: an SSN validator, a U.S. (NANP) phone formatter, the `UsaValidator` static facade for `FormatPhone` and `ValidateSsn`, plus a `RandomUsaDataHelper` for generating test data.

```csharp
using AxisValidator.Usa;

SsnValidator.Validate("512-45-6789");                 // true / false
PhoneValidator.Format("2125551234");                  // "(212) 555-1234"
UsaValidator.ValidateSsn("...");                       // AxisResult<string>
UsaValidator.FormatPhone("2125551234");                // AxisResult<string>
```

---

## When to use

Anywhere your code accepts U.S. inputs and you want **typed validation** + **canonical formatting**. The static methods on `UsaValidator` return an `AxisResult<string>` and plug straight into a pipeline; the underlying validators are pure functions you can call from a `RequiredTryParse` in `AxisValidatorBase`.

## When *not* to use

| You want to… | Use instead |
|---|---|
| validate non-american documents (brazilian CPF, EU IDs) | [Brazilian validators](brazil.md) or a different localisation package |
| extract details from the document (issuing state, entity type) | not the goal of this package — write your own helper |

---

## `SsnValidator`

| Member | Signature | Description |
|---|---|---|
| `Validate(ssn)` | `static bool Validate(string? ssn)` | true if `ssn` is a syntactically valid 9-digit SSN; rejects structurally invalid area/group/serial ranges and the known-invalid sequences (e.g. `"000000000"`, `"666123456"`, `"078051120"`) |

Unlike a CPF, an SSN has **no public check-digit algorithm** — the SSA never released one. `Validate` is a structural check (area `!= 000/666` and `< 900`, group `!= 00`, serial `!= 0000`) plus a known-invalid blocklist, not a checksum. It cannot tell you the SSN was actually issued.

## `PhoneValidator`

| Member | Signature | Description |
|---|---|---|
| `Format(phone)` | `static string? Format(string? phone)` | returns `"(NPA) NXX-XXXX"` or `null` if the input cannot be normalised to a valid NANP number |
| `OnlyNumbers(phone)` | `static string? OnlyNumbers(string? phone)` | normalises a phone string into a 10-digit local format or `null` |
| `TryFormat(phone, out formatted)` | `static bool TryFormat(string?, out string?)` | non-throwing companion of `Format` |

Follows the North American Numbering Plan: 10 digits, area code and exchange code both starting with `2`-`9`; a leading country code `1` (11 digits total) is stripped automatically.

## `UsaValidator`

The static facade that wraps the pure validators into the `AxisResult<string>` railway.

| Member | Signature | Behaviour |
|---|---|---|
| `FormatPhone(phone)` | `static AxisResult<string> FormatPhone(string? phone)` | returns `AxisResult.Ok(formatted)` with the canonical `"(NPA) NXX-XXXX"`; `AxisError.ValidationRule("PHONE_NUMBER_NULL_OR_NOT_VALID")` for unparseable input |
| `ValidateSsn(document)` | `static AxisResult<string> ValidateSsn(string? document)` | returns `AxisResult.Ok(document)` when the SSN is structurally valid; `AxisError.ValidationRule("DOCUMENT_INVALID")` if it is bad |

## `RandomUsaDataHelper`

> Lives in the `AxisValidator.Usa.Helpers` namespace — a level deeper than `SsnValidator`/`PhoneValidator`/`UsaValidator` (all in `AxisValidator.Usa`). Add `using AxisValidator.Usa.Helpers;` to use it.

| Member | Signature | Description |
|---|---|---|
| `GenerateSsn(format = false)` | `static string GenerateSsn(bool format = false)` | a structurally valid random SSN (raw or `"512-45-6789"`) for tests |

---

## Real-world examples

### 1. SSN inside a validator

```csharp
public class CreatePersonValidator : AxisValidatorBase<CreatePersonCommand>
{
    public CreatePersonValidator()
    {
        RequiredTryParse(x => x.Ssn, "PERSON_SSN_INVALID", ssn => SsnValidator.Validate(ssn as string));
    }
}
```

**Why it pays off:** the SSN check rides the same pipeline behaviour as everything else. A bad SSN becomes `AxisError.ValidationRule("PERSON_SSN_INVALID")`, exactly like every other field.

### 2. Phone formatting in a pipeline

```csharp
return await uow.InTransactionAsync(() =>
    UsaValidator.FormatPhone(input.Phone)                // AxisResult<string>
        .ThenAsync(formatted => contactWriter.UpsertAsync(personId, formatted)));
```

**Why it pays off:** the format rule and the persistence step share the railway. If the phone is invalid, the writer is never called.

### 3. Realistic SSN in unit tests

```csharp
using AxisValidator.Usa.Helpers;   // RandomUsaDataHelper lives one namespace deeper

var fake = RandomUsaDataHelper.GenerateSsn();    // raw 9 digits
Assert.True(SsnValidator.Validate(fake));
```

**Why it pays off:** tests do not hard-code a single SSN that may eventually collide with seed data; every run uses a fresh, structurally valid one.

---

## See also

- [Validator base and rules](validator-base.md) — `RequiredTryParse` is the bridge to these validators
- [The `IAxisValidator<T>` contract](iaxisvalidator.md) — what your validator returns
- [Brazilian validators](brazil.md) — the other localisation pack, same shape

---

↩ [Back to AxisValidator docs](README.md)
