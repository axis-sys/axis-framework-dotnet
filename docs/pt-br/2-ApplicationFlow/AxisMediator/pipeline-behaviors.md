# Pipeline behaviours · `IAxisPipelineBehavior`

> Código transversal que embrulha cada dispatch. Registre um behaviour open-generic uma vez; o mediator constrói uma cadeia em torno do handler.

```csharp
public interface IAxisPipelineBehavior<in TRequest> where TRequest : IAxisRequest
{
    Task<AxisResult> HandleAsync(TRequest request, AxisPipelineContext context, Func<Task<AxisResult>> next);
}

public interface IAxisPipelineBehavior<in TRequest, TResponse>
    where TRequest : IAxisRequest
    where TResponse : IAxisResponse
{
    Task<AxisResult<TResponse>> HandleAsync(TRequest request, AxisPipelineContext context, Func<Task<AxisResult<TResponse>>> next);
}
```

---

## Quando usar

Qualquer coisa que você faria "antes de cada handler" ou "depois de cada handler": logging, validação, autorização, telemetria, transações, retries. O pipeline deixa o handler focado no caso de uso.

## Quando *não* usar

| Você quer… | Use no lugar |
|---|---|
| rodar **lógica de negócio** que depende do formato do request | o handler |
| chamar uma porta | injete a porta no handler |
| fazer stream de valores do behaviour | streams não fluem por `IAxisPipelineBehavior` — elas pulam o pipeline |

---

## Anatomia de um behaviour

```csharp
public class MyBehavior<TRequest, TResponse>(IMyDep dep) : IAxisPipelineBehavior<TRequest, TResponse>
    where TRequest : IAxisRequest
    where TResponse : IAxisResponse
{
    public async Task<AxisResult<TResponse>> HandleAsync(
        TRequest request, AxisPipelineContext context, Func<Task<AxisResult<TResponse>>> next)
    {
        // pré — antes do pipeline interno rodar
        var result = await next();
        // pós — depois que rodou
        return result;
    }
}
```

| Argumento | Propósito |
|---|---|
| `request` | o request tipado — mesma instância que o handler recebe |
| `context` | o [`AxisPipelineContext`](pipeline-context.md) — um dicionário por chamada compartilhado com outros behaviours |
| `next` | um thunk que executa o resto do pipeline (o próximo behaviour, ou o handler) |

> Chamar `next()` **uma vez** é o contrato. Pular (retornar um `Error` curto-circuito) é permitido e esperado (validação faz isso). Chamar mais de uma vez é erro de programação.

---

## Ordenação

Lendo `AxisMediatorHandler.ExecutePipelineAsync` direto: behaviours vêm de `IServiceProvider.GetServices<IAxisPipelineBehavior<TRequest>>().Reverse()` — e um `foreach` constrói a cadeia. O resultado é:

```
Mais externo (primeiro registrado)
   ┃
   ▼
   ... (outros) ...
   ┃
   ▼
Mais interno (último registrado)
   ┃
   ▼
Handler
```

Se você registra `LoggingBehavior` depois `ValidationBehavior`:

- O request entra em `LoggingBehavior` (loga `Handling X`).
- Depois em `ValidationBehavior` (valida).
- Depois o handler.
- Caminho de retorno volta por fora.

> O **primeiro registrado** vê o **request primeiro** e a **response por último**. Escolha a ordem deliberadamente.

---

## Behaviours embarcados em todo o framework

| Behaviour | Package | O que faz |
|---|---|---|
| `LoggingBehavior<TRequest>` / `<TRequest, TResponse>` | [`AxisLogger`](../../1-Observability/AxisLogger/README.md) | loga `Handling X` |
| `ValidationBehavior<TRequest>` / `<TRequest, TResponse>` | [`AxisValidator`](../AxisValidator/README.md) | curto-circuita em falha de `IAxisValidator<TRequest>.ValidateAsync` |
| `TelemetryBehavior<TRequest>` / `<TRequest, TResponse>` | [`AxisTelemetry`](../../1-Observability/AxisTelemetry/README.md) | embrulha num span, tag-eia, grava counters e histograms |
| `PerformanceBehavior<TRequest, TResponse>` | esta package | alerta quando lento (>500ms) |

Uma fiação típica os registra nesta ordem:

```csharp
services.AddLoggingBehavior();      // loga primeiro
services.AddAxisValidator(asm);     // valida em seguida
services.AddOpenTelemetryAxis();
services.AddTransient(typeof(IAxisPipelineBehavior<,>), typeof(TelemetryBehavior<,>));
services.AddPerformanceBehavior();  // performance por último (mais próximo do handler)
```

---

## Exemplos reais

### 1. Behaviour de autorização

```csharp
public class AuthBehavior<TRequest, TResponse>(IAxisMediator mediator)
    : IAxisPipelineBehavior<TRequest, TResponse>
    where TRequest : IAxisRequest
    where TResponse : IAxisResponse
{
    public Task<AxisResult<TResponse>> HandleAsync(
        TRequest req, AxisPipelineContext ctx, Func<Task<AxisResult<TResponse>>> next)
    {
        if (req is IRequiresAuthentication && mediator.AxisEntityId is null)
            return Task.FromResult<AxisResult<TResponse>>(AxisError.Unauthorized("AUTH_REQUIRED"));

        return next();
    }
}
```

**Por que compensa:** todo command/query que implementa `IRequiresAuthentication` é checado uma vez, no nível do pipeline — handlers param de se preocupar com autenticação completamente.

### 2. Behaviour transacional

```csharp
public class TransactionalBehavior<TRequest, TResponse>(IAxisUnitOfWork uow)
    : IAxisPipelineBehavior<TRequest, TResponse>
    where TRequest : IAxisCommand<TResponse>
    where TResponse : IAxisCommandResponse
{
    public Task<AxisResult<TResponse>> HandleAsync(
        TRequest req, AxisPipelineContext ctx, Func<Task<AxisResult<TResponse>>> next)
        => uow.InTransactionAsync(next);
}
```

**Por que compensa:** todo command que tem response tipada roda dentro de uma transação. Commit/rollback segue a ferrovia. Sem boilerplate `using var tx = …` em handler nenhum.

---

## Veja também

- [Pipeline context](pipeline-context.md) — compartilhe valores entre behaviours
- [Despachando · `IAxisMediatorHandler`](dispatching.md) — como a cadeia é construída
- [Performance behaviour](performance-behavior.md) — um exemplo embarcado
- [`LoggingBehavior`](../../1-Observability/AxisLogger/README.md) · [`ValidationBehavior`](../AxisValidator/README.md) · [`TelemetryBehavior`](../../1-Observability/AxisTelemetry/README.md)

---

↩ [Voltar à documentação do AxisMediator](README.md)
