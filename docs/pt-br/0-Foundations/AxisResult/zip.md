# Combinar · `Zip`

> Junta **valores diferentes** numa tupla, para usá-los juntos depois. Cada `Zip` adiciona um valor; se qualquer um falhar, a tupla inteira curto-circuita.

---

## Quando usar

Você precisa de 2 a 4 valores de operações distintas para montar um resultado (um dashboard, um agregado de leitura).

## Quando *não* usar

| Você quer… | Use no lugar |
|---|---|
| reduzir **N** resultados do **mesmo** tipo a um | [`Combine`/`All`](aggregate.md) |
| substituir o valor (não acumular) | [`Then`](then.md) |

---

## Operadores

| Método | Semântica |
|---|---|
| `Zip` / `ZipAsync` | **sequencial**, *fail-fast*; constrói `(T1, T2)` … até `(T1, T2, T3, T4)` |
| `ZipParallelAsync` | roda os dois lados **concorrentes**; se ambos falham, **acumula** os erros |
| `ThenAsync((a, b) => …)` | **side effect** falível no meio da tupla (persistir, invalidar) — preserva a tupla inteira |
| `Map((a, b) => …)` | desestrutura a tupla no fim — sem `.Value1` / `.Value2` |

> **Spread, sempre.** Em um trilho de tupla, escreva a lambda com **um parâmetro por elemento** — é isso que seleciona a sobrecarga de tupla. Uma lambda de um parâmetro só casa com a sobrecarga de valor único e **aninha** a tupla (`((T1, T2), T3)`) — é assim que o `zip.Value1.Value2` aparece.

---

## Exemplo 1 — montar um dashboard (sequencial)

```csharp
var dashboard = await GetUserAsync(userId)
    .ZipAsync(user    => GetAccountAsync(user.AccountId))   // (User, Account)
    .ZipAsync((u, ac) => GetPlanAsync(ac.PlanId))           // (User, Account, Plan)
    .MapAsync((user, account, plan) => new DashboardResponse
    {
        UserName       = user.Name,
        AccountBalance = account.Balance,
        PlanName       = plan.Name
    });
```

**Por que compensa:** os três valores chegam juntos no `MapAsync` final, desestruturados com nome — e qualquer passo que falhe interrompe a cadeia, sem aninhamento.

## Exemplo 2 — operações independentes (paralelo)

Quando os dois lados **não dependem** um do outro, rode-os concorrentes:

```csharp
var dashboard = await GetUserAsync(userId)
    .ZipParallelAsync(() => GetRecentOrdersAsync(userId))   // roda em paralelo
    .MapAsync((user, orders) => new DashboardResponse
    {
        UserName   = user.Name,
        OrderCount = orders.Count
    });
```

**Por que compensa:** `ZipParallelAsync` usa `Task.WhenAll` por baixo — os dois *fetches* acontecem ao mesmo tempo; e se **os dois** falharem, você vê **todos** os erros de uma vez, não só o primeiro.

## Exemplo 3 — side effect no meio da tupla (rotação de token)

Um passo falível (persistir) entre dois `Zip`s, com a tupla sobrevivendo a ele:

```csharp
public Task<AxisResult<TokenResponse>> HandleAsync(RefreshTokenCommand command)
    => refreshTokens.ConsumeByHashAsync(hash)                                     // AxisResult<Consumed>
        .ZipAsync(consumed => issuer.Issue(consumed.User, consumed.Trusted))      // (consumed, next)
        .ThenAsync((consumed, next) => refreshTokens.AddAsync(next.Properties))   // persiste; a tupla sobrevive
        .ZipAsync((consumed, next) => jwt.IssueToken(consumed.User))              // (consumed, next, access)
        .MapAsync((consumed, next, access) => new TokenResponse
        {
            AccessToken  = access.Token,
            RefreshToken = next.Plain,
            UserId       = consumed.User.UserId,
        });
```

**Por que compensa:** cada valor é nomeado uma vez e continua nomeado — o `ThenAsync` *spread* sem valor executa a persistência sem colapsar a tupla, e o `MapAsync` final ainda vê os três pelo nome.

---

## Veja também

- [Transformar · `Map`](map.md) — desestruturar a tupla com `(a, b) => …`
- [Agregar · `Combine`/`All`](aggregate.md) — para N resultados do mesmo tipo
- [`Task` vs `ValueTask`](async-task-vs-valuetask.md) — escolha do async no `ZipParallelAsync`

---

↩ [Voltar à documentação do AxisResult](README.md)
