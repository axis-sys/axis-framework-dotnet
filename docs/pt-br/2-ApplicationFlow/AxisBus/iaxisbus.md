# Contrato · `IAxisBus`

> Uma porta de publicação de um método só. Detalhes de roteamento (tópicos, exchanges, partition keys) são preocupações do adapter, não do contrato. Chamadores pensam em *eventos* e *handlers*, não em transportes.

```csharp
public interface IAxisBus
{
    Task<AxisResult> PublishAsync<TEvent>(TEvent @event, params string[] topics)
        where TEvent : IAxisEvent;
}
```

---

## Quando usar

Em qualquer lugar onde seu código executa uma ação a que outras partes do sistema **podem querer reagir** sem acoplamento: pedido criado, cliente atualizado, fatura paga. Publique primeiro, reaja em handlers; o publicador não sabe quem escuta.

## Quando *não* usar

| Você quer… | Use no lugar |
|---|---|
| enviar um **comando** (destinatário único, espera resposta) | `AxisMediator` |
| disparar um único side effect in-process amarrado ao publicador | uma chamada de método direta — eventos é over-engineering |
| consumir mensagens externas | o adapter (o bus é para *publicação*, não subscrição em código) |

---

## O que o contrato garante

| Comportamento | Garantido por |
|---|---|
| Retorna um `AxisResult` (nunca lança cooperativamente) | `IAxisBus` |
| Agrega todas as falhas de handler num único resultado | o adapter (o adapter na caixa usa `AxisResult.Combine`) |
| Handlers recebem a mesma instância do evento | o adapter |
| Semântica de roteamento para `params string[] topics` | o adapter — interpretada como topic / routing key / partition key |

---

## O que o contrato **não** garante

- **Durabilidade de entrega.** O adapter in-memory não tem; um adapter distribuído pode ter at-least-once ou exactly-once dependendo do broker.
- **Ordenação.** Handlers podem rodar concorrentemente. Ordenação entre handlers não é garantida.
- **Retries.** Falhas são reportadas, não retentadas. Adicione uma pipeline behaviour ou política de retry no broker se precisar.

---

## Exemplos reais

### 1. Publicar antes do commit

```csharp
public Task<AxisResult<CreateOrderResponse>> HandleAsync(CreateOrderCommand cmd)
    => orderFactory.CreateAsync(cmd)
        .ThenAsync(order => bus.PublishAsync(new OrderCreatedEvent(order.OrderId)))
        .ThenAsync(order => unitOfWork.SaveChangesAsync())
        .MapAsync(order => new CreateOrderResponse { OrderId = order.OrderId });
```

**Por que compensa:** o evento é enfileirado *antes* do commit. Com um adapter de outbox (Exemplo 3) a linha do evento é gravada na mesma conexão da mudança de estado; o único `SaveChangesAsync` commita ambos atomicamente, ou faz rollback dos dois. Não há dual-write commit-depois-publish, então o estado nunca pode commitar enquanto o evento se perde.

### 2. Dica de tópico para um adapter distribuído

```csharp
await bus.PublishAsync(
    new OrderShippedEvent(orderId),
    topics: ["orders", $"tenant:{tenant}"]);
```

**Por que compensa:** o publicador nomeia dois tópicos (uma stream genérica e uma com escopo de tenant). O adapter decide como mapeá-los — a aplicação fica vendor-neutral.

### 3. Plugando um outbox

> Ilustrativo — o framework já entrega um adapter de outbox durável pronto para produção, `AxisBus.Repository`, com os adapters de storage `AxisBus.Postgres` / `AxisBus.MySql` registrados via `AddAxisBusPostgres` / `AddAxisBusMySql` (veja a [referência da API](api-reference.md) e o [Adapter custom](custom-adapter.md)). Recorra ao esboço abaixo só se precisar de um formato de outbox que o adapter da caixa não cobre.

```csharp
public class OutboxBusAdapter(IOutboxStore outbox) : IAxisBus
{
    public Task<AxisResult> PublishAsync<TEvent>(TEvent @event, params string[] topics)
        where TEvent : IAxisEvent
        => outbox.EnqueueAsync(@event, topics);
}
```

**Por que compensa:** o mesmo `IAxisBus` vira um outbox transacional — publicadores não mudam uma linha. Um worker em background drena o outbox para o broker real, com entrega at-least-once e sem race de dual-write.

---

## Veja também

- [Publicar · `PublishAsync`](publish.md) — semântica em profundidade
- [Definindo eventos e handlers](events-and-handlers.md) — modelando a superfície
- [Adapter `AxisMemoryBus`](memory-adapter.md) — a implementação na caixa
- [Adapter custom](custom-adapter.md) — escreva um para seu broker

---

↩ [Voltar à documentação do AxisBus](README.md)
