# Identity · `AxisEntityId`

> A typed identifier for every domain entity, encoded as a **UUID v7**. UUID v7 means the id is **time-sortable**, which is friendly to indexes, paging and logs.

```csharp
var personId = AxisEntityId.New;   // "01927a8b-3c5e-7..."

string column   = personId;       // implicit → "01927a8b-3c5e-7..."
AxisEntityId back = column;       // implicit ← roundtrip
```

---

## When to use

Every persisted entity's primary key. Anywhere you'd otherwise type `Guid Id { get; set; }`.

## When *not* to use

| You want to… | Use instead |
|---|---|
| build a **custom** typed string id with your own format | the [`[ValueObject]` generator](value-object-generator.md) |

---

## Anatomy of the value

```
01927a8b-3c5e-7c63-8ff0-9da76e5db0a3
└─ UUID v7 (time-sortable; the first bytes encode a millisecond timestamp)
```

The constructor enforces every part of that layout. Invalid inputs throw at construction time and `TryParse` returns `false`.

| Validation | Behaviour |
|---|---|
| `null` or whitespace | `ArgumentNullException` |
| not a valid GUID | `ArgumentException` |
| GUID is not a UUID v7 | `ArgumentException` |

---

## Members

| Member | Signature | Description |
|---|---|---|
| `New` | `static AxisEntityId New { get; }` | mints a fresh UUID v7 |
| `ToString()` | `string` (generated) | the UUID string |
| `implicit operator string` | (generated) | for DB columns, headers, JSON serialisation |
| `implicit operator AxisEntityId(string?)` | (generated) | strict parse — throws on invalid input |
| `TryParse(object?)` | (generated) | non-throwing parse — returns `true`/`false` without yielding the value |
| `TryParse(object?, out AxisEntityId)` | (generated) | non-throwing parse — returns `false` on invalid input |
| `Equals` / `GetHashCode` | (generated) | case-insensitive (`OrdinalIgnoreCase`) |

---

## Real-world examples

### 1. Persisting an entity

```csharp
public class Person
{
    public AxisEntityId PersonId { get; private set; }     // typed primary key

    public Person()                                        // EF Core ctor
    { }

    public Person(AxisEntityId personId)
    {
        PersonId = personId;
    }
}

// EF Core mapping (a string column, thanks to the implicit operator)
modelBuilder.Entity<Person>()
    .Property(p => p.PersonId)
    .HasConversion(id => (string)id, value => value);     // round-trip via string
```

**Why it pays off:** the column is a `string`, the property is `AxisEntityId`, and the implicit conversion glues them together. Your code reads typed; the storage reads stable. UUID v7 keeps inserts at the end of the B-tree.

### 2. HTTP route parameter (non-throwing parse)

```csharp
app.MapGet("/people/{id}", (string id, IPersonService svc) =>
{
    if (!AxisEntityId.TryParse(id, out var personId))
        return Results.BadRequest("INVALID_PERSON_ID");

    return svc.GetByIdAsync(personId);
});
```

**Why it pays off:** the parser refuses malformed ids **before** any database call. Inside the handler, the id is already typed — `IPersonService.GetByIdAsync(AxisEntityId)` cannot be called with the wrong thing.

---

## See also

- [The `[ValueObject]` generator](value-object-generator.md) — how the conversion and parsing helpers are generated
- [Why AxisTypes?](why-axistypes.md) — why typed identifiers beat `Guid` and `string`

---

↩ [Back to AxisTypes docs](README.md)
