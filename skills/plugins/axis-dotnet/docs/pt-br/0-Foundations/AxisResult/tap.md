# Efeitos · `Tap`

> Executa um efeito colateral (log, métrica, evento) e **devolve o resultado intacto**. `Tap` não muda a trilha nem o valor — ele só observa.

---

## Quando usar

Registrar um log, emitir uma métrica, disparar um evento *fire-and-forget* — sem afetar o que flui no pipeline.

## Quando *não* usar

| Você quer… | Use no lugar |
|---|---|
| um efeito que **pode falhar** e deve curto-circuitar | [`Then`](then.md) |
| transformar o valor | [`Map`](map.md) |

---

## Operadores

| Método | Roda quando | Recebe |
|---|---|---|
| `Tap` / `TapAsync` | **sucesso** | o valor |
| `TapError` / `TapErrorAsync` | **falha** | a lista de `AxisError` |

Todos retornam o resultado original e existem em `Task`/`ValueTask` e [com `CancellationToken`](cancellation.md).

---

## Exemplo real — observabilidade nas duas trilhas

```csharp
return CreateOrderAsync(cmd)
    .TapAsync(order  => logger.LogInformation("Order {OrderId} created", order.OrderId)) // só no sucesso
    .TapErrorAsync(errors => metrics.IncrementFailure(errors[0].Code));                   // só na falha
```

**Por que compensa:** o log de sucesso e a métrica de falha convivem no mesmo pipeline, cada um no seu trilho, **sem** um `if (result.IsSuccess)` para separá-los — e o `order` segue adiante sem ser tocado.

## `Tap` vs `Then` (a diferença que importa)

- `Tap` **ignora** o retorno do efeito e nunca falha a trilha — perfeito para log.
- `Then` **propaga** a falha do efeito — use quando o passo precisa de fato curto-circuitar.

```csharp
.TapAsync(o => auditLog.WriteAsync(o))    // se o log falhar, o pipeline SEGUE
.ThenAsync(o => uow.SaveChangesAsync())   // se o save falhar, o pipeline PARA
```

---

## Veja também

- [Encadear · `Then`](then.md) — quando o efeito precisa poder falhar
- [Remapear erros · `MapError`](map-errors.md) — transformar os erros que o `TapError` observa
- [Sair · `Match`](match.md) — o ramo de falha no fim da trilha

---

↩ [Voltar à documentação do AxisResult](README.md)
