# `Task` vs `ValueTask` · qual async usar

> Todo método async do AxisResult tem duas variantes: `Task` e `ValueTask`. **Na dúvida, use `Task`.** Esta página diz exatamente quando o `ValueTask` compensa — porque nem todo dev tem isso na ponta da língua.

---

## A regra curta

- **Padrão: `Task`.** É seguro: pode ser aguardado (`await`) mais de uma vez, combinado em `Task.WhenAll`, e armazenado.
- **`ValueTask`** só quando o método **frequentemente completa de forma síncrona** (ex.: *cache hit*, valor já em memória) **e** está num *hot path* comprovado por *profiling*. Ganho: **zero alocação** no caminho síncrono.

## Quando *não* usar `ValueTask`

Um `ValueTask` mal usado é **pior** que um `Task`. Não use se você for:

| Situação | Por quê |
|---|---|
| aguardar (`await`) **mais de uma vez** o mesmo valor | um `ValueTask` só pode ser consumido uma vez |
| **armazenar** em campo/propriedade para usar depois | mesma razão — ele não foi feito para guardar |
| passar para **`Task.WhenAll` / `WhenAny`** | exige `.AsTask()` antes (e aí você perde o ganho) |
| você **não mediu** que aquele caminho é quente | otimização prematura; comece com `Task` |

---

## Por que a lib oferece os dois

O AxisResult expõe `ThenAsync`, `MapAsync`, `TapAsync`, etc. para **ambos** — e o pipeline é **idêntico**:

```csharp
// Task (padrão)
public Task<AxisResult<User>> GetUserAsync(UserId id) => ...;

// ValueTask (hot path: zero alocação quando completa síncrono)
public ValueTask<AxisResult<User>> GetUserAsync(UserId id) => ...;
```

Toda composição (`ThenAsync`, `MapAsync`, `TapAsync`…) funciona igual nos dois. Você começa com `Task` e migra apenas os pontos quentes para `ValueTask` **sem reescrever** o pipeline.

**Por que compensa:** num resolver de cache que acerta 95% das vezes de forma síncrona, o `ValueTask` elimina a alocação do `Task` em quase todas as chamadas — sem mudar uma linha da lógica de composição acima.

> Referência: o comportamento zero-alocação do `ValueTask<T>` em conclusões síncronas é documentado pela equipe .NET — ver Stephen Toub, [*Understanding the Whys, Whats, and Whens of ValueTask*](https://devblogs.microsoft.com/dotnet/understanding-the-whys-whats-and-whens-of-valuetask/).

---

## Veja também

- [Encadear · `Then`](then.md) — o pipeline que ganha as variantes async
- [Cancelamento](cancellation.md) — as variantes com `CancellationToken`

---

↩ [Voltar à documentação do AxisResult](README.md)
