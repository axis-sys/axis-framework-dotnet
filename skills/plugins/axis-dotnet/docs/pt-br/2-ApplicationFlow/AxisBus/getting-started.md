# Primeiros passos · instalação e uso

> Instale a abstração e o adapter in-process, registre na DI, defina um evento e um handler, e publique seu primeiro evento em menos de cinco minutos.

---

## Instalação

```
dotnet add package AxisBus           # a abstração
dotnet add package AxisMemoryBus     # adapter in-process
```

`AxisBus` depende apenas de `AxisResult` (via `AxisMediator.Contracts`, pelo tipo de retorno). `AxisMemoryBus` liga o bus in-process e o mediator behaviour de CQRS.

---

## Registrando o adapter

```csharp
using AxisMemoryBus;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAxisMemoryBus();      // IAxisBus → MemoryBusAdapter + scan de handlers
```

`AddAxisMemoryBus()` também chama `services.AddCqrsMediator(Assembly.GetExecutingAssembly())` para que handlers do assembly **chamador** sejam descobertos automaticamente.

---

## Definindo um evento e um handler

```csharp
using Axis;
using AxisMediator.Contracts.CQRS.Events;

public sealed record OrderCreatedEvent(AxisEntityId OrderId, AxisEntityId CustomerId) : IAxisEvent;

public class WarmCustomerCacheHandler(IAxisCache cache) : IAxisEventHandler<OrderCreatedEvent>
{
    public Task<AxisResult> HandleAsync(OrderCreatedEvent @event)
        => cache.RemoveAsync($"customer:{@event.CustomerId}");
}
```

Todo handler retorna um `AxisResult`. Uma falha vira uma entrada agregada no resultado do publish — veja [Publicar · `PublishAsync`](publish.md).

---

## Publicando

```csharp
public Task<AxisResult<CreateOrderResponse>> HandleAsync(CreateOrderCommand cmd)
    => orderFactory.CreateAsync(cmd)
        .ThenAsync(order => bus.PublishAsync(new OrderCreatedEvent(order.OrderId, cmd.CustomerId)))
        .ThenAsync(order => unitOfWork.SaveChangesAsync())
        .MapAsync(order => new CreateOrderResponse { OrderId = order.OrderId });
```

**Por que compensa:** o publish vem *antes* do commit. Com um adapter de outbox o evento é gravado na mesma conexão da mudança de estado, e o único `SaveChangesAsync` commita ambos atomicamente — o estado e o evento aterrissam juntos ou nenhum dos dois. Isso fecha a brecha de dual-write que uma sequência commit-depois-publish deixa aberta (estado commitado, e então o processo cai antes de o evento sair). Com o adapter in-memory, use `PublishAsync` para side effects pós-commit que toleram perda — veja [`PublishAsync`](publish.md).

---

## Veja também

- [O contrato `IAxisBus`](iaxisbus.md) — a porta de publicação
- [Publicar · `PublishAsync`](publish.md) — semântica de fan-out, agregação de erros, tópicos
- [Definindo eventos e handlers](events-and-handlers.md) — modelagem e registro
- [Adapter `AxisMemoryBus`](memory-adapter.md) — a implementação in-process
- [Adapter custom](custom-adapter.md) — Kafka, RabbitMQ, Service Bus
- [Por que AxisBus?](why-axisbus.md) — o argumento por uma porta de um método só

---

↩ [Voltar à documentação do AxisBus](README.md)
