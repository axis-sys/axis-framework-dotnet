# Getting started · installation and usage

> Install the package, use the ready-made identifiers, and declare your first custom value object — the minimum to get off the ground in a few minutes.

---

## Installation

```
dotnet add package AxisTypes                     # ready-made AxisEntityId
dotnet add package AxisTypes.SourceGenerator      # the [ValueObject] attribute + generator, for your own value objects
```

`AxisTypes` ships `AxisEntityId` pre-generated. If you only consume `AxisEntityId`, that's all you need. The `[ValueObject]` attribute itself is not part of that package — it's emitted by the Roslyn generator. To declare a value object **you** write (like `OrderNumber` below), reference `AxisTypes.SourceGenerator` directly — it's a `DevelopmentDependency` (analyzer-only), emitted at compile time and never shipped into your output. That reference is the only prerequisite; you don't need the `AxisTypes` package for it.

---

## Using the ready-made types

```csharp
using Axis;

// AxisEntityId — every entity's id
AxisEntityId personId = AxisEntityId.New;    // UUID v7

// Implicit string roundtrip — for DB columns, headers, JSON
string raw = personId;             // "01927a8b-3c5e-7..."
AxisEntityId back = raw;           // implicit parse
```

> Implicit conversion from `string` is **strict** by design. `AxisEntityId _ = "invalid";` throws — see [Why AxisTypes?](why-axistypes.md).

---

## Declaring a custom value object

```csharp
using AxisTypes.SourceGenerator;

[ValueObject]
public readonly partial record struct OrderNumber
{
    private string Value { get; }

    private OrderNumber(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("OrderNumber cannot be empty");

        Value = value.ToUpperInvariant();
    }
}
```

The generator emits:

```csharp
public readonly partial record struct OrderNumber
{
    public static implicit operator string(OrderNumber value) => value.Value;
    public override string ToString() => Value;
    public static implicit operator OrderNumber(string? value) => new(value);
    public bool Equals(OrderNumber other) => string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);
    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Value);

    public static bool TryParse(object? s) => TryParse(s, out _);
    public static bool TryParse(object? s, out OrderNumber result) { /* ... */ }
}
```

> Want to tweak the property name, disable implicit parse, or switch to invariant-culture equality? See **[The `[ValueObject]` generator](value-object-generator.md)**.

---

## Inspecting and parsing

```csharp
// Roundtrip
var id = AxisEntityId.New;
string asString = id;                  // implicit → string
AxisEntityId again = asString;          // implicit ← string

// Non-throwing parse (useful for HTTP model binding, query strings)
if (AxisEntityId.TryParse(rawInput, out var parsed))
    Console.WriteLine($"Got {parsed}");
else
    Console.WriteLine("Invalid identity");

// Case-insensitive equality
OrderNumber a = "ORD-1";                // implicit parse
OrderNumber b = "ord-1";                // implicit parse
a == b;   // true
```

**Why it pays off:** the constructor runs the validation **once**, when the value enters the domain. After that, the type system itself proves the invariant — you never check `if (string.IsNullOrWhiteSpace(...))` again downstream.

---

## See also

- [The `[ValueObject]` generator](value-object-generator.md) — every option of the attribute and the code it emits
- [`AxisEntityId`](axis-entity-id.md) — UUID v7 typed identifier
- [Why AxisTypes?](why-axistypes.md) — primitive obsession, in detail
- [API reference](api-reference.md) — every type and member in one place

---

↩ [Back to AxisTypes docs](README.md)
