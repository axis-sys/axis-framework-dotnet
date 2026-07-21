# Publicar · `PublishAsync`

> Distribui um evento para **cada** `IAxisEventHandler<TEvent>` registrado, executa concorrentemente e combina os resultados. A falha de um handler **não** para os outros — cada erro emerge no mesmo `AxisResult`.

```csharp
var result = await bus.PublishAsync(new OrderCreatedEvent(orderId));

if (result.IsFailure)
    foreach (var error in result.Errors)
        logger.LogWarning("Handler failed: {Code}", error.Code);
```

---

## Quando usar

Sempre que o publicador não precisa de um valor de volta. Fan-out é a ferramenta certa quando "mais de uma coisa pode querer saber disso", ou quando handlers carregam side effects (invalidação de cache, email, atualização de projeção).

## Quando *não* usar

| Você quer… | Use no lugar |
|---|---|
| fazer uma pergunta e obter resposta | `IAxisMediator.QueryAsync` (query CQRS) |
| executar um único comando com resposta tipada | `IAxisMediator.ExecuteAsync` (command CQRS) |
| curto-circuitar na primeira falha | itere os handlers manualmente com `Then`/`Map` |

---

## Tabela de comportamento

Lendo `MemoryBusAdapter.PublishAsync` direto:

| Handlers registrados | Cada handler retorna | `AxisResult` retornado |
|---|---|---|
| zero | n/a | `Ok()` |
| N, todos OK | `Ok()` × N | `Ok()` |
| N, K falham com `Error(errors)` | misto | `Combine`d — só handlers que falharam contribuem com erros |
| handler **lança** | `InvalidOperationException` | a exceção **escapa** do `PublishAsync` |

> **Borda afiada:** um handler que lança (falha não-cooperativa) **não** é pegado pelo adapter in-memory. Embrulhe trabalho de risco em `AxisResult.TryAsync` dentro do handler — ou escreva um adapter que pega e converte.

---

## Tópicos

```csharp
await bus.PublishAsync(@event, "orders", $"tenant:{tenant}");
```

Os `params string[] topics` são **dicas para o adapter**. O adapter in-memory ignora (cada handler é invocado independentemente). Um adapter Kafka/RabbitMQ usa como topic / routing key / partition key — o publicador não precisa saber qual.

---

## Exemplos reais

### 1. Invalidação de cache depois de uma escrita

```csharp
public sealed record CustomerUpdatedEvent(AxisEntityId CustomerId) : IAxisEvent;

public class InvalidateCustomerCacheHandler(IAxisCache cache) : IAxisEventHandler<CustomerUpdatedEvent>
{
    public Task<AxisResult> HandleAsync(CustomerUpdatedEvent @event)
        => cache.RemoveAsync($"customer:{@event.CustomerId}");
}

// publicador
await bus.PublishAsync(new CustomerUpdatedEvent(cmd.CustomerId));
```

**Por que compensa:** o command handler fica focado em persistência; a camada de cache se pluga via eventos. Adicionar um segundo cache (Redis L2) é mais um handler — nada muda no command handler.

### 2. Múltiplos side effects em paralelo

```csharp
// Os três handlers rodam concorrentemente; suas falhas agregam.
public class SendOrderEmailHandler(IAxisEmail email)         : IAxisEventHandler<OrderCreatedEvent> { /* ... */ }
public class WarmOrderProjectionHandler(IAxisCache cache)    : IAxisEventHandler<OrderCreatedEvent> { /* ... */ }
public class UpdateAnalyticsHandler(IAnalyticsPort analytics): IAxisEventHandler<OrderCreatedEvent> { /* ... */ }

// publicador
var result = await bus.PublishAsync(new OrderCreatedEvent(orderId));
```

**Por que compensa:** três side effects, uma chamada de publish, erros agregados. Se o handler de email falha, a projeção ainda esquenta — e o publicador vê os dois desfechos no mesmo `AxisResult`.

### 3. Falha parcial → telemetria, não retry

```csharp
return await bus.PublishAsync(new OrderShippedEvent(orderId))
    .TapErrorAsync(errors => telemetry.RecordFanOutFailure("OrderShipped", errors))
    .Recover(AxisResult.Ok());   // já persistimos; não falhar o comando
```

**Por que compensa:** o publicador registra a falha parcial para observabilidade, depois **recupera** para manter o comando bem-sucedido. O pedido enviado fica commitado; o time de ops vê a degradação do handler.

---

## Veja também

- [O contrato `IAxisBus`](iaxisbus.md) — o que a porta garante
- [Definindo eventos e handlers](events-and-handlers.md) — como escrever
- [Adapter `AxisMemoryBus`](memory-adapter.md) — a implementação de fan-out na caixa
- [Adapter custom](custom-adapter.md) — como um adapter distribuído lida com tópicos

---

↩ [Voltar à documentação do AxisBus](README.md)
