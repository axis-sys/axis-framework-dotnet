# `LoggingBehavior` — request logging automático

> Um `IAxisPipelineBehavior` opt-in que loga `"Handling {RequestName}"` no topo de cada request do mediator. Registre uma vez; cada handler do seu app se beneficia.

```csharp
services
    .AddAxisLogger()
    .AddLoggingBehavior();   // pluga LoggingBehavior<TRequest> e <TRequest, TResponse>
```

---

## Quando usar

Sempre, salvo razão pra não. Entradas de log "Handling X" por requisição são o ganho transversal de observabilidade mais barato disponível — e combinam perfeitamente com `LogResult` no fim do pipeline.

## Quando *não* usar

| Você quer… | Use no lugar |
|---|---|
| logar só um subconjunto de handlers | injete `IAxisLogger<T>` por handler e chame manualmente |
| logar o **payload** da requisição | escreva seu próprio behaviour com redaction |
| logar latência / métricas | [`AxisTelemetry`](../AxisTelemetry/README.md) |

---

## O que o behaviour faz

Lendo `LoggingBehavior<TRequest>` e `<TRequest, TResponse>` direto:

| Variante | Onde fica | O que loga |
|---|---|---|
| `LoggingBehavior<TRequest>` | requests sem response | `LogInformation("Handling {RequestName}.", ("RequestName", typeof(TRequest).Name))` |
| `LoggingBehavior<TRequest, TResponse>` | requests com response tipada | mesma forma, mesma chamada |

Depois da chamada de log, o behaviour só `await`a `next()` — **não** embrulha a chamada em `try/catch`, **não** loga o desfecho, **não** cronometra. Pareie com `LogResult` (manual ou via seu próprio behaviour) para o lado do desfecho.

---

## O que é registrado

`AddLoggingBehavior` (de `DependencyInjection.cs`):

```csharp
public IServiceCollection AddLoggingBehavior()
{
    services.AddSingleton(TimeProvider.System);
    services.AddTransient(typeof(IAxisPipelineBehavior<>),  typeof(LoggingBehavior<>));
    services.AddTransient(typeof(IAxisPipelineBehavior<,>), typeof(LoggingBehavior<,>));
    return services;
}
```

- `TimeProvider.System` é registrado como singleton para que o enriquecimento de `UtcTime` no `IAxisLogger<T>` funcione.
- Dois behaviours **open-generic** são registrados como transient para que o mediator resolva um por tipo de request.

---

## Exemplo real — compondo com um `LogResult` manual

```csharp
// Program.cs
builder.Services
    .AddAxisMediator()
    .AddAxisLogger()
    .AddLoggingBehavior();    // loga Handling X no topo

// Handler
public class CreatePersonHandler(IAxisLogger<CreatePersonHandler> logger, ...)
{
    public Task<AxisResult<CreatePersonResponse>> HandleAsync(CreatePersonCommand cmd)
        => factory.CreateAsync(cmd)
            .ThenAsync(p => uow.SaveChangesAsync())
            .TapAsync(r => logger.LogResult("CreatePerson", r))   // loga o desfecho no fim
            .MapAsync(_ => new CreatePersonResponse { … });
}
```

**Por que compensa:** a entrada "Handling …" em nível de request é automática; a entrada de desfecho por handler é uma linha. Juntas, elas envolvem cada request com `TraceId`/`OriginId`/`JourneyId` — a linha do tempo lê limpa em qualquer sink.

---

## Veja também

- [O contrato `IAxisLogger<T>`](iaxislogger.md) — o que o behaviour acaba chamando
- [`LogResult`](log-result.md) — pareie com o behaviour no fim do pipeline
- [Categorias e propriedades estruturadas](categories.md) — por que o behaviour usa `IAxisLogger<TRequest>`

---

↩ [Voltar à documentação do AxisLogger](README.md)
