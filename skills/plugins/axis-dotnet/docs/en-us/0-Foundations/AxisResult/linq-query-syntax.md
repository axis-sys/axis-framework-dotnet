# LINQ query syntax

> For those who prefer *comprehension* syntax, AxisResult supports `from … select …`. It's equivalent to fluent chaining — use whichever form reads better for your team.

---

## When to use

When a sequence of dependent steps reads better as a query than as a `Then`/`Map` chain — especially with several named intermediate values.

## When *not* to use

| You want… | Use instead |
|---|---|
| **async** pipelines | the fluent `ThenAsync`/`MapAsync` chain |
| a single transformation step | [`Map`](map.md) directly |

---

## Operators

| Syntax | Fluent equivalent |
|---|---|
| `from x in result select f(x)` | `result.Map(f)` |
| `from x in r1 from y in r2 select …` | `r1.Then(x => r2).Map(…)` |
| `SelectManyAsync` | async LINQ chaining |

---

## Real-world example — comprehension vs. fluent

```csharp
AxisResult<decimal> total =
    from customer in GetCustomer(customerId)
    from order in CreateOrder(customer.Id, productId)
    select order.Total;
```

Equivalent to:

```csharp
var total = GetCustomer(customerId)
    .Then(customer => CreateOrder(customer.Id, productId))
    .Map(order => order.Total);
```

**Why it pays off:** both forms are first-class. Each additional `from` is a failable step (a `Then`), and the final `select` is the `Map`. For async pipelines, prefer the fluent `ThenAsync`/`MapAsync` chain.

---

## See also

- [Chain · `Then`](then.md) — the `from … from …` underneath
- [Transform · `Map`](map.md) — the `select` underneath
- [API reference](api-reference.md) — the full LINQ table

---

↩ [Back to AxisResult docs](README.md)
