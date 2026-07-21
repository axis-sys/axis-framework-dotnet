# AxisSaga — Documentação

> 🌐 [English (README principal)](../../../en-us/2-ApplicationFlow/AxisSaga/README.md)

**Orquestração de processos longos e multi-passo com compensações** — definições declarativas de saga (forward stages, error stages, roteamento de sucesso, route-to de compensação), handlers por stage retornando `AxisResult<TPayload>`, um adapter de storage incluído (Postgres, MySQL, …) sobre um core compartilhado que persiste cada instância, cada log de stage e o catálogo de definições, e um resumer por polling embutido que recupera sagas travadas depois de um restart de processo.

```csharp
public static class OrderSagaDefinition
{
    public static void Configure(IAxisSagaConfigurator<OrderPayload> saga)
    {
        saga.AddStage("ReserveStock")
            .NextStageOnSuccess("ChargeCard")
            .RouteToOnError("CompensateOrder");

        saga.AddStage("ChargeCard")
            .FinishOnSuccess()
            .RouteToOnError("RefundStock", "CompensateOrder");

        saga.AddErrorStage("RefundStock")     /* … */;
        saga.AddErrorStage("CompensateOrder") /* … */;
    }
}
```

Use esta página como **mapa**: leia o tronco abaixo (~5 min) e salte direto para o detalhe do grupo que você precisa — sem ler centenas de linhas.

---

## O tronco (leia primeiro)

### O que é uma saga

Uma **saga** é um processo longo composto por **stages** que rodam em ordem. Cada stage executa um `IAxisSagaStageHandler<TPayload>` que retorna `AxisResult<TPayload>` — sucesso avança para o próximo stage; falha roteia para um ou mais **error stages** (o padrão clássico de *compensação*). Se um stage precisa notificar o resto do sistema, é o próprio handler que publica o evento no `IAxisBus`, na mesma unit of work que persiste a mudança de estado do stage — o runtime da saga em si não publica nada.

### Os cinco tipos participantes

| Tipo | O que é |
|---|---|
| `IAxisSagaStageHandler<TPayload>` | handler por stage que roda um passo e retorna `AxisResult<TPayload>` |
| `IAxisSagaConfigurator<TPayload>` | builder fluente que declara as stages e rotas |
| `AxisSagaDefinition` | definição imutável e validada que o registry expõe em runtime |
| `IAxisSagaMediator` | inicia uma saga, busca seu estado, ou pede para retomar |
| `IAxisSagaResumer` | worker de recovery por polling que procura sagas travadas e re-dispara o engine |

→ **[Conceitos · stages e rotas](concepts.md)** · **[`IAxisSagaConfigurator<TPayload>` — o builder fluente](configuration.md)** · **[Escrevendo um stage handler](stage-handlers.md)** · **[Dirigindo uma saga — `IAxisSagaMediator`](mediator.md)** · **[Retomando · `IAxisSagaResumer`](resumer.md)**

### Adapters de storage — Postgres, MySQL, …

O schema é declarado uma vez, agnóstico de dialeto, no core (`AxisSagaSchema`) e renderizado por dialeto: um schema único (`AXIS_SAGA`) com quatro tabelas — **saga_definitions**, **saga_instances**, **saga_stage_logs** e **saga_settings** — mais uma tabela de controle `MIGRATIONS` criada pelo runner de migração do framework. O core traz o `SagaEngine` que dirige uma instância para frente (load → resolve stage → invoca handler → grava log → roteia), o `SagaMediator` que insere uma nova instância e dispara o engine em background, e o resumer embutido. Os adapters de storage incluídos compartilham esse core — atualmente `AxisSaga.Postgres` (`AddAxisSagaPostgres`), `AxisSaga.MySql` (`AddAxisSagaMySql`), entre outros que podem seguir — cada um fornecendo só o data source, as implementações de store e o runner de migração.

→ **[Adapter Postgres](postgres-adapter.md)** · **[Adapter MySQL](mysql-adapter.md)** · **[Schema do banco](database-schema.md)**

### Instalação

```
dotnet add package AxisSaga              # contratos + configurator
dotnet add package AxisSaga.Postgres     # o adapter de storage Postgres (ou AxisSaga.MySql)
```

`AxisSaga` depende de `AxisResult`, `AxisLogger`, `AxisMediator.Contracts`. `AxisSaga.Postgres` adiciona `Npgsql`.

→ Guia completo: **[Primeiros passos](getting-started.md)**

---

## O mapa (salte para o que precisa)

| Grupo | Você quer… | Detalhe |
|---|---|---|
| **Conceitos · stages e rotas** | entender as engrenagens | [concepts.md](concepts.md) |
| **Configurator · `IAxisSagaConfigurator<TPayload>`** ⭐ | declarar uma saga de ponta a ponta | [configuration.md](configuration.md) |
| **Stage handlers** | escrever um handler que roda um stage | [stage-handlers.md](stage-handlers.md) |
| **Mediator · `IAxisSagaMediator`** | iniciar uma saga, ler seu estado, retomar | [mediator.md](mediator.md) |
| **Resumer · `IAxisSagaResumer`** | recuperar instâncias travadas depois de restart | [resumer.md](resumer.md) |
| **Adapter Postgres** | o storage + engine | [postgres-adapter.md](postgres-adapter.md) |
| **Adapter MySQL** | o storage + engine | [mysql-adapter.md](mysql-adapter.md) |
| **Schema do banco** | o que o adapter cria | [database-schema.md](database-schema.md) |
| **Por quê?** | o argumento pela abstração | [why-axissaga.md](why-axissaga.md) |
| **Referência** | cada membro num só lugar | [api-reference.md](api-reference.md) |

**Comece aqui:** [Primeiros passos](getting-started.md) · [Conceitos](concepts.md) · [Por que AxisSaga?](why-axissaga.md)

**Fundamentos:** [Configurator](configuration.md) · [Stage handlers](stage-handlers.md) · [Mediator](mediator.md) · [Resumer](resumer.md)

**Referência e extras:** [Adapter Postgres](postgres-adapter.md) · [Adapter MySQL](mysql-adapter.md) · [Schema do banco](database-schema.md) · [Referência da API](api-reference.md)

---

## Princípios de design

1. **Sagas são declarativas.** Stages, next-on-success e route-to-on-error são *dados* (`AxisSagaDefinition`), não condicionais no handler.
2. **Compensação é first-class.** Um forward stage falho roteia para uma sequência de error stages — não há "lança uma exceção e torce".
3. **Cada passo é logado.** O start / completion / failure de cada stage é uma linha em `axis_saga.saga_stage_logs`. Forense é uma query SQL.
4. **O engine se retoma sozinho.** O core traz um resumer embutido (um worker auto-hospedado) que acha instâncias travadas e redispara o engine — restart de processo não é restart de saga.
5. **Um storage por processo.** `AddAxisSagaPostgres` recusa um segundo registro de propósito: toda saga, em todo BC, compartilha o schema `AXIS_SAGA`.

---

## Licença

Apache 2.0
