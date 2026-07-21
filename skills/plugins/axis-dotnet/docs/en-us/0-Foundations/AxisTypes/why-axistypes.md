# Why AxisTypes? · comparison

> There are other ways to model strongly-typed value objects in C#. This page tells you why AxisTypes is different — a direct comparison, no hand-waving.

---

## vs. raw `string` and `Guid`

The "no library" baseline. Cheap, ubiquitous and, in any non-trivial codebase, a bug magnet.

```csharp
public Task<Customer> GetAsync(string customerId, string tenantId, string countryCode); // 👀
```

Nothing stops the caller from swapping arguments. The compiler is silent, the runtime is silent, and the bug surfaces in production as the wrong customer. **AxisTypes** types each parameter (`AxisEntityId`) and the signature itself rejects the mistake.

## vs. hand-written value objects

You write your own `record struct OrderNumber(string Value)` and then add — by hand — `implicit operator string`, `implicit operator OrderNumber(string?)`, `ToString`, case-insensitive `Equals`, `GetHashCode`, and two `TryParse` overloads. Multiply by every value object you have. **AxisTypes** moves all that boilerplate into a Roslyn source generator: you write the constructor, the generator writes the rest, and every value object behaves identically.

## vs. `Vogen`

`Vogen` is the closest neighbour — a strongly-typed value-object source generator with rich features (validation interfaces, instance methods, EF/JSON converters, configuration). It is **bigger** and **more opinionated** than `AxisTypes`. If you need its breadth, use it. If you want a **small, focused generator** that pairs with the rest of Axis (`AxisResult`, `AxisEntityId`) and ships nothing at runtime, use `AxisTypes`.

## vs. `StronglyTypedId`

`StronglyTypedId` covers exactly one shape: id structs that wrap a primitive (`Guid`, `int`, `long`, `string`). It does not give you the **constructor escape hatch** AxisTypes does — your constructor is the validation, and the generator wraps everything around it. If your value objects are pure ids, both work; if you need parsing or normalisation logic, AxisTypes lets you keep it where it belongs.

## vs. a plain `struct` with manual operators

The DIY option. Same trade-off as "hand-written value objects" above, plus the maintenance burden of keeping `Equals`/`GetHashCode` aligned with the `Value`. **AxisTypes** removes the maintenance burden entirely.

---

## The comparison

| Feature | AxisTypes | Raw `string` | Hand-written | Vogen | StronglyTypedId |
|---|:--:|:--:|:--:|:--:|:--:|
| Compile-time type safety between domain ids | **Yes** | No | Yes | Yes | Yes |
| `implicit operator string` | **Yes** | n/a | manual | Yes | Yes |
| `implicit operator T(string?)` | **Yes** | n/a | manual | Yes | No |
| Case-insensitive `Equals`/`GetHashCode` | **Yes (default)** | No | manual | Configurable | No |
| Invariant-culture toggle | **Yes** | No | manual | Yes | No |
| Non-throwing `TryParse(object?, out T)` | **Yes** | n/a | manual | Yes | No |
| Constructor as the single validation point | **Yes** | No | Yes | Partial | No |
| Ready-made `AxisEntityId` (UUID v7) | **Yes** | No | No | No | No |
| Zero runtime payload (source generator only) | **Yes** | n/a | n/a | Yes | Yes |
| Zero NuGet dependencies | **Yes** | n/a | Yes | Yes | Yes |

---

## See also

- [The `[ValueObject]` generator](value-object-generator.md) — the source generator at the heart of the package
- [Getting started](getting-started.md) — install and write your first value object
- [API reference](api-reference.md) — every type and member in one place

---

↩ [Back to AxisTypes docs](README.md)
