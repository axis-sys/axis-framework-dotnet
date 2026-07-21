# Adapter in-process · `AxisMemoryBus`

> A implementação pronta de `IAxisBus` para fan-out in-process. Registra `IAxisBus` como `MemoryBusAdapter`, scaneia o assembly chamador por handlers e executa em paralelo no publish.

```csharp
using AxisMemoryBus;

services.AddAxisMemoryBus();   // IAxisBus → MemoryBusAdapter + scan de handlers
```

---

## Quando usar

- Um app single-process onde produtores e consumidores vivem juntos.
- Testes onde você quer fan-out sem um broker de verdade.
- Uma camada de staging antes de plugar um adapter distribuído — o código da aplicação fica idêntico.

## Quando *não* usar

| Você quer… | Use no lugar |
|---|---|
| compartilhar eventos entre processos | um adapter distribuído (Kafka, RabbitMQ, Service Bus) |
| sobreviver a restart de processo com eventos em voo | um outbox persistente + adapter distribuído |
| isolar falhas de handlers da transação do publicador | um adapter distribuído com entrega at-least-once |

---

## O que é registrado

`DependencyInjection.AddAxisMemoryBus`:

```csharp
public static IServiceCollection AddAxisMemoryBus(this IServiceCollection services)
{
    services.AddCqrsMediator(Assembly.GetExecutingAssembly());  // descobre handlers no asm chamador
    services.AddScoped<IAxisBus, MemoryBusAdapter>();           // o binding de IAxisBus
    return services;
}
```

- `AddCqrsMediator` scaneia o assembly chamador por implementações de `IAxisEventHandler<>` e registra.
- `IAxisBus` é registrado como **scoped** — um bus por escopo de requisição, compartilhando o `IServiceProvider` do escopo.

Se seus handlers vivem em outro assembly, chame `AddCqrsMediator(typeof(MyHandler).Assembly)` por conta própria antes de `AddAxisMemoryBus`.

---

## Como o `PublishAsync` funciona

Lendo `MemoryBusAdapter.PublishAsync` direto:

```csharp
public async Task<AxisResult> PublishAsync<TEvent>(TEvent @event, params string[] topics)
    where TEvent : IAxisEvent
{
    var handlers = serviceProvider.GetServices<IAxisEventHandler<TEvent>>().ToList();
    if (handlers.Count == 0) return AxisResult.Ok();

    var tasks = handlers.Select(h => h.HandleAsync(@event));
    var results = await Task.WhenAll(tasks);

    return AxisResult.Combine(results);
}
```

- **Zero handlers** → `Ok()`.
- **Vários handlers** → todos disparados concorrentemente com `Task.WhenAll`, resultados combinados com `AxisResult.Combine` (erros agregam, sucesso quando todos `Ok`).
- **Um handler lança** → a exceção escapa do `PublishAsync` (o adapter in-memory *não* captura — embrulhe código de risco no handler com `AxisResult.TryAsync`).
- **Tópicos** → aceitos mas ignorados pelo adapter in-memory; úteis quando você mira uma troca futura para distribuído.

---

## Exemplo real — fiando dois handlers

```csharp
// Program.cs
builder.Services
    .AddAxisMediator()       // mediator
    .AddAxisLogger()
    .AddAxisMemoryBus();     // bus + scan de handlers

// Handlers — descobertos automaticamente
public class WarmCacheHandler(IAxisCache cache) : IAxisEventHandler<OrderCreatedEvent>
{
    public Task<AxisResult> HandleAsync(OrderCreatedEvent @event)
        => cache.RemoveAsync($"customer:{@event.CustomerId}");
}

public class SendEmailHandler(IAxisEmail email) : IAxisEventHandler<OrderCreatedEvent>
{
    public Task<AxisResult> HandleAsync(OrderCreatedEvent @event)
        => email.SendAsync(new OrderConfirmation(@event.OrderId));
}

// publicador
await bus.PublishAsync(new OrderCreatedEvent(orderId, customerId));
```

**Por que compensa:** migrar para um broker distribuído depois é trocar `AddAxisMemoryBus()` por `AddAxisKafkaBus(...)` — os handlers e a chamada de publish ficam iguais. Desenvolvimento local e CI mantêm o caminho leve in-process.

---

## Veja também

- [O contrato `IAxisBus`](iaxisbus.md) — o que todo adapter deve garantir
- [Publicar · `PublishAsync`](publish.md) — a semântica
- [Definindo eventos e handlers](events-and-handlers.md) — como modelar
- [Adapter custom](custom-adapter.md) — escreva um para seu broker

---

↩ [Voltar à documentação do AxisBus](README.md)
