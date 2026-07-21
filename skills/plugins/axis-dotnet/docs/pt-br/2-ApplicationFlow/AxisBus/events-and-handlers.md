# Eventos e handlers · `IAxisEvent`, `IAxisEventHandler<T>`

> Eventos são records que implementam `IAxisEvent`. Handlers são classes que implementam `IAxisEventHandler<TEvent>` e retornam um `AxisResult`. Registre na DI, publique via `IAxisBus`, e o adapter cuida do fan-out.

```csharp
public sealed record CustomerUpdatedEvent(AxisEntityId CustomerId) : IAxisEvent;

public class InvalidateCustomerCacheHandler(IAxisCache cache) : IAxisEventHandler<CustomerUpdatedEvent>
{
    public Task<AxisResult> HandleAsync(CustomerUpdatedEvent @event)
        => cache.RemoveAsync($"customer:{@event.CustomerId}");
}
```

---

## Quando usar

Modele um evento sempre que um *fato* se tornou verdade e outro código pode querer reagir: um pedido foi criado, um pagamento foi confirmado, um tenant foi provisionado. Eventos são **no passado** — eles registram o que já aconteceu.

## Quando *não* usar

| Você quer… | Use no lugar |
|---|---|
| pedir para algo ser feito | um *comando* via `AxisMediator.ExecuteAsync` |
| retornar dados ao publicador | uma *query* via `AxisMediator.QueryAsync` |
| reusar um handler para tipos de evento diferentes | publique um evento base, ou divida em dois handlers |

---

## As duas interfaces

| Tipo | Formato | Propósito |
|---|---|---|
| `IAxisEvent` | `public interface IAxisEvent { string? OrderingKey => null; }` | identifica o payload como evento de bus; `OrderingKey` (padrão `null`) permite optar pela chave de partição FIFO do outbox durável |
| `IAxisEventHandler<TEvent>` | `Task<AxisResult> HandleAsync(TEvent @event)` | um handler por `TEvent` (ou vários — todos rodam) |

Eventos devem ser **records** (ou readonly structs): imutáveis, igualdade por valor, fáceis de logar.

---

## Registro

`AddAxisMemoryBus` chama `services.AddCqrsMediator(Assembly.GetExecutingAssembly())`, que scaneia o **assembly chamador** por implementações de `IAxisEventHandler<>` e registra.

Para handlers em outros assemblies:

```csharp
services.AddCqrsMediator(typeof(MyHandlerInOtherAssembly).Assembly);
services.AddScoped<IAxisEventHandler<MyEvent>, MyHandler>();   // ou registre um por um
```

> O adapter in-memory resolve handlers via `IServiceProvider` por publish — handlers podem ser scoped, transient ou singleton, ao seu gosto.

---

## Exemplos reais

### 1. Um único handler fazendo uma coisa

```csharp
public sealed record InvoicePaidEvent(AxisEntityId InvoiceId) : IAxisEvent;

public class ReleaseGoodsHandler(IShippingPort shipping) : IAxisEventHandler<InvoicePaidEvent>
{
    public Task<AxisResult> HandleAsync(InvoicePaidEvent @event)
        => shipping.ReleaseForInvoiceAsync(@event.InvoiceId);
}
```

**Por que compensa:** o lado do pagamento nunca sabe sobre envio, e o envio reage ao *fato* (fatura paga) sem acoplar de volta ao serviço de pagamento.

### 2. Múltiplos handlers por evento (fan-out)

```csharp
public class WarmProjectionHandler(IAxisCache cache)    : IAxisEventHandler<OrderCreatedEvent> { /* ... */ }
public class SendOrderEmailHandler(IAxisEmail email)    : IAxisEventHandler<OrderCreatedEvent> { /* ... */ }
public class PublishToAnalyticsHandler(IAnalyticsPort a): IAxisEventHandler<OrderCreatedEvent> { /* ... */ }
```

**Por que compensa:** três reações independentes a um evento. Adicionar uma quarta é uma classe — não um refactor.

### 3. Um handler que falha graciosamente

```csharp
public class SendOrderEmailHandler(IAxisEmail email, IAxisLogger logger) : IAxisEventHandler<OrderCreatedEvent>
{
    public Task<AxisResult> HandleAsync(OrderCreatedEvent @event)
        => email.SendAsync(new OrderConfirmationMessage(@event.OrderId))
            .TapErrorAsync(errs => logger.LogWarningAsync("EMAIL_SEND_FAILED", errs))
            .RecoverAsync(AxisResult.Ok());        // recupera para Ok — não falhar o publish por erro de email
}
```

**Por que compensa:** o resultado do publish fica `Ok` para handlers não-críticos, mas o erro é logado e observável. O handler decide localmente se borbulha ou absorve.

---

## Veja também

- [O contrato `IAxisBus`](iaxisbus.md) — a superfície de publicação
- [Publicar · `PublishAsync`](publish.md) — semântica de fan-out e agregação
- [Adapter `AxisMemoryBus`](memory-adapter.md) — registro de handlers

---

↩ [Voltar à documentação do AxisBus](README.md)
