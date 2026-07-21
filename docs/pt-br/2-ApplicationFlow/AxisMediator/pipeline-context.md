# Pipeline context · `AxisPipelineContext`

> Um dicionário por chamada compartilhado entre cada behaviour no pipeline. Use para passar um valor calculado por um behaviour upstream a um downstream — sem inventar novos pontos de injeção.

```csharp
public sealed class AxisPipelineContext
{
    public IDictionary<string, object?> Items { get; } = new Dictionary<string, object?>(StringComparer.Ordinal);

    public T? Get<T>(string key)
        => Items.TryGetValue(key, out var value) && value is T typed ? typed : default;

    public void Set<T>(string key, T value) => Items[key] = value;
}
```

---

## Quando usar

Quando um behaviour produz um valor que o próximo quer — ex.: `TelemetryBehavior` abre um `IAxisSpan` e um behaviour downstream quer setar tags extras. Coloque o span no context com uma chave tipada; o downstream lê de volta sem recriar o span.

## Quando *não* usar

| Você quer… | Use no lugar |
|---|---|
| passar valores ao **handler** | a própria request ou o contexto ambiente do mediator |
| compartilhar valores entre **múltiplos requests** | um service singleton |
| guardar state transversal (usuário atual, tenant) | [`IAxisMediator`](mediator-and-accessors.md) — o contexto ambiente, não este |

---

## As chaves bem-conhecidas — `AxisPipelineContextKeys`

```csharp
public static class AxisPipelineContextKeys
{
    public const string Span = "axis.pipeline.span";   // o IAxisSpan aberto pelo TelemetryBehavior
}
```

Adicione a essa classe quando você publicar uma chave de um behaviour embarcado. Para seus próprios behaviours, defina uma `const string` no seu próprio tipo — e mantenha sob seu namespace (`"my-app.audit.actor"`).

---

## Exemplos reais

### 1. Lendo o span setado pelo `TelemetryBehavior`

```csharp
public class AddIdentityTagBehavior<TRequest>(IAxisMediator mediator)
    : IAxisPipelineBehavior<TRequest> where TRequest : IAxisRequest
{
    public async Task<AxisResult> HandleAsync(
        TRequest request, AxisPipelineContext context, Func<Task<AxisResult>> next)
    {
        var span = context.Get<IAxisSpan>(AxisPipelineContextKeys.Span);
        span?.SetTag(TelemetryTagNames.AxisIdentity, mediator.AxisEntityId);

        return await next();
    }
}
```

**Por que compensa:** a tag de identidade anda no span existente. Sem segundo span, sem risco de tags órfãs, sem injeção de `IAxisSpan` neste behaviour.

### 2. Passando um `IDisposable` de um behaviour para outro

```csharp
public class StartScopeBehavior<TRequest>(IServiceProvider sp) : IAxisPipelineBehavior<TRequest>
    where TRequest : IAxisRequest
{
    public async Task<AxisResult> HandleAsync(
        TRequest request, AxisPipelineContext context, Func<Task<AxisResult>> next)
    {
        var scope = sp.CreateScope();
        context.Set("my-app.scope", scope);
        try { return await next(); }
        finally { scope.Dispose(); }
    }
}

public class UseScopedRepoBehavior<TRequest>(IAxisMediator mediator) : IAxisPipelineBehavior<TRequest>
    where TRequest : IAxisRequest
{
    public Task<AxisResult> HandleAsync(
        TRequest request, AxisPipelineContext context, Func<Task<AxisResult>> next)
    {
        var scope = context.Get<IServiceScope>("my-app.scope");
        var repo = scope?.ServiceProvider.GetRequiredService<IMyRepo>();
        // … use repo, depois chame next()
        return next();
    }
}
```

**Por que compensa:** o scope abre uma vez no behaviour externo, usado por behaviours internos, disposed com segurança na saída — sem nenhum "ambient" global.

### 3. Guardando um timer iniciado para uma linha de log futura

```csharp
public class StartTimerBehavior<TRequest> : IAxisPipelineBehavior<TRequest> where TRequest : IAxisRequest
{
    public async Task<AxisResult> HandleAsync(
        TRequest request, AxisPipelineContext context, Func<Task<AxisResult>> next)
    {
        var sw = Stopwatch.StartNew();
        context.Set("my-app.sw", sw);

        var result = await next();
        return result;
    }
}

public class TrailingLogBehavior<TRequest>(IAxisLogger<TRequest> logger) : IAxisPipelineBehavior<TRequest>
    where TRequest : IAxisRequest
{
    public async Task<AxisResult> HandleAsync(
        TRequest request, AxisPipelineContext context, Func<Task<AxisResult>> next)
    {
        var result = await next();
        var sw = context.Get<Stopwatch>("my-app.sw");
        logger.LogInformation("Took {Ms}ms", ("Ms", sw?.ElapsedMilliseconds ?? 0));
        return result;
    }
}
```

**Por que compensa:** o timer começa no behaviour mais externo e o log de saída usa o valor decorrido — mesmo que os dois behaviours não se conheçam.

---

## Veja também

- [Pipeline behaviours](pipeline-behaviors.md) — como um behaviour lê e escreve no context
- [Despachando · `IAxisMediatorHandler`](dispatching.md) — quando o context é criado (uma vez por dispatch)
- [Telemetry behaviour](../../1-Observability/AxisTelemetry/telemetry-behavior.md) — o produtor da chave `Span`

---

↩ [Voltar à documentação do AxisMediator](README.md)
