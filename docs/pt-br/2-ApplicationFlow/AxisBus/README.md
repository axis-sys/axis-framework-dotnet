# AxisBus — Documentação

> 🌐 [English (README principal)](../../../en-us/2-ApplicationFlow/AxisBus/README.md)

**Uma porta de event bus de um método só** — `IAxisBus.PublishAsync<TEvent>(@event, params topics)` retornando um `AxisResult` que **agrega toda falha de handler**. Use `AxisMemoryBus` para broadcast in-process; troque por um adapter Kafka, RabbitMQ, Service Bus ou de outbox transacional sem tocar no código da aplicação.

```csharp
public Task<AxisResult<CreateOrderResponse>> HandleAsync(CreateOrderCommand cmd)
    => orderFactory.CreateAsync(cmd)
        .ThenAsync(order => bus.PublishAsync(new OrderCreatedEvent(order.OrderId)))   // enfileira no outbox…
        .ThenAsync(order => unitOfWork.SaveChangesAsync())                            // …e então um commit persiste o pedido E o evento
        .MapAsync(order => new CreateOrderResponse { OrderId = order.OrderId });
```

> **O evento nunca sai do unit of work.** Com um adapter de outbox (veja [Adapter custom](custom-adapter.md)), `PublishAsync` enfileira o evento na conexão do unit of work *antes* do commit — o único `SaveChangesAsync` então persiste a mudança de estado e o evento juntos, atomicamente, ou nenhum. Não há dual-write commit-depois-publish, então o race clássico — estado commitado mas evento perdido, ou evento publicado para uma mudança de estado que depois dá rollback — não pode acontecer. Fan-out in-memory para side effects tolerantes (cache, email) é um papel *separado* → veja [`PublishAsync`](publish.md).

Use esta página como **mapa**: leia o tronco abaixo (~5 min) e salte direto para o detalhe do grupo que você precisa — sem ler centenas de linhas.

---

## O tronco (leia primeiro)

### A interface em 60 segundos

```csharp
public interface IAxisBus
{
    Task<AxisResult> PublishAsync<TEvent>(TEvent @event, params string[] topics)
        where TEvent : IAxisEvent;
}
```

Um método. O bus distribui o evento para cada `IAxisEventHandler<TEvent>` registrado, executa todos **em paralelo**, e agrega os resultados com `AxisResult.Combine` — erros de cada handler emergem juntos. Detalhes de roteamento (tópicos, partition keys, exchanges) são problema do **adapter**, não do chamador. → **[O contrato `IAxisBus`](iaxisbus.md)**

### Eventos e handlers

Os contratos vêm do `AxisMediator`:

```csharp
public interface IAxisEvent
{
    string? OrderingKey => null;   // opcional; chave de partição FIFO do outbox durável
}

public interface IAxisEventHandler<in TEvent> where TEvent : IAxisEvent
{
    Task<AxisResult> HandleAsync(TEvent @event);
}
```

Defina um record que implementa `IAxisEvent`, escreva um handler para ele, registre na DI, e o bus já encontra. → **[Definindo eventos e handlers](events-and-handlers.md)**

### Adapter in-memory

`AxisMemoryBus` registra `IAxisBus` em cima de handlers in-process, executa em paralelo e agrega os resultados:

```csharp
services.AddAxisMemoryBus();   // liga IAxisBus → MemoryBusAdapter + scaneia handlers
```

→ **[Adapter `AxisMemoryBus`](memory-adapter.md)**

### Adapter de outbox durável (na caixa)

`AxisBus.Repository` entrega um outbox transacional pronto para produção sobre Postgres/MySQL: publicar enfileira no unit of work, um commit drena atomicamente junto com o estado do negócio, e um dispatcher em background entrega depois do commit — sem coluna de status, claim-by-lease, at-least-once:

```csharp
services.AddAxisBusPostgres(new AxisBusRepositorySettings { ConnectionString = "..." });   // ou AddAxisBusMySql
```

→ **[Referência da API](api-reference.md)** (veja "Adapter de outbox durável")

### Instalação

```
dotnet add package AxisBus           # a abstração (depende de AxisResult)
dotnet add package AxisMemoryBus     # adapter in-process
```

→ Guia completo: **[Primeiros passos](getting-started.md)**

---

## O mapa (salte para o que precisa)

| Grupo | Você quer… | Detalhe |
|---|---|---|
| **Contrato · `IAxisBus`** | a porta de publicação e sua semântica | [iaxisbus.md](iaxisbus.md) |
| **Publicar · `PublishAsync`** ⭐ | distribuir um evento para cada handler | [publish.md](publish.md) |
| **Eventos · `IAxisEvent`** | defina eventos e escreva handlers | [events-and-handlers.md](events-and-handlers.md) |
| **In-process · `AxisMemoryBus`** | o adapter pronto | [memory-adapter.md](memory-adapter.md) |
| **Outbox durável · `AxisBus.Repository`** | o adapter de outbox transacional da caixa (Postgres/MySQL) | [api-reference.md](api-reference.md) |
| **Adapter custom** | escreva o seu (Kafka, RabbitMQ, Service Bus) | [custom-adapter.md](custom-adapter.md) |
| **Por quê?** | o argumento por uma porta de um método só | [why-axisbus.md](why-axisbus.md) |
| **Referência** | cada membro num só lugar | [api-reference.md](api-reference.md) |

**Comece aqui:** [Primeiros passos](getting-started.md) · [O contrato `IAxisBus`](iaxisbus.md) · [Por que AxisBus?](why-axisbus.md)

**Fundamentos:** [Publicar · `PublishAsync`](publish.md) · [Definindo eventos e handlers](events-and-handlers.md) · [Adapter `AxisMemoryBus`](memory-adapter.md)

**Referência e extras:** [Adapter custom](custom-adapter.md) · [Referência da API](api-reference.md)

---

## Princípios de design

1. **Um método.** Uma porta só de publicação mantém a aplicação fora do vocabulário do transporte. Subscrições vivem no adapter ou no broker, não na abstração.
2. **Erros agregam, não derrubam.** A falha de cada handler vira uma entrada no `AxisResult` combinado. O publicador vê o quadro *inteiro*, não a primeira coisa que estourou.
3. **Handlers rodam em paralelo.** O adapter in-memory espera todos concorrentemente. Side effects precisam ser independentes.
4. **Roteamento pertence ao adapter.** Tópicos, partition keys e exchanges são conceitos do fornecedor. A porta aceita um `params string[] topics` para chamadores darem dicas, mas interpretar é do adapter.
5. **Eventos são records bobos.** `IAxisEvent` é uma marker interface. Adicione os campos que os consumidores precisam; nada mais.
6. **Eventos de domínio andam junto com o unit of work.** Um evento de domínio é enfileirado *dentro* da transação que persiste a mudança de estado — um adapter de outbox grava a linha do evento na mesma conexão, e o commit torna ambos duráveis atomicamente. Nunca `SaveChanges` primeiro e `Publish` depois: esse dual-write pode commitar o estado e perder o evento. Fan-out in-memory fica reservado para side effects pós-commit que toleram perda.

---

## Licença

Apache 2.0
