# Passo condicional · `ThenWhen`

> Executa o passo fallível **transformador de mesmo tipo** **somente quando o predicado é verdadeiro**. Quando o predicado é falso — o passo simplesmente não se aplica — o sucesso passa adiante com o valor intacto, e o `next` nunca roda.

---

## Quando usar

Aplicar um passo que só faz sentido para alguns valores, onde o passo **muda o valor** mas não o tipo: "converta o pedido — quando a moeda for estrangeira", "aplique o desconto de boas-vindas — quando for a primeira compra". O predicado lê o valor da trilha e responde "este passo se aplica aqui?".

`ThenWhen` ganha seu lugar quando a condição depende de um valor **nascido no meio da pipeline** (produzido por um passo fallível anterior) — uma condição conhecida na entrada do método fica melhor como guarda simples antes de a pipeline começar.

## Quando *não* usar

| Você quer…                                              | Use no lugar            |
|---------------------------------------------------------|-------------------------|
| **falhar** quando a condição não vale                   | [`Ensure`](ensure.md)   |
| executar o passo incondicionalmente                     | [`Then`](then.md)       |
| executar um **efeito colateral sem valor** a menos que já feito | [`ThenUnless`](then-unless.md) |
| transformar condicionalmente para um **tipo diferente** | [`Then`](then.md) com o branch dentro da lambda |
| recuperar de uma falha quando uma condição casa         | [`RecoverWhen`](recover.md) |

---

## Operadores

| Método | Assinatura | O que faz |
|---|---|---|
| `ThenWhen` | `(Func<T,bool> predicate, Func<T,AxisResult<T>> next)` | predicado false → pass-through; true → executa `next`, cujo resultado **substitui** o da trilha |
| `ThenWhenAsync` | `(Func<T,bool> predicate, Func<T,Task<AxisResult<T>>> next)` | o mesmo, com passo assíncrono |

Definido apenas em `AxisResult<TValue>`, e o `next` retorna o **mesmo** `TValue` — o ramo pass-through devolve o resultado atual, então o tipo não pode mudar. O predicado é sempre síncrono: é roteamento barato sobre um valor já em memória; o trabalho fallível vive no `next`. Variantes `Async` existem para `Task`/`ValueTask` e [com `CancellationToken`](cancellation.md), além dos lifts de extensão sobre `Task<AxisResult<T>>`/`ValueTask<AxisResult<T>>`.

### `ThenWhen` vs `ThenUnless` (o par espelhado)

| | `ThenUnless` | `ThenWhen` |
|---|---|---|
| `next` retorna | `AxisResult` (efeito colateral sem valor) | `AxisResult<T>` (transformação de mesmo tipo) |
| executa `next` quando o predicado é | **false** ("ainda não foi feito") | **true** ("se aplica aqui") |
| no sucesso do `next`, o valor | é **preservado** (o original segue) | é **substituído** (o resultado do next segue) |

---

## Exemplo 1 — conversão cambial só quando estrangeira

A moeda do pedido só é conhecida depois de carregá-lo — a condição depende de um valor nascido no meio da pipeline:

```csharp
return orders.GetByIdAsync(command.OrderId)                    // AxisResult<Order> nasce aqui
    .ThenWhenAsync(
        order => order.Currency != settlement.Currency,        // doméstico → nada a converter
        order => fx.ConvertAsync(order, settlement.Currency))  // fallível; SUBSTITUI o pedido
    .ThenAsync(order => payments.CaptureAsync(order));         // roda nos DOIS ramos
```

**Por que compensa:** o ternário dentro da lambda — `order.Currency != settlement.Currency ? fx.ConvertAsync(...) : AxisResult.Ok(order).AsTaskAsync()` — desaparece, e a intenção "se aplica aqui?" vira um passo declarativo e nomeado.

## Exemplo 2 — enriquecimento condicional gravado no valor

```csharp
return carts.GetByIdAsync(command.CartId)
    .ThenAsync(cart => pricing.PriceAsync(cart))               // Quote nasce aqui
    .ThenWhenAsync(
        quote => quote.UsesLoyaltyPoints,
        quote => loyalty.ReservePointsAsync(quote.CustomerId, quote.PointsToBurn)
            .MapAsync(reservation => quote with { PointsReservationId = reservation.Id }))
    .ThenAsync(quote => orders.SubmitAsync(quote));
```

**Por que compensa:** o passo condicional é fallível *e* grava algo novo no valor da trilha — exatamente a combinação que nem `ThenUnless` (descarta o resultado do `next`) nem `Ensure` (só pode falhar) expressa.

---

## Veja também

- [Encadear · `Then`](then.md) — o passo incondicional
- [Guarda condicional · `ThenUnless`](then-unless.md) — o espelho: efeito colateral sem valor, roda quando o predicado é **false**
- [Garantir · `Ensure`](ensure.md) — condição falha → a trilha falha
- [Cancelamento](cancellation.md) — os overloads com `CancellationToken`

---

↩ [Voltar aos docs do AxisResult](README.md)
