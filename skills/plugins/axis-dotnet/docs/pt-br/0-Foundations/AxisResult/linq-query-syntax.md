# Sintaxe de query LINQ

> Para quem prefere a sintaxe de *comprehension*, o AxisResult suporta `from … select …`. É equivalente ao encadeamento fluente — use a forma que ler melhor para o seu time.

---

## Quando usar

Quando uma sequência de passos que dependem um do outro fica mais legível como query do que como cadeia `Then`/`Map` — especialmente com vários valores intermediários nomeados.

## Quando *não* usar

| Você quer… | Use no lugar |
|---|---|
| pipelines **async** | a cadeia fluente `ThenAsync`/`MapAsync` |
| um único passo de transformação | [`Map`](map.md) direto |

---

## Operadores

| Sintaxe | Equivalente fluente |
|---|---|
| `from x in result select f(x)` | `result.Map(f)` |
| `from x in r1 from y in r2 select …` | `r1.Then(x => r2).Map(…)` |
| `SelectManyAsync` | encadeamento LINQ async |

---

## Exemplo real — comprehension vs. fluente

```csharp
AxisResult<decimal> total =
    from customer in GetCustomer(customerId)
    from order in CreateOrder(customer.Id, productId)
    select order.Total;
```

Equivalente a:

```csharp
var total = GetCustomer(customerId)
    .Then(customer => CreateOrder(customer.Id, productId))
    .Map(order => order.Total);
```

**Por que compensa:** as duas formas são de primeira classe. Cada `from` adicional é um passo falível (um `Then`), e o `select` final é o `Map`. Para pipelines async, prefira a cadeia fluente `ThenAsync`/`MapAsync`.

---

## Veja também

- [Encadear · `Then`](then.md) — o `from … from …` por baixo
- [Transformar · `Map`](map.md) — o `select` por baixo
- [Referência da API](api-reference.md) — a tabela LINQ completa

---

↩ [Voltar à documentação do AxisResult](README.md)
