# API reference

> The complete catalogue, grouped by responsibility. Use it for lookup — each group links back to its detail page.

---

## The generator attribute

| Member | Default | Description |
|---|---|---|
| `[ValueObject]` | — | annotate a `partial record struct`, `partial struct`, `partial record` or `partial class` to emit the conversion / equality / parse helpers |
| `PropertyName` | `"Value"` | the name of the wrapped property the generator reads when emitting `ToString`/`Equals` |
| `ImplicitFromString` | `true` | emit `implicit operator MyType(string?)` |
| `TryParse` | `true` | emit `TryParse(object?)` and `TryParse(object?, out MyType)` |
| `CaseInsensitiveEquals` | `true` | emit case-insensitive `Equals` and `GetHashCode` |
| `UseInvariantCulture` | `false` | switch case-insensitive equality to `InvariantCultureIgnoreCase` |

→ [The `[ValueObject]` generator](value-object-generator.md)

---

## Members emitted by the generator

| Member | Signature | When |
|---|---|---|
| Implicit string conversion | `static implicit operator string(MyType)` | always |
| `ToString` | `override string ToString()` | always |
| Implicit parse | `static implicit operator MyType(string?)` | `ImplicitFromString = true` |
| Case-insensitive `Equals` | `bool Equals(MyType)` (struct) or `virtual bool Equals(MyType?)` (class) | `CaseInsensitiveEquals = true` |
| `GetHashCode` | `override int GetHashCode()` | `CaseInsensitiveEquals = true` |
| `TryParse` | `static bool TryParse(object?)` | `TryParse = true` |
| `TryParse` (out) | `static bool TryParse(object?, out MyType)` | `TryParse = true` |

→ [The `[ValueObject]` generator](value-object-generator.md)

---

## `AxisEntityId`

| Member | Signature | Description |
|---|---|---|
| `New` | `static AxisEntityId New { get; }` | mints a fresh UUID v7 |
| `ToString()` | `string` | the UUID string |
| `implicit operator string` | (generated) | for storage and serialisation |
| `implicit operator AxisEntityId(string?)` | (generated) | strict parse |
| `TryParse` | (generated) | non-throwing parse |
| `Equals` / `GetHashCode` | (generated, case-insensitive) | structural equality |

→ [`AxisEntityId`](axis-entity-id.md)

---

## See also

- [Getting started](getting-started.md) — install and use
- [Why AxisTypes?](why-axistypes.md) — the case for typed value objects
- [Full documentation](README.md) — the map of the whole documentation

---

↩ [Back to AxisTypes docs](README.md)
