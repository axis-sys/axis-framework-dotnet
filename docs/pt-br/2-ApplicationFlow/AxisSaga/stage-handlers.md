# Stage handlers · `IAxisSagaStageHandler<TPayload>`

> Cada stage numa saga tem um handler que executa o trabalho e retorna `AxisResult<TPayload>`. O handler não conhece roteamento, eventos ou persistência — ele só transforma o payload e reporta sucesso ou falha.

```csharp
public interface IAxisSagaStageHandler<TPayload> where TPayload : class
{
    string SagaName { get; }
    string StageName { get; }

    Task<AxisResult<TPayload>> ExecuteAsync(TPayload payload);
}
```

---

## Quando usar

Um handler por par `(SagaName, StageName)`. O engine nunca resolve handlers sozinho — ele passa pelo `ISagaStageHandlerInvoker`, que abre seu **próprio scope de DI fresco por stage**, resolve `GetServices(typeof(IAxisSagaStageHandler<>).MakeGenericType(payloadType))` e casa por `SagaName` + `StageName`. Esses dois nomes são lidos do tipo da **interface**, não da classe concreta, então um handler que implementa o contrato **explicitamente** (`string IAxisSagaStageHandler<T>.SagaName => …`) ainda casa.

## Quando *não* usar

| Você quer… | Use no lugar |
|---|---|
| rodar lógica de negócio **síncrona** sem orquestração | um [command do `AxisMediator`](../AxisMediator/README.md) |
| fan-out para **muitos destinatários** | publique um evento no [`IAxisBus`](../AxisBus/README.md) a partir do handler, na sua unit of work |
| atravessar **vários serviços** sem persistência local | outra saga, ou um process manager com messaging |

---

## Anatomia

| Membro | Propósito |
|---|---|
| `SagaName` | a saga a que este handler pertence — precisa casar com o `Name` do configurator |
| `StageName` | o stage que este handler implementa — precisa casar com uma chamada `AddStage(...)` / `AddErrorStage(...)` |
| `ExecuteAsync(payload)` | roda o stage; retorna `AxisResult<TPayload>` |

### Por que ambos os nomes são propriedades (não generics)

Um handler é casado **por conteúdo**, não por tipo. Dois stages em duas sagas podem usar o mesmo tipo `TPayload`; o invoker ainda pega o handler certo porque cada um se identifica com `SagaName` + `StageName` (lidos do tipo da interface, então implementações explícitas de interface também casam).

> `IAxisCache` e outras abstrações Axis podem ser injetadas no construtor do handler como qualquer serviço. Cada stage roda no seu **próprio scope de DI** que o invoker cria e descarta em volta da chamada — então infraestrutura scoped (ex.: uma unit of work que detém uma conexão + transação) é fresca por stage e nunca vaza uma transação faulted para o próximo stage nem para a compensação.

---

## O que o handler precisa fazer

1. Aplicar os side effects que o stage precisa (reservar stock, cobrar um cartão, gravar uma linha).
2. Retornar um `AxisResult<TPayload>` cujo **payload** é o que o *próximo* stage vai ver. O engine persiste o payload retornado como o novo `PayloadJson`.

> O payload pode mutar entre stages. Um padrão típico é adicionar campos à medida que a saga progride (ex.: `OrderPayload.PaymentToken` é preenchido pelo `ChargeCard`, usado pelo `Compensate`).

---

## Retornando sucesso vs. falha

| Retorno | Reação do engine |
|---|---|
| `AxisResult.Ok(payload)` | persiste o novo payload, loga `Completed`, avança para `NextStageOnSuccess` (ou finaliza) |
| `AxisError.X(...).Map(...)` (ou qualquer falha) | persiste o payload atual, loga `Failed` com `LastErrorCode`/`LastErrorMessage`, percorre `RouteToOnError` |
| **lança** | o invoker captura na borda, loga a exceção e a transforma num resultado de falha (`AxisError.InternalServerError("STAGE_HANDLER_THREW_…")`). O engine então a trata como qualquer falha de stage — percorre `RouteToOnError` (compensação) quando o stage tem rotas de erro, ou falha a saga quando não tem. Uma exceção lançada **não** é um caminho de código diferente de uma falha retornada. |

