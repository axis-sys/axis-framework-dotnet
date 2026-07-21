# Adapter custom · escreva seu próprio `IAxisBus`

> Troque o adapter in-process por Kafka, RabbitMQ, Azure Service Bus, AWS SNS — ou um outbox transacional. Implemente um método, registre sua classe como `IAxisBus`.

```csharp
public class KafkaBusAdapter(IProducer<string, byte[]> producer) : IAxisBus
{
    public async Task<AxisResult> PublishAsync<TEvent>(TEvent @event, params string[] topics)
        where TEvent : IAxisEvent
    {
        if (topics.Length == 0) topics = [DefaultTopic(typeof(TEvent))];

        var payload = JsonSerializer.SerializeToUtf8Bytes(@event);
        var deliveryResults = await Task.WhenAll(
            topics.Select(t => producer.ProduceAsync(t, new() { Value = payload })));

        return AxisResult.Ok();   // ou agregue erros do broker via AxisResult.Combine
    }

    private static string DefaultTopic(Type t) => t.Name.ToLowerInvariant();
}
```

---

## Quando usar

- Produtores e consumidores vivem em **processos diferentes**.
- Você precisa de **persistência**, **partitioning**, **retries no broker**.
- Você quer um **outbox transacional** na frente do broker.
- Você quer um **test double** que registra cada publish.

## Quando *não* usar

| Você quer… | Use no lugar |
|---|---|
| rodar num único processo | o [`AxisMemoryBus`](memory-adapter.md) da caixa |
| adicionar orquestração no tempo de publish | uma *pipeline behaviour* do mediator, não um bus custom |

---

## O contrato que você precisa honrar

| Comportamento | Obrigatório | Razão |
|---|---|---|
| Retorne `Task<AxisResult>`, nunca lance cooperativamente | sim | chamadores encadeiam pela ferrovia |
| `params string[] topics` interpretados como dicas de transporte | sim | o adapter da caixa ignora; o seu deve dar significado |
| Agregue falhas no fan-out (use `AxisResult.Combine`) | recomendado | espelha o adapter da caixa e a história de `Combine` em todo Axis |
| Honre cancelamento de `IAxisMediatorAccessor.AxisMediator?.CancellationToken` | recomendado | espelha o restante do Axis |
| Logue via `AxisLogger` (enrichers de correlation / tenant) | recomendado | logs estruturados em todos os packages |

---

## Exemplo real — outbox transacional

> **O framework já entrega um destes.** `AxisBus.Repository` é um adapter de outbox durável completo e pronto para produção — enqueue e drain-no-commit, um dispatcher em background, claim-by-lease, tudo — com os adapters de storage `AxisBus.Postgres` e `AxisBus.MySql` registrados via `AddAxisBusPostgres` / `AddAxisBusMySql` (veja a [referência da API](api-reference.md)). Você não precisa construir o adapter abaixo você mesmo; ele fica aqui só para ilustrar como um adapter customizado no formato outbox funcionaria.

Uma implementação de bus que não publica de fato — escreve o evento em uma tabela de outbox dentro da `UnitOfWork` atual, e um worker em background drena o outbox para o broker real. O publicador **não** muda uma linha.

```csharp
public class OutboxBusAdapter(IOutboxStore outbox) : IAxisBus
{
    public Task<AxisResult> PublishAsync<TEvent>(TEvent @event, params string[] topics)
        where TEvent : IAxisEvent
        => outbox.EnqueueAsync(new OutboxEntry(
            EventType:   typeof(TEvent).FullName!,
            PayloadJson: JsonSerializer.Serialize(@event),
            Topics:      topics));
}

// composição
services.AddScoped<IAxisBus, OutboxBusAdapter>();
services.AddHostedService<OutboxDrainerWorker>();
```

**Por que compensa:** eventos viram parte da mesma transação da persistência; o publish é atômico com a escrita. Sem race de dual-write, entrega at-least-once de graça, e o código *da aplicação* é idêntico ao caso in-memory.

---

## Veja também

- [O contrato `IAxisBus`](iaxisbus.md) — o que seu adapter precisa satisfazer
- [Adapter `AxisMemoryBus`](memory-adapter.md) — a referência da caixa
- [Publicar · `PublishAsync`](publish.md) — semântica de fan-out

---

↩ [Voltar à documentação do AxisBus](README.md)
