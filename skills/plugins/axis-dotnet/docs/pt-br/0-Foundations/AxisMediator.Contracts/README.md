# AxisMediator.Contracts — Documentação

> 🌐 [English (README principal)](../../../en-us/0-Foundations/AxisMediator.Contracts/README.md)

**Os contratos puros do mediator do Axis** — markers de CQRS, interfaces de handler, a fachada de execução, o contexto ambiente `IAxisMediator` e as abstrações de pipeline. Zero infraestrutura: apenas as abstrações que todo consumidor compartilha.

```csharp
// Um comando e seu handler dependem apenas dos contratos — nunca da implementação.
public sealed record CreateOrderCommand(Guid CustomerId, Guid ProductId, int Quantity)
    : IAxisCommand<CreateOrderResponse>;

public sealed record CreateOrderResponse(Guid OrderId) : IAxisCommandResponse;

public sealed class CreateOrderHandler(IAxisMediator mediator)
    : IAxisCommandHandler<CreateOrderCommand, CreateOrderResponse>
{
    public Task<AxisResult<CreateOrderResponse>> HandleAsync(CreateOrderCommand command)
        => /* ... */;
}
```

Use esta página como um **mapa**: leia o tronco abaixo (~5 min) e salte para a [Referência da API](api-reference.md) para o catálogo completo — ou para o **[AxisMediator](../../2-ApplicationFlow/AxisMediator/README.md)** para o guia de uso, primeiros passos, pipelines e tutoriais de CQRS.

---

## O tronco (leia primeiro)

### Por que um pacote só de contratos?

`AxisMediator.Contracts` guarda as **abstrações** do mediator e nada mais — sem fiação de DI, sem dispatcher, sem behaviours. Por ser composto de tipos puros, ele vive em `0-Foundations`, enquanto a **[implementação](../../2-ApplicationFlow/AxisMediator/README.md)** concreta (`AxisMediator`) vive em `2-ApplicationFlow`.

Essa separação permite que pacotes como **AxisBus**, **AxisSaga**, **AxisValidator**, **AxisLogger** e **AxisTelemetry** dependam apenas dos contratos — declarando comandos, handlers ou pipeline behaviours — sem arrastar o dispatcher consigo.

### CQRS em um minuto

Toda mensagem é uma **request** que implementa um marker, e toda request tem um **handler** correspondente:

- **Command** — altera estado. `IAxisCommand` (sem resposta) ou `IAxisCommand<TResponse>`. Tratado por `IAxisCommandHandler<TCommand>` / `IAxisCommandHandler<TCommand, TResponse>`.
- **Query** — lê estado. `IAxisQuery<TResponse>`, tratada por `IAxisQueryHandler<TQuery, TResponse>`. Leituras em streaming usam `IAxisStreamQuery<TItem>` + `IAxisStreamQueryHandler<TQuery, TItem>`.
- **Event** — um fato que já aconteceu. `IAxisEvent`, tratado por `IAxisEventHandler<TEvent>`.

Handlers retornam `Task<AxisResult>` ou `Task<AxisResult<TResponse>>` — falhas são valores, não exceções.

### Executando requests

`IAxisMediator` é o contexto ambiente injetado no seu código. Ele carrega a correlação (`TraceId`, `OriginId`, `JourneyId`), a `AxisEntityId` do chamador, o `CancellationToken` da request e a fachada `Cqrs` — `IAxisMediatorHandler` — usada para despachar:

```csharp
AxisResult<CreateOrderResponse> result =
    await mediator.Cqrs.ExecuteAsync<CreateOrderCommand, CreateOrderResponse>(command);
```

→ Catálogo completo: **[Referência da API](api-reference.md)**

### Pipeline behaviours

Passos transversais (logging, telemetria, validação) implementam `IAxisPipelineBehavior<TRequest>` ou `IAxisPipelineBehavior<TRequest, TResponse>`. Eles compartilham estado ao longo de uma execução através do `AxisPipelineContext` (`Items` / `Get` / `Set`), indexado pelas constantes bem conhecidas em `AxisPipelineContextKeys`.

### Instalação

```
dotnet add package AxisMediator.Contracts
```

> A maioria das aplicações instala o **AxisMediator** em vez deste, que referencia estes contratos transitivamente. Adicione este pacote diretamente apenas quando você cria abstrações (ex.: uma biblioteca que declara handlers ou behaviours) que não devem depender da implementação.

---

## O mapa (salte para o que precisa)

| Grupo | Você quer… | Detalhe |
|---|---|---|
| **Referência · todos os contratos** ⭐ | consultar cada marker, handler, fachada e tipo de pipeline | [api-reference.md](api-reference.md) |
| **Uso · `AxisMediator`** | primeiros passos, pipelines, CQRS e a implementação do dispatcher | [AxisMediator](../../2-ApplicationFlow/AxisMediator/README.md) |

**Comece aqui:** [Referência da API](api-reference.md) · [Guia de uso do AxisMediator](../../2-ApplicationFlow/AxisMediator/README.md)

---

## Princípios de design

1. **Abstrações em Foundations, implementação em ApplicationFlow.** Os contratos têm zero dependências do dispatcher, então consumidores podem alvejá-los isoladamente.
2. **Erros são valores, não exceções.** Todo handler retorna `AxisResult` / `AxisResult<TResponse>`; a falha faz parte da assinatura.
3. **O sistema de tipos é o contrato.** Interfaces marker e restrições genéricas tornam pares request/handler ilegais irrepresentáveis.
4. **Um marker por intenção.** Comandos, queries, streams e eventos têm cada um seu próprio marker, então o dispatcher roteia por tipo.
5. **Estado transversal permanece explícito.** Behaviours passam dados através de um `AxisPipelineContext` tipado, nunca por globais ocultas.

---

## Licença

Apache 2.0
