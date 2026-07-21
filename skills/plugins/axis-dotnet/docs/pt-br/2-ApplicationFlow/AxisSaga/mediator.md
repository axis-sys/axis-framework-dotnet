# Mediator · `IAxisSagaMediator`

> A superfície para sagas que a aplicação vê. Iniciar uma instância (com id gerado ou fornecido pelo chamador, opcionalmente com uma janela de retenção), buscar seu estado (tipado ou não-tipado), ou pedir para retomar.

```csharp
public interface IAxisSagaMediator
{
    Task<AxisResult<string>> StartAsync<TPayload>(string sagaName, TPayload payload)
        where TPayload : class;

    // Overloads opcionais: forneça seu próprio id de correlação e/ou uma janela de retenção.
    Task<AxisResult<string>> StartAsync<TPayload>(string sagaName, TPayload payload, string sagaId)
        where TPayload : class;
    Task<AxisResult<string>> StartAsync<TPayload>(string sagaName, TPayload payload, TimeSpan? retainedFor)
        where TPayload : class;
    Task<AxisResult<string>> StartAsync<TPayload>(string sagaName, TPayload payload, string sagaId, TimeSpan? retainedFor)
        where TPayload : class;

    Task<AxisResult<AxisSagaInstance>>             GetByIdAsync(string sagaId);
    Task<AxisResult<AxisSagaInstance<TPayload>>>   GetByIdAsync<TPayload>(string sagaId)
        where TPayload : class;

    Task<AxisResult> ResumeAsync(string sagaId);
}
```

O id é **gerado** (UUID v7) a menos que você passe `sagaId` — forneça o seu quando quiser reusá-lo como chave de correlação no seu próprio domínio (um id de batch/run). `retainedFor` define uma janela de retenção: assim que a saga chega a um status terminal, o janitor embutido pode deletar a linha depois que essa janela expira.

---

## Quando usar

O mediator é o que você injeta em command handlers e controllers quando quer **iniciar** ou **inspecionar** uma saga. O engine em si é invocado de dentro do `StartAsync` (e do `ResumeAsync` / resumer) — código de aplicação nunca fala com `SagaEngine` direto.

## Quando *não* usar

| Você quer… | Use no lugar |
|---|---|
| despachar command / query comum | [`IAxisMediator.Cqrs`](../AxisMediator/README.md) — o mediator de saga só sabe de sagas |
| publicar um evento relacionado à saga de fora da saga | [`IAxisBus`](../AxisBus/README.md) |
| dirigir o engine para frente por conta própria | chame `ResumeAsync(sagaId)` — nunca chame o engine direto |

---

## `StartAsync<TPayload>(sagaName, payload[, sagaId][, retainedFor])`

Lendo o `SagaMediator.StartAsync` agnóstico de dialeto direto (a implementação do core, compartilhada por todo adapter de storage):

| Validação | Retorna |
|---|---|
| `sagaName` vazio | `AxisError.ValidationRule("SAGA_NAME_REQUIRED")` |
| `sagaId` vazio | `AxisError.ValidationRule("SAGA_ID_REQUIRED")` |
| `sagaName` não está no registry | `AxisError.NotFound(AxisSagaErrors.SagaDefinitionNotFound)` |
| `JsonSerializer.Serialize` lança | `AxisError.InternalServerError(AxisSagaErrors.PayloadSerializationFailed)` |
| `InsertAsync` tem sucesso | `AxisResult.Ok(sagaId)`, depois **dispara o engine em background** |
| `InsertAsync` esbarra em id duplicado | `AxisError.Conflict("SAGA_ID_ALREADY_EXISTS")` |
| qualquer outra falha no `InsertAsync` | `AxisError.InternalServerError(AxisSagaErrors.PersistenceFailed)` |

> `sagaName` é validado **antes** de `sagaId`. Os desfechos de conflito (`SAGA_ID_ALREADY_EXISTS`) e de persistência (`PersistenceFailed`) são produzidos pelo `ISagaInstanceStore.InsertAsync` do dialeto — o mediator só os repassa.

A instância é persistida como `Status = Pending`, `Version = 1`, `CurrentStage = null`. O engine pega em background, avança para o primeiro forward stage e roda dali.

