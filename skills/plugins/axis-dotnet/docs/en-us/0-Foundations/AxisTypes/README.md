# AxisTypes — Documentation

> 🌐 [Português (documentação navegável)](../../../pt-br/0-Foundations/AxisTypes/README.md)

**Strongly-typed value objects for C#** — a zero-dependency package plus a Roslyn `[ValueObject]` source generator that gives you implicit conversions, `ToString`, case-insensitive `Equals` and `TryParse` for free, plus the ready-to-use `AxisEntityId` type (UUID v7).

```csharp
// 1) Use a ready-made identity in your domain
var personId = AxisEntityId.New;                         // UUID v7, time-sortable
string raw   = personId;                                 // implicit → "01927a8b-..."
AxisEntityId back = raw;                                 // implicit ← roundtrip

// 2) Or declare your own value object — the generator writes the boilerplate
[ValueObject]
public readonly partial record struct OrderNumber
{
    private string Value { get; }
    private OrderNumber(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("empty");
        Value = value.ToUpperInvariant();
    }
}
```

Use this page as a **map**: read the trunk below (~5 min) and jump straight to the detail of the group you need — without reading hundreds of lines.

---

## The trunk (read first)

### Primitive obsession in 60 seconds

`string customerId` and `string orderId` are the same type. The compiler will not stop you from passing one to the other, from accidentally storing one as the other, from logging the wrong one. **Strongly-typed value objects** make the compiler your ally: `CustomerId` cannot be assigned to `OrderId`, even though both wrap a `string`. → **[Why AxisTypes?](why-axistypes.md)**

### `[ValueObject]` in 60 seconds

Add the attribute to a `partial record struct` (or `partial class`) and the source generator writes:

- `implicit operator string` — for logging, serialisation, DB columns.
- `implicit operator MyType(string?)` — for parsing in from configuration, JSON, HTTP.
- `override ToString()` — returns the wrapped value.
- `bool Equals(other)` and `GetHashCode()` — **case-insensitive** by default (`OrdinalIgnoreCase`).
- `static bool TryParse(object?, out MyType)` — non-throwing parse for HTTP model binding.

You write the **constructor** (the validation). Everything else is generated. → **[The `[ValueObject]` generator](value-object-generator.md)**

### The ready-made type

- **`AxisEntityId`** — every entity's id. UUID v7 (time-sortable), created with `AxisEntityId.New`. → **[`AxisEntityId`](axis-entity-id.md)**

### Installation

```
dotnet add package AxisTypes                     # ready-made AxisEntityId
dotnet add package AxisTypes.SourceGenerator      # the [ValueObject] attribute + generator, for your own value objects
```

`AxisTypes` alone gives you the ready-made `AxisEntityId` (already generated for you) — it does **not** expose the `[ValueObject]` attribute. To declare a value object **you** write, reference the Roslyn generator itself, `AxisTypes.SourceGenerator` (a `DevelopmentDependency`, analyzer-only — it never ships into your output): it emits the `[ValueObject]` attribute as an `internal sealed` type on every compile that references it, so the `AxisTypes` package isn't even a prerequisite for this.

→ Full guide: **[Getting started](getting-started.md)**

---

## The map (jump to what you need)

| Group | You want to… | Detail |
|---|---|---|
| **Generator · `[ValueObject]`** ⭐ | declare a new strongly-typed value object | [value-object-generator.md](value-object-generator.md) |
| **Identity · `AxisEntityId`** | a typed id for every domain entity | [axis-entity-id.md](axis-entity-id.md) |
| **Why?** | the case against primitive obsession | [why-axistypes.md](why-axistypes.md) |
| **Reference** | every type and member at a glance | [api-reference.md](api-reference.md) |

**Start here:** [Getting started](getting-started.md) · [The `[ValueObject]` generator](value-object-generator.md) · [Why AxisTypes?](why-axistypes.md)

**Fundamentals:** [`AxisEntityId`](axis-entity-id.md)

**Reference & extras:** [API reference](api-reference.md)

---

## Design principles

1. **Identifiers are types, not strings.** The compiler refuses to mix a `CustomerId` and an `OrderId`. Primitive obsession is a bug class, not a style preference.
2. **The generator pays the boilerplate tax.** Hand-writing every value object means hand-writing every operator. `[ValueObject]` writes them once, correctly, every time.
3. **Case-insensitive by default.** Identifiers and codes travel through configs, headers and querystrings; `OrdinalIgnoreCase` matches reality.
4. **Time-sortable identifiers.** UUID v7 keeps the lexicographic order aligned with creation time — friendly to indexes, pagination and logs.
5. **No allocations you don't need.** `readonly partial record struct` means the typed wrapper is a value type with structural equality, free `GetHashCode`, and zero heap pressure.

---

## License

Apache 2.0
