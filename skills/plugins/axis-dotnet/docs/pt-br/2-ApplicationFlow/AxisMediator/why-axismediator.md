# Por que AxisMediator? · comparação

> Há outras maneiras de fazer CQRS / mediação de requests em .NET. Esta página diz por que o AxisMediator é diferente — uma comparação direta, sem mão na cintura.

---

## vs. MediatR

`MediatR` é a biblioteca canônica. AxisMediator difere deliberadamente:

1. **Retorna `AxisResult`.** O `Send` do MediatR retorna `TResponse` e você embrulha; AxisMediator retorna `AxisResult<TResponse>`.
2. **Marker interfaces por formato.** `IAxisCommand`, `IAxisCommand<TResponse>`, `IAxisQuery<TResponse>`, `IAxisStreamQuery<TItem>`, `IAxisEvent` — cada um tem seu handler tipado e método de dispatch. MediatR colapsa commands e queries em `IRequest<T>`.
3. **Contexto ambiente embutido.** `IAxisMediator.TraceId`/`OriginId`/`JourneyId`/`AxisEntityId`/`CancellationToken` vêm de graça.
4. **Pipeline context.** `AxisPipelineContext` faz a passagem de valor entre behaviours uma operação first-class. MediatR te força a contrabandear via DI.
5. **Sem notifications no mediator.** Eventos vão pelo [`AxisBus`](../AxisBus/README.md). Isso mantém o mediator focado em request/response e te deixa trocar o bus por um outbox sem tocar no mediator.

## vs. `IMediator` do MassTransit

`MassTransit.Mediator` é ótimo se você já usa MassTransit. AxisMediator é **menor**, **opinativo sobre formatos CQRS**, e integrado com o resto do Axis (logger, validator, telemetry, repository, saga). Se você não precisa do MassTransit para o bus, AxisMediator + AxisBus é mais leve.

## vs. um service por caso de uso à mão

DIY. Mesma forma, mas você redescobre: behaviours, contexto ambiente, o scanner, o `AxisPipelineContext`, a fiação de logger / validator / telemetry. AxisMediator poupa o custo — e mantém o pipeline consistente entre times.

---

## A comparação

| Característica | AxisMediator | MediatR | MassTransit.Mediator | Caseiro |
|---|:--:|:--:|:--:|:--:|
| Retorna `AxisResult` | **Sim** | Não | Não | Talvez |
| Markers tipados `Command`/`Query`/`Stream`/`Event` | **Sim** | Parcial | Sim | Talvez |
| Ambiente `TraceId`/`OriginId`/`JourneyId`/`AxisEntityId` | **Sim** | Não | Parcial | Talvez |
| Pipeline behaviours open-generic | **Sim** | Sim | Sim | Talvez |
| Pipeline context por chamada (`AxisPipelineContext`) | **Sim** | Não | Não | Talvez |
| Eventos vivem em package separada (`AxisBus`) | **Sim** | Notifications dentro do mediator | Sim | Talvez |
| Scanner de assembly (`AddCqrsMediator`) | **Sim** | Sim | Sim | Talvez |
| Behaviours embarcados: log / valida / telemetry / performance | **Sim** (packages próprias) | Não | Sim | Talvez |

---

## Veja também

- [Primeiros passos](getting-started.md) — instale e despache
- [CQRS · commands, queries, streams, eventos](cqrs.md) — os formatos de request
- [Pipeline behaviours](pipeline-behaviors.md) — o ponto de extensão open-generic

---

↩ [Voltar à documentação do AxisMediator](README.md)
