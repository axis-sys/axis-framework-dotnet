# Configurator · `IAxisSagaConfigurator<TPayload>`

> Um builder fluente que produz uma `AxisSagaDefinition`. Dois métodos para declarar stages (`AddStage`, `AddErrorStage`); uma pequena cadeia para fiar o roteamento de sucesso e a compensação em cada um.

```csharp
public interface IAxisSagaConfigurator<out TPayload> where TPayload : class
{
    IAxisSagaStageBuilder<TPayload> AddStage(string stageName);
    IAxisSagaStageBuilder<TPayload> AddErrorStage(string stageName);
}

public interface IAxisSagaStageBuilder<out TPayload> where TPayload : class
{
    IAxisSagaStageBuilder<TPayload> NextStageOnSuccess(string nextStageName);
    IAxisSagaStageBuilder<TPayload> FinishOnSuccess();

    IAxisSagaStageBuilder<TPayload> RouteToOnError(params string[] errorStageNames);
}
```

---

## Quando usar

Defina toda saga em um pequeno método estático cujo único trabalho é chamar este configurator. O resultado — uma `AxisSagaDefinition` compilada — é carregado pelo registry no startup e persistido em `axis_saga.saga_definitions` pelo initializer.

## Quando *não* usar

| Você quer… | Use no lugar |
|---|---|
| rodar branching **condicional** dentro de um stage | o stage handler — ele retorna `AxisResult<TPayload>`, o caminho `Error` dispara as rotas |
| rodar um workflow single-stage | um [command do `AxisMediator`](../AxisMediator/README.md) — sagas pagam pelo que são |
| reagir a eventos de fora da saga | um `IAxisEventHandler` comum — sagas dirigem a si mesmas |

---

## A API fluente em profundidade

### `AddStage(name)` / `AddErrorStage(name)`

| Método | Adiciona em | Notas |
|---|---|---|
| `AddStage(name)` | `ForwardStages` | o primeiro forward stage é o ponto de entrada da saga |
| `AddErrorStage(name)` | `ErrorStages` | só alcançável via `RouteToOnError(...)` em outro stage |

Nomes devem ser **não-vazios** e **únicos** em toda a definição. O configurator lança em build-time se qualquer regra for violada.

Para publicar eventos de domínio, faça isso a partir do próprio stage handler — dentro do seu `ExecuteAsync`, na mesma unit of work que persiste a mudança de estado (veja [Stage handlers](stage-handlers.md)). O configurator fia apenas o fluxo: roteamento e compensação.

### `NextStageOnSuccess(nextStageName)` vs. `FinishOnSuccess()`

Mutuamente exclusivos — chame um ou outro.

| Chamada | Efeito |
|---|---|
| `NextStageOnSuccess(name)` | o engine seta `CurrentStage = name` depois do sucesso e roda aquele handler |
| `FinishOnSuccess()` | o engine seta `Status = Completed` (ou `Compensated` se o stage era error stage) |

### `RouteToOnError(params errorStageNames)`

Lista os error stages para percorrer em ordem quando o stage atual falha. Se a lista for vazia, a saga termina em `Failed` e para ali.

```csharp
saga.AddStage("ChargeCard")
    .RouteToOnError("RefundStock", "CompensateOrder");
// em falha: roda RefundStock → CompensateOrder
```

---

## O que o configurator valida

Lendo `AxisSagaConfigurator<TPayload>.Build()` direto:

| Check | Lança quando |
|---|---|
| `ForwardStages` não vazio | nenhum `AddStage(...)` foi chamado |
| `StageName` não vazio | qualquer `AddStage("")` ou `AddErrorStage("")` |
| Nomes únicos entre forward + error stages | duplicate `StageName` (case-sensitive `Ordinal`) |
| Todo `NextStageOnSuccess` referencia um stage conhecido | nome desconhecido |
| Toda entrada de `RouteToOnError` referencia um stage conhecido | nome desconhecido |

Os erros saem como `InvalidOperationException` com mensagem clara no app startup — não em runtime da saga.

---

## Exemplo real — a "OrderSaga"

```csharp
public static class OrderSagaDefinition
{
    public const string Name = "OrderSaga";

    public static void Configure(IAxisSagaConfigurator<OrderPayload> saga)
    {
        saga.AddStage("ReserveStock")
            .NextStageOnSuccess("ChargeCard")
            .RouteToOnError("CompensateOrder");

        saga.AddStage("ChargeCard")
            .FinishOnSuccess()
            .RouteToOnError("RefundStock", "CompensateOrder");

        saga.AddErrorStage("RefundStock")
            .NextStageOnSuccess("CompensateOrder");

        saga.AddErrorStage("CompensateOrder")
            .FinishOnSuccess();
    }
}
```

**Por que compensa:** a orquestração inteira lê como um checklist. Adicionar um terceiro forward stage é uma chamada de método; mudar a cadeia de compensação é uma linha em `RouteToOnError`. Os handlers não sabem do roteamento — só retornam `Ok` ou `Error`.

---

## Veja também

- [Conceitos · stages e rotas](concepts.md) — as engrenagens
- [Stage handlers](stage-handlers.md) — o que roda em cada stage
- [Mediator · `IAxisSagaMediator`](mediator.md) — inicie a saga depois de configurada
- [Adapter Postgres](postgres-adapter.md) — como um storage adapter incluído (Postgres, MySQL, …) fia o engine (do core dialect-agnóstico) que consome a definição

---

↩ [Voltar à documentação do AxisSaga](README.md)
