# Conceitos · stages e rotas

> Antes de ler qualquer código, construa o modelo. Uma saga é uma pequena máquina de estados em formato de dados: uma lista de forward stages, uma lista de error stages, e para onde rotear em caso de sucesso ou falha.

```
ReserveStock ──ok──▶ ChargeCard ──ok──▶  (Completed)
     │                    │
   error                error
     │                    │
     ▼                    ▼
CompensateOrder      RefundStock ──ok──▶ CompensateOrder
```

---

## Máquina de status — `AxisSagaStatus`

| Valor | Significado |
|---|---|
| `Pending` | a linha da instância foi inserida; o engine ainda não começou |
| `Running` | o engine está executando forward stages |
| `Completed` | o último forward stage terminou com `FinishOnSuccess()` |
| `Failed` | um forward stage falhou e não havia error stages roteados |
| `Compensating` | um forward stage falhou; o engine está percorrendo os error stages roteados |
| `Compensated` | todo error stage roteado terminou com sucesso |

## Status de stage — `AxisSagaStageStatus`

| Valor | Onde é escrito |
|---|---|
| `Started` | linha adicionada em `saga_stage_logs` quando o engine pega o stage |
| `Completed` | linha adicionada depois que `IAxisSagaStageHandler` retorna `Ok` |
| `Failed` | linha adicionada depois que `IAxisSagaStageHandler` retorna `Error` |

---

## A anatomia de um stage

Cada stage na definição tem quatro partes configuráveis:

| Parte | Propósito |
|---|---|
| `StageName` | único dentro da saga |
| `IsErrorStage` | `false` para forward, `true` para error/compensação |
| `NextStageOnSuccess` | para onde ir em `Ok` |
| `RouteToOnError` | para onde ir em `Error` |

Quando um stage tem sucesso:

1. O engine escreve uma linha `Completed` em `saga_stage_logs`.
2. Se `NextStageOnSuccess` está setado, o engine vai para lá. Caso contrário, a saga completa (`FinishOnSuccess`).

Quando um stage falha:

1. O engine escreve uma linha `Failed` em `saga_stage_logs`.
2. Se `RouteToOnError` não está vazio, o engine seta status para `Compensating` e percorre os error stages listados em ordem. Caso contrário, o status da saga vira `Failed`.

---

## Forward stages vs. error stages

| Aspecto | Forward (`AddStage`) | Error (`AddErrorStage`) |
|---|---|---|
| Onde aparece | `ForwardStages` | `ErrorStages` |
| Propósito padrão | mover o estado de negócio para frente | desfazer ou notificar (compensação) |
| Roteado para em erro | sim (configurável por stage) | também possível, mas tipicamente a cadeia termina aqui |
| Conta para o status `Compensated` | não | sim |

> Error stages ainda são implementados por um `IAxisSagaStageHandler` — mesma interface, mesmo formato `AxisResult<TPayload>`. A única diferença é **onde** ficam na definição e **como** o engine trata suas falhas.

---

## A forma completa — `AxisSagaDefinition`

```csharp
public record AxisSagaDefinition
{
    public required string SagaName { get; init; }
    public required Type PayloadType { get; init; }
    public required IReadOnlyList<AxisSagaStageDefinition> ForwardStages { get; init; }
    public required IReadOnlyList<AxisSagaStageDefinition> ErrorStages   { get; init; }

    public AxisSagaStageDefinition FirstForwardStage => ForwardStages[0];
    public AxisSagaStageDefinition? GetStage(string stageName); // procura nas duas listas
}
```

O configurator constrói e **valida** esse objeto: ao menos um forward stage; nomes de stage únicos; cada `NextStageOnSuccess` e `RouteToOnError` referencia um stage que realmente existe. Definições inválidas lançam no startup, não em runtime.

---

## Exemplo real — uma "OrderSaga" passo a passo

| Passo | Status | Stage | `saga_stage_logs` |
|---|---|---|---|
| 1. `StartAsync` | `Pending` | (nenhum) | (nenhum) |
| 2. engine pega | `Running` | `ReserveStock` | `Started(ReserveStock)` |
| 3. handler retorna `Ok` | `Running` | `ReserveStock` | `Completed(ReserveStock)` |
| 4. engine avança | `Running` | `ChargeCard` | `Started(ChargeCard)` |
| 5. handler retorna `Ok` | `Completed` | (nenhum) | `Completed(ChargeCard)` |

Se o passo 5 tivesse falhado:

| Passo | Status | Stage | `saga_stage_logs` |
|---|---|---|---|
| 5'. handler retorna `Error` | `Compensating` | `RefundStock` | `Failed(ChargeCard)` depois `Started(RefundStock)` |
| 6'. handler retorna `Ok` | `Compensating` | `CompensateOrder` | `Completed(RefundStock)`, `Started(CompensateOrder)` |
| 7'. handler retorna `Ok` | `Compensated` | (nenhum) | `Completed(CompensateOrder)` |

---

## Veja também

- [Configurator · `IAxisSagaConfigurator<TPayload>`](configuration.md) — o builder fluente que produz a definição
- [Stage handlers](stage-handlers.md) — o código que roda cada stage
- [Mediator · `IAxisSagaMediator`](mediator.md) — inicie uma saga e leia seu estado
- [Resumer · `IAxisSagaResumer`](resumer.md) — o worker embutido que re-dispara instâncias travadas
- [Adapter Postgres](postgres-adapter.md) — um dos adapters de storage embutidos (Postgres, MySQL, …) que persiste essa máquina
- [Schema do banco](database-schema.md) — o que é persistido

---

↩ [Voltar à documentação do AxisSaga](README.md)
