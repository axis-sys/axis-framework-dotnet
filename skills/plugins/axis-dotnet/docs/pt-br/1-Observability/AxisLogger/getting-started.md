# Primeiros passos · instalação e uso

> Instale a package, registre na DI, logue sua primeira entrada estruturada — e opcionalmente ligue o behaviour de request logging com uma linha extra.

---

## Instalação

```
dotnet add package AxisLogger
```

`AxisLogger` é pequeno — depende diretamente de `AxisMediator.Contracts`, com `AxisResult` (usado por `LogResult`) vindo transitivamente através dela. Sinks plugam via `Microsoft.Extensions.Logging` como sempre (Serilog, OpenTelemetry, console, arquivo).

---

## Registrando

```csharp
using Axis;

builder.Services
    .AddAxisMediator()       // fornece IAxisMediator (TraceId/OriginId/JourneyId)
    .AddAxisLogger();        // pluga IAxisLogger<T> como scoped no assembly chamador
```

> **Atenção:** o construtor de `AxisLogger<T>` também exige um `TimeProvider`, e nem `AddAxisMediator()` nem `AddAxisLogger()` registram um. Resolver `IAxisLogger<T>` só com o registro acima lança um erro de resolução de DI. Consiga um chamando `AddLoggingBehavior()` (veja [Ligando o request logging automático](#ligando-o-request-logging-automático) mais abaixo — ele registra `TimeProvider.System`), ou registre você mesmo com `builder.Services.AddSingleton(TimeProvider.System);`.

Ambas as extensões vivem como **extensions de C# 12** em `IServiceCollection`:

```csharp
extension(IServiceCollection services)
{
    public IServiceCollection AddAxisLogger() { … }
    public IServiceCollection AddLoggingBehavior() { … }
}
```

> `AddAxisLogger()` chama `services.AddLogging()` internamente, então o pipeline `ILogger<T>` por baixo fica pronto mesmo se você esqueceu de registrar.

---

## Logando entradas estruturadas

```csharp
public class CreatePersonHandler(IAxisLogger<CreatePersonHandler> logger, ...)
{
    public Task<AxisResult<CreatePersonResponse>> HandleAsync(CreatePersonCommand cmd)
    {
        logger.LogInformation("Creating person",
            ("Document", cmd.Document),
            ("Tenant",   cmd.Tenant));

        // … pipeline
    }
}
```

Cada entrada é automaticamente enriquecida com `UtcTime`, `OriginId`, `TraceId` e `JourneyId` resolvidos do `IAxisMediator` ambiente. Seu sink vê um único objeto estruturado por linha — nunca uma string templada com valores embutidos.

---

## Logando um desfecho `AxisResult`

```csharp
return factory.CreateAsync(cmd)
    .ThenAsync(person => uow.SaveChangesAsync())
    .TapAsync(r => logger.LogResult("CreatePerson", r))
    .MapAsync(_ => new CreatePersonResponse { … });
```

**Por que compensa:** `LogResult` escolhe `Information` no sucesso e `Error` na falha, anexa `Tag`, `RequestName` e (na falha) a `AxisErrorList` inteira como propriedade estruturada. Um método, uma chamada de log sem decisão.

---

## Ligando o request logging automático

```csharp
builder.Services
    .AddAxisMediator()
    .AddAxisLogger()
    .AddLoggingBehavior();   // adiciona IAxisPipelineBehavior<TRequest> / <TRequest, TResponse>
```

Agora cada request do mediator loga `Handling {RequestName}` com propriedades estruturadas no topo de cada handler — sem boilerplate por handler.

---

## Veja também

- [O contrato `IAxisLogger<T>`](iaxislogger.md) — cada overload
- [`LogResult` — desfechos estruturados](log-result.md) — o companheiro da ferrovia
- [`LoggingBehavior` — request logging automático](logging-behavior.md) — pipeline opt-in do mediator
- [Categorias e propriedades estruturadas](categories.md) — o que o `T` faz
- [Por que AxisLogger?](why-axislogger.md) — o argumento contra `ILogger<T>` direto
- [Referência da API](api-reference.md) — cada membro num só lugar

---

↩ [Voltar à documentação do AxisLogger](README.md)
