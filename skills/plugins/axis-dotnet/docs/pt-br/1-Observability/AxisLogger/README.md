# AxisLogger — Documentação

> 🌐 [English (README principal)](../../../en-us/1-Observability/AxisLogger/README.md)

**Logging estruturado com escopo de requisição para C#** — `IAxisLogger<T>` embrulha o `ILogger<T>` do `Microsoft.Extensions.Logging` e **enriquece automaticamente** cada entrada com `OriginId`, `TraceId` e `JourneyId` puxados do `IAxisMediator` atual. Além de `LogResult(tag, AxisResult)` para uma linha de logging estruturado de cada desfecho de ferrovia, e um `LoggingBehavior` opt-in que loga cada request do mediator automaticamente.

```csharp
public class CreatePersonHandler(IAxisLogger<CreatePersonHandler> logger, ...)
{
    public Task<AxisResult<CreatePersonResponse>> HandleAsync(CreatePersonCommand cmd)
    {
        logger.LogInformation("Creating person", ("Document", cmd.Document));

        return factory.CreateAsync(cmd)
            .ThenAsync(person => uow.SaveChangesAsync())
            .TapAsync(r => logger.LogResult("CreatePerson", r))   // uma linha, desfecho estruturado
            .MapAsync(_ => new CreatePersonResponse { PersonId = cmd.PersonId });
    }
}
```

Use esta página como **mapa**: leia o tronco abaixo (~5 min) e salte direto para o detalhe do grupo que você precisa — sem ler centenas de linhas.

---

## O tronco (leia primeiro)

### A interface em 60 segundos

```csharp
public interface IAxisLogger<in T>
{
    void LogDebug(string message, params (string Key, object? Value)[] properties);
    void LogInformation(string message, params (string Key, object? Value)[] properties);
    void LogWarning(string message, params (string Key, object? Value)[] properties);
    void LogError(string message, params (string Key, object? Value)[] properties);
    void LogError(Exception exception, string message, params (string Key, object? Value)[] properties);
    void LogCritical(string message, params (string Key, object? Value)[] properties);
    void LogResult(string tag, AxisResult result);
}
```

Seis níveis familiares, todo overload aceita `params (string Key, object? Value)[] properties` para enriquecimento **estruturado** do log. Cada entrada é embrulhada em um `ILogger.BeginScope(...)` carregando `UtcTime`, `OriginId`, `TraceId`, `JourneyId` e suas propriedades. → **[O contrato `IAxisLogger<T>`](iaxislogger.md)**

### Por que um generic? — `IAxisLogger<T>`

O `T` é a **categoria** para o `Microsoft.Extensions.Logging` (mesmo papel de `ILogger<T>`). Tag-eia a fonte da entrada para que filtros e sinks possam rotear por classe. → **[Categorias e propriedades estruturadas](categories.md)**

### `LogResult` — o companheiro da ferrovia

```csharp
logger.LogResult("CreatePerson", result);
```

`Success` → `Information` + `Tag`/`RequestName`. `Failure` → `Error` + as mesmas mais `AxisErrorList`. Uma chamada, uma entrada estruturada, no nível certo. → **[`LogResult` — desfechos estruturados](log-result.md)**

### `LoggingBehavior` — logar cada request automaticamente

Behaviour opt-in do mediator que loga `Handling {RequestName}` no topo de cada handler:

```csharp
services
    .AddAxisLogger()
    .AddLoggingBehavior();    // pipeline behaviour sobre IAxisMediator
```

→ **[`LoggingBehavior` — request logging automático](logging-behavior.md)**

### Instalação

```
dotnet add package AxisLogger
```

`AxisLogger` depende diretamente de `AxisMediator.Contracts` (para os identificadores ambiente); `AxisResult` (usado por `LogResult`) vem transitivamente através dela.

→ Guia completo: **[Primeiros passos](getting-started.md)**

---

## O mapa (salte para o que precisa)

| Grupo | Você quer… | Detalhe |
|---|---|---|
| **Contrato · `IAxisLogger<T>`** ⭐ | logar entradas estruturadas com auto-enriquecimento | [iaxislogger.md](iaxislogger.md) |
| **`LogResult`** | logar um desfecho de `AxisResult` em uma linha | [log-result.md](log-result.md) |
| **`LoggingBehavior`** | logar cada request do mediator automaticamente | [logging-behavior.md](logging-behavior.md) |
| **Categorias · `IAxisLogger<T>`** | como o `T` flui para as props estruturadas | [categories.md](categories.md) |
| **Por quê?** | o argumento contra `ILogger<T>` direto | [why-axislogger.md](why-axislogger.md) |
| **Referência** | cada membro num só lugar | [api-reference.md](api-reference.md) |

**Comece aqui:** [Primeiros passos](getting-started.md) · [O contrato `IAxisLogger<T>`](iaxislogger.md) · [Por que AxisLogger?](why-axislogger.md)

**Fundamentos:** [`LogResult` — desfechos estruturados](log-result.md) · [`LoggingBehavior` — request logging automático](logging-behavior.md) · [Categorias e propriedades estruturadas](categories.md)

**Referência e extras:** [Referência da API](api-reference.md)

---

## Princípios de design

1. **Estruturado primeiro.** Todo overload público aceita pares `(Key, Value)` — nunca uma string interpolada com valores embutidos. Busca e agregação dependem disso.
2. **Auto-enriquecimento é inegociável.** `OriginId`, `TraceId`, `JourneyId` sempre viajam com a entrada. Se você esquecer de passar, o logger não esquece.
3. **`LogResult` é o logger da ferrovia.** Um desfecho tipado merece uma entrada de log tipada, com o nível certo escolhido por você.
4. **O behaviour é opt-in.** `LoggingBehavior` pluga logging estruturado no pipeline do mediator com uma linha; desligue quando não precisar.
5. **Sinks ficam livres.** AxisLogger não escolhe o sink — `ILogger<T>` escolhe. Use Serilog, NLog, OpenTelemetry-Logs, qualquer coisa que pluga em `Microsoft.Extensions.Logging`.

---

## Licença

Apache 2.0
