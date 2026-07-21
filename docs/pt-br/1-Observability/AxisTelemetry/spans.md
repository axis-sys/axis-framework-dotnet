# Spans · `IAxisSpan`

> O span com que você de fato trabalha. Fluente — todo mutador retorna `this` para você encadear. Implementa `IDisposable` — embrulhe com `using var` e o span encerra automaticamente.

```csharp
public interface IAxisSpan : IDisposable
{
    string TraceId { get; }
    string SpanId { get; }

    IAxisSpan SetTag(string key, object? value);
    IAxisSpan SetStatus(AxisSpanStatus status, string? description = null);
    IAxisSpan RecordException(Exception exception);
    IAxisSpan AddEvent(string name, params KeyValuePair<string, object?>[] attributes);
}
```

---

## Quando usar

Em qualquer lugar onde uma unidade de trabalho mereça sua linha no trace UI: uma chamada de banco, um HTTP downstream, um passo in-process que pode demorar, uma fronteira de integração. Abra um span por operação; não aninhe a menos que o trabalho seja genuinamente aninhado.

## Quando *não* usar

| Você quer… | Use no lugar |
|---|---|
| contar algo | [`IAxisMetrics.IncrementCounter`](contracts.md) |
| gravar distribuição de duração | [`IAxisMetrics.RecordHistogram`](contracts.md) |
| logar uma mensagem estruturada | [`AxisLogger`](../AxisLogger/README.md) |

---

## Os quatro mutadores

| Método | O que faz | Retorna |
|---|---|---|
| `SetTag(key, value)` | adiciona uma tag estruturada ao span | `this` (fluente) |
| `SetStatus(status, description?)` | marca o span `Ok` / `Error` / `Unset` | `this` (fluente) |
| `RecordException(ex)` | adiciona um evento `"exception"` com `type`/`message`/`stacktrace`, depois `SetStatus(Error, ex.Message)` | `this` (fluente) |
| `AddEvent(name, attrs...)` | adiciona um evento pontual dentro do span | `this` (fluente) |

Lendo `ActivityAxisSpan` direto:

- `SetTag` chama `activity?.SetTag(key, value)`.
- `SetStatus` chama `activity?.SetStatus(MapStatus(status), description)`.
- `RecordException` adiciona um `ActivityEvent("exception", …)` e chama `SetStatus(Error, ex.Message)`.
- `AddEvent` adiciona um `ActivityEvent(name, tags)`.

O caso de activity-null é no-op — o mesmo código funciona quando o `ActivitySource` retorna `null` porque ninguém está escutando.

---

## O padrão de disposal

```csharp
public Task<AxisResult> CommitAsync()
{
    using var span = telemetry.StartSpan("db.postgres.commit", AxisSpanKind.Client);
    span.SetTag("db.system", "postgresql");

    try { /* trabalho */ span.SetStatus(AxisSpanStatus.Ok); }
    catch (Exception ex) { span.RecordException(ex); throw; }

    return AxisResult.Ok();
}
```

`using var` garante que o span encerra quando o método sai — caminho de sucesso, caminho de exceção, return antecipado, tudo coberto.

---

## Exemplos reais

### 1. Tagear uma chamada de banco

```csharp
public async Task<AxisResult> StartAsync()
{
    using var span = telemetry.StartSpan("db.postgres.connect", AxisSpanKind.Client);
    span.SetTag("db.system", "postgresql");

    try
    {
        _connection ??= await dataSource.OpenConnectionAsync(ct);
        _transaction = await _connection.BeginTransactionAsync(ct);
        span.SetStatus(AxisSpanStatus.Ok);
        return AxisResult.Ok();
    }
    catch (Exception ex)
    {
        span.RecordException(ex);
        return AxisError.InternalServerError("POSTGRES_ERROR_STARTING_CONNECTION");
    }
}
```

**Por que compensa:** o `PostgresUnitOfWork` embarcado já faz exatamente isso. Cada connection / commit / rollback ganha um span tag-eado `db.system = postgresql`, com status + exceção capturados.

### 2. Encadeamento fluente

```csharp
using var span = telemetry.StartSpan("orders.import_batch")
    .SetTag("batch.size", batch.Count)
    .SetTag("batch.source", source);

await ProcessBatchAsync(batch);
span.SetStatus(AxisSpanStatus.Ok);
```

**Por que compensa:** o estilo fluente mantém todo o tagging perto do `StartSpan` — sem caça depois de "esqueci de tagear o tamanho do lote?".

### 3. Evento dentro de um span

```csharp
using var span = telemetry.StartSpan("orders.fulfill");

span.AddEvent("payment.captured",
    new("amount",  order.Amount),
    new("gateway", "stripe"));

span.AddEvent("inventory.reserved",
    new("warehouse", warehouse));

span.SetStatus(AxisSpanStatus.Ok);
```

**Por que compensa:** os eventos aparecem como marcadores pontuais dentro do span, com seus próprios atributos — uma mini-timeline da operação, numa linha do trace.

---

## Veja também

- [Os contratos](contracts.md) — o que te dá o span
- [`TelemetryBehavior`](telemetry-behavior.md) — o behaviour que abre spans automaticamente
- [Tag names](tag-names.md) — constantes canônicas para tags comuns

---

↩ [Voltar à documentação do AxisTelemetry](README.md)
