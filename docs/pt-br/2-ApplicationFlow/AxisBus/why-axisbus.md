# Por que AxisBus? · comparação

> Há outras bibliotecas de event bus para .NET. Esta página diz por que o AxisBus é diferente — uma comparação direta, sem mão na cintura.

---

## vs. `MediatR` Notifications

`INotification` do `MediatR` faz fan-out in-process, mas cada call site precisa conhecer `IMediator`. Não tem abstração sobre o **transporte** — migrar de in-process para Kafka significa reescrever seu código de fan-out. AxisBus *é* a abstração: `IAxisBus` permanece o mesmo quando o adapter muda.

## vs. `MassTransit`

`MassTransit` é um framework completo de messaging distribuído — sagas, convenções, transports, retries, scheduling. Se você precisa de tudo isso, use. AxisBus é **uma porta de um método só** que você pode implementar *em cima do* MassTransit, ou substituir por um adapter mínimo para um único broker. O código da aplicação nunca vê a escolha do framework.

## vs. callbacks de `Microsoft.Extensions.DependencyInjection`

Você poderia chamar serviços `IEnumerable<IFoo>` e `Task.WhenAll` por conta própria. É o que o `MemoryBusAdapter` faz. AxisBus padroniza o padrão **publish-falhas-agregadas-em-`AxisResult`** em todos os packages do Axis.

## vs. um `IEventBus<T>` caseiro

A abstração DIY. Mesma forma do `IAxisBus`, mas você escreve o contrato, o adapter in-memory, os testes, e redescobre os mesmos trade-offs sozinho. `IAxisBus` poupa o custo — e herda a história da ferrovia do `AxisResult`.

---

## A comparação

| Característica | AxisBus | Notifications do MediatR | MassTransit | `IEventBus` custom |
|---|:--:|:--:|:--:|:--:|
| Porta de publicação de um método só | **Sim** | Não (`Mediator`) | Não | Sim |
| Retorna `AxisResult` | **Sim** | Não | Não | Talvez |
| Agrega falhas de handlers | **Sim** | Não | Por política | Talvez |
| Código da aplicação vendor-neutral | **Sim** | Não | Não | Sim |
| Adapter in-process incluído | **Sim** | n/a | Sim | Não |
| Troca outbox/Kafka/RabbitMQ sem mudar a aplicação | **Sim** | Não | Sim | Sim |
| Superfície minúscula, sem curva de aprendizado | **Sim** | Sim | Não | Sim |
| Zero deps NuGet na abstração | **Sim** | Sim | Não | Sim |

---

## Veja também

- [O contrato `IAxisBus`](iaxisbus.md) — a superfície
- [Publicar · `PublishAsync`](publish.md) — semântica
- [Adapter custom](custom-adapter.md) — como plugar um broker

---

↩ [Voltar à documentação do AxisBus](README.md)