> Prefira retornar uma falha tipada a lançar: embrulhe chamadas de infra arriscadas com `AxisResult.TryAsync` dentro do handler para que uma `DbException` lançada vire um `AxisError.InternalServerError(...)` explícito com um code significativo, em vez do `STAGE_HANDLER_THREW_…` genérico que o invoker sintetiza.

---

## Exemplos reais

### 1. Reservar stock com uma port

```csharp
public class ReserveStockHandler(IStockPort stock) : IAxisSagaStageHandler<OrderPayload>
{
    public string SagaName  => OrderSagaDefinition.Name;
    public string StageName => "ReserveStock";

    public Task<AxisResult<OrderPayload>> ExecuteAsync(OrderPayload payload)
        => stock.ReserveAsync(payload.OrderId, payload.Quantity)
            .MapAsync(reservationId => payload with { ReservationId = reservationId });
}
```

**Por que compensa:** o handler lê como `stock → reserve → armazena o reservation id de volta no payload`. Se a port retorna `Conflict("OUT_OF_STOCK")`, o engine roteia para compensação; se a chamada tem sucesso, o próximo stage recebe o payload atualizado.

### 2. Cobrar um cartão com mapeamento explícito de falha-para-roteamento

```csharp
public class ChargeCardHandler(IPaymentsPort payments) : IAxisSagaStageHandler<OrderPayload>
{
    public string SagaName  => OrderSagaDefinition.Name;
    public string StageName => "ChargeCard";

    public Task<AxisResult<OrderPayload>> ExecuteAsync(OrderPayload payload)
        => payments.ChargeAsync(payload.PaymentMethod, payload.Amount)
            .MapAsync(token => payload with { PaymentToken = token })
            .MapErrorAsync(errs => errs.Select(e =>
                e.Code is "CARD_DECLINED" ? AxisError.BusinessRule("CARD_DECLINED") : e).ToArray());
}
```

**Por que compensa:** a saga só conhece o roteamento dos stages listados — o handler normaliza o código de erro upstream para que o resto do sistema possa pivotar num nome estável.

### 3. Um handler de compensação puro

```csharp
public class RefundStockHandler(IStockPort stock) : IAxisSagaStageHandler<OrderPayload>
{
    public string SagaName  => OrderSagaDefinition.Name;
    public string StageName => "RefundStock";

    public Task<AxisResult<OrderPayload>> ExecuteAsync(OrderPayload payload)
        => string.IsNullOrEmpty(payload.ReservationId)
            ? Task.FromResult(AxisResult.Ok(payload))            // nada para refundar
            : stock.ReleaseAsync(payload.ReservationId).MapAsync(_ => payload);
}
```

**Por que compensa:** error stages são idempotentes por design — `payload.ReservationId` pode estar vazio se `ReserveStock` nunca rodou. O handler simplesmente retorna `Ok` e o engine vai para o próximo error stage.

---

## Registrando handlers

```csharp
services.AddAxisSagaHandlers(Assembly.GetExecutingAssembly());
```

Lendo `DependencyInjection.AddAxisSagaHandlers` direto: o scanner acha cada classe não-abstrata e não-genérica implementando `IAxisSagaStageHandler<>` e registra como **scoped** contra cada variante de interface que implementa.

---

## Veja também

- [Conceitos · stages e rotas](concepts.md) — as engrenagens
- [Configurator](configuration.md) — a definição contra a qual o handler é casado
- [Mediator · `IAxisSagaMediator`](mediator.md) — `StartAsync` dispara o primeiro handler
- [Resumer · `IAxisSagaResumer`](resumer.md) — o worker embutido que re-dispara (e portanto re-roda) um handler após um crash ou um lease expirado — por que handlers precisam ser idempotentes

---

↩ [Voltar à documentação do AxisSaga](README.md)