> **Borda afiada:** o chamador recebe `Ok(sagaId)` *assim que a linha está no banco*. O engine roda **assincronamente** via `Task.Run`. Se você precisa esperar a saga terminar, faça polling em `GetByIdAsync` por `Status = Completed` / `Failed` / `Compensated`.

## `GetByIdAsync(sagaId)` e `GetByIdAsync<TPayload>(sagaId)`

| Overload | Retorna | Use quando |
|---|---|---|
| não-tipado | `AxisResult<AxisSagaInstance>` (com `PayloadJson` como string crua) | você não tem `TPayload` em mãos (logging, admin endpoints) |
| tipado | `AxisResult<AxisSagaInstance<TPayload>>` (com `Payload` desserializado) | você tem o tipo de payload e quer ler seus campos |

| Falha | Razão |
|---|---|
| `AxisError.NotFound(SagaInstanceNotFound)` | nenhuma linha com esse id |
| `AxisError.InternalServerError(PayloadDeserializationFailed)` | o JSON armazenado não pode ser desserializado em `TPayload` |
| `AxisError.InternalServerError(PersistenceFailed)` | exceção inesperada do DB |

`AxisSagaInstance` carrega o estado completo: `Status`, `CurrentStage`, `PayloadJson`, `LastErrorCode`/`LastErrorMessage`, `Version`, `CreatedAt`, `UpdatedAt`.

## `ResumeAsync(sagaId)`

Um sinal fire-and-forget que diz "por favor, dirija essa saga para frente". O mediator agenda o engine para rodar (`Task.Run` → um escopo de DI novo → `SagaEngine.ExecuteAsync`) e a chamada retorna `Ok` na hora — não espera o engine. Re-disparar é inofensivo para uma saga que já está `Completed` / `Compensated` / `Failed`: o engine re-adquire o lease de execução via `AcquireLeaseAsync`, que exclui sagas terminais, então a execução simplesmente não acha o que reivindicar e para.

> É o que [`IAxisSagaResumer`](resumer.md) chama por baixo para cada instância cujo lease expirou.

---

## Exemplos reais

### 1. Iniciar uma saga de um controller

```csharp
public class OrdersController(IAxisSagaMediator sagas) : ControllerBase
{
    [HttpPost]
    public Task<AxisResult<string>> CreateAsync(CreateOrderRequest req)
        => sagas.StartAsync(
            sagaName: OrderSagaDefinition.Name,
            payload:  new OrderPayload(req.OrderId, req.Amount, req.CustomerEmail),
            sagaId:   $"order-{Guid.CreateVersion7()}");
}
```

**Por que compensa:** o controller retorna na hora com o `sagaId`. A orquestração roda em background — clientes fazem polling em `GET /orders/{sagaId}/status` ou reagem aos eventos do bus.

### 2. Ler o estado atual de um admin endpoint

```csharp
public class SagaAdminController(IAxisSagaMediator sagas) : ControllerBase
{
    [HttpGet("{sagaId}")]
    public async Task<IResult> GetAsync(string sagaId)
    {
        var result = await sagas.GetByIdAsync(sagaId);
        return result.Match(
            onSuccess: i      => Results.Ok(new { i.Status, i.CurrentStage, i.LastErrorCode, i.Version }),
            onFailure: errors => Results.NotFound());
    }
}
```

**Por que compensa:** admin e equipe de suporte podem inspecionar o status de qualquer saga sem ler o banco direto. O overload tipado (`GetByIdAsync<TPayload>`) deixa o payload disponível para dashboards mais ricos.

### 3. Forçar um resume depois de corrigir um problema downstream

```csharp
public Task<AxisResult> RetryAsync(string sagaId)
    => sagas.ResumeAsync(sagaId);
```

**Por que compensa:** quando uma falha externa transiente é corrigida (uma fila drenando, um fornecedor voltando online), um operador pode re-disparar o engine sem tocar no banco.

---

## Veja também

- [Configurator](configuration.md) — declare a saga antes de iniciar
- [Stage handlers](stage-handlers.md) — o que roda em cada passo
- [Resumer · `IAxisSagaResumer`](resumer.md) — resumption automática
- Adapters de storage (Postgres, MySQL, …) — o que o `StartAsync` escreve e o que o engine faz; veja o [adapter Postgres](postgres-adapter.md) para um passo a passo concreto

---

↩ [Voltar à documentação do AxisSaga](README.md)
