# The `[ValueObject]` generator · `ValueObjectAttribute`

> Annotate a `partial record struct`, `partial struct`, `partial record` or `partial class` with `[ValueObject]` and the Roslyn source generator writes all the conversion, equality and parsing boilerplate. You write the constructor; the generator writes everything else.

```csharp
using AxisTypes.SourceGenerator;

[ValueObject]
public readonly partial record struct InvoiceNumber
{
    private string Value { get; }

    private InvoiceNumber(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("InvoiceNumber cannot be empty");

        Value = value;
    }
}
```

---

## When to use

Whenever a `string` (or any other primitive) is **not** the right type for a domain concept. Customer ids, order numbers, country codes, document numbers, currency codes, e-mail addresses, telephone numbers — anything you want the compiler to track.

## When *not* to use

| You want to… | Use instead |
|---|---|
| represent a multi-field domain concept (price = amount + currency) | a hand-written `readonly record struct` |
| build an enum-like fixed set | a real `enum` or a `Smart Enum` library |
| validate without throwing | call `TryParse`; do not throw inside the constructor for non-domain inputs |

---

## What gets generated

Reading `ValueObjectGenerator.cs` directly, this is what the generator emits for each type:

| Member | Always | Controlled by |
|---|:--:|---|
| `static implicit operator string(MyType value)` | **Yes** | — |
| `override string ToString()` | **Yes** | — |
| `static implicit operator MyType(string? value)` | optional | `ImplicitFromString` (default `true`) |
| `bool Equals(MyType other)` (case-insensitive) | optional | `CaseInsensitiveEquals` (default `true`) |
| `override int GetHashCode()` (case-insensitive) | optional | `CaseInsensitiveEquals` (default `true`) |
| `static bool TryParse(object?)` and `TryParse(object?, out MyType)` | optional | `TryParse` (default `true`) |

The case-insensitive variant uses `OrdinalIgnoreCase` by default and switches to `InvariantCultureIgnoreCase` when `UseInvariantCulture = true`.

---

## The five options

| Option | Type | Default | Meaning |
|---|---|---|---|
| `PropertyName` | `string` | `"Value"` | the name of the wrapped property the generator reads when emitting `ToString`, `Equals`, etc. Change it when your value object exposes the wrapped data under a different name (e.g. `Code`). |
| `ImplicitFromString` | `bool` | `true` | when `true`, emits `implicit operator MyType(string?)` so `MyType x = "abc"` works. Disable when the type only accepts construction via a factory. |
| `TryParse` | `bool` | `true` | emits the two `TryParse` overloads. Disable if you implement parsing yourself. |
| `CaseInsensitiveEquals` | `bool` | `true` | emits case-insensitive `Equals` + `GetHashCode`. Disable for case-sensitive identifiers (e.g. cryptographic hashes). |
| `UseInvariantCulture` | `bool` | `false` | when case-insensitive equality is on, switches `OrdinalIgnoreCase` → `InvariantCultureIgnoreCase`. Use for human-language tokens where locale-aware folding matters. |

```csharp
// Strict, case-sensitive, no implicit parse — for a cryptographic token
[ValueObject(
    ImplicitFromString    = false,
    CaseInsensitiveEquals = false,
    TryParse              = false)]
public readonly partial record struct ApiKey
{
    private string Value { get; }
    private ApiKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("empty");
        Value = value;
    }

    public static ApiKey FromBase64(string b64) => new(b64); // explicit factory
}
```

---

## Real-world example — typed order number with validation

```csharp
[ValueObject]
public readonly partial record struct OrderNumber
{
    private string Value { get; }

    private OrderNumber(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("OrderNumber cannot be empty");

        if (value.Length is < 6 or > 32)
            throw new ArgumentException("OrderNumber length must be 6..32");

        Value = value.ToUpperInvariant();
    }
}

// Usage
OrderNumber n = "ord-12345";              // implicit parse → uppercased internally
string column   = n;                       // implicit to string → "ORD-12345"
OrderNumber other = "ORD-12345";           // implicit parse
bool sameOrder  = n == other;              // true (case-insensitive)

// HTTP model binding — non-throwing
if (OrderNumber.TryParse(rawQueryString, out var orderNumber))
    return await GetByOrderAsync(orderNumber);
```

**Why it pays off:** every entry point (constructor, implicit cast, `TryParse`) routes through the **same** validation. The compiler refuses `string` where `OrderNumber` is expected, and downstream code never re-validates.

---

## See also

- [`AxisEntityId`](axis-entity-id.md) — the canonical example of a typed identifier
- [Why AxisTypes?](why-axistypes.md) — what the generator buys you over hand-written code

---

↩ [Back to AxisTypes docs](README.md)
