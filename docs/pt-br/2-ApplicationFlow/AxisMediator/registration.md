# Registro e scanning

> Duas chamadas de extension cuidam de tudo: `AddAxisMediator()` fia o mediator e seus accessors, `AddCqrsMediator(assembly)` scaneia o assembly por command / query / stream / event handlers e registra cada um contra sua interface.

```csharp
builder.Services
    .AddAxisMediator()
    .AddCqrsMediator(Assembly.GetExecutingAssembly());
```

---

## Quando usar

`AddAxisMediator()` exatamente uma vez por app. `AddCqrsMediator(assembly)` uma vez por assembly que contenha handlers — a maioria dos apps chama para o assembly da API; alguns chamam de novo para uma class library que tem mais handlers.

## Quando *não* usar

| Você quer… | Use no lugar |
|---|---|
| registrar um único handler manualmente | `services.AddTransient<IAxisCommandHandler<TCommand>, MyHandler>();` (o scanner faz isso por você) |
| registrar behaviours de validação / logging / telemetria | as `Add*` extensions próprias ([`AddAxisLogger`](../../1-Observability/AxisLogger/README.md), [`AddAxisValidator`](../AxisValidator/README.md), [`AddOpenTelemetryAxis`](../../1-Observability/AxisTelemetry/README.md)) |

---

## `AddAxisMediator()`

Lendo `DependencyInjection.AddAxisMediator`:

| Tipo | Lifetime | Propósito |
|---|---|---|
| `IAxisMediatorHandler` → `AxisMediatorHandler` | scoped | o dispatcher (`ExecuteAsync`/`QueryAsync`/`StreamAsync`) |
| `IAxisMediator` → `AxisMediator` | scoped | o contexto ambiente (construtor define `accessor.AxisMediator = this`, `Dispose` limpa) |
| `IAxisMediatorAccessor` → `AxisMediatorAccessor` | singleton | guarda o último `IAxisMediator` construído (para adapters fora do scope) |
| `IAxisMediatorContextAccessor` → `AxisMediatorContextAccessor` | singleton | storage `AsyncLocal` para `OriginId`/`JourneyId`/`AxisEntityId`/`CancellationToken` |

## `AddPerformanceBehavior()`

| Tipo | Lifetime | Propósito |
|---|---|---|
| `IAxisPipelineBehavior<,>` → `PerformanceBehavior<,>` | transient | o alerta de slow-request, veja [Performance behaviour](performance-behavior.md) |

## `AddCqrsMediator(assembly)`

Lendo `CQRS.DependencyInjection.AddCqrsMediator` direto — seis chamadas `RegisterHandlers(...)` por assembly, uma para cada interface de handler:

```csharp
RegisterHandlers(services, assembly, typeof(IAxisCommand<>));          // markers de comando tipado
RegisterHandlers(services, assembly, typeof(IAxisCommandHandler<>));    // handlers void-command
RegisterHandlers(services, assembly, typeof(IAxisCommandHandler<,>));   // handlers typed-command
RegisterHandlers(services, assembly, typeof(IAxisQueryHandler<,>));     // query handlers
RegisterHandlers(services, assembly, typeof(IAxisStreamQueryHandler<,>)); // stream-query handlers
RegisterHandlers(services, assembly, typeof(IAxisEventHandler<>));      // event handlers (para AxisBus)
```

`RegisterHandlers`:

1. Pega cada classe não-abstrata e não-genérica do assembly.
2. Mantém as que implementam ao menos uma interface genérica que case com o formato de handler.
3. Para cada variante de interface que case, chama `services.AddTransient(interface, implementation)`.

| Comportamento | Detalhe |
|---|---|
| Lifetime | **transient** — uma instância de handler por dispatch |
| Múltiplos handlers por tipo de request | só o último registrado ganha para commands e queries (formatos single-handler); eventos aceitam vários |
| Tipos internos | incluídos (o scanner não filtra por `IsPublic`) |
| Handlers genéricos | não incluídos (`IsGenericType: false`) |

---

## Exemplos reais

### 1. App com handlers divididos em dois assemblies

```csharp
builder.Services
    .AddAxisMediator()
    .AddCqrsMediator(typeof(CreateOrderHandler).Assembly)       // OrderModule
    .AddCqrsMediator(typeof(CreatePersonHandler).Assembly);     // PersonModule
```

**Por que compensa:** cada módulo é dono de seus handlers, e a raiz de composição faz opt-in. Módulo novo? Mais uma linha, sem refazer.

### 2. Adicionando um handler manualmente

```csharp
services.AddTransient<IAxisCommandHandler<RecalculateInvoicesCommand>, RecalculateInvoicesHandler>();
```

Se o handler é genérico, ou você quer um lifetime diferente, registre por conta própria. O scanner pula tipos genéricos para você gerenciar à mão.

### 3. Substituindo um handler em testes

```csharp
services.RemoveAll(typeof(IAxisCommandHandler<CreateOrderCommand, CreateOrderResponse>));
services.AddTransient<IAxisCommandHandler<CreateOrderCommand, CreateOrderResponse>, FakeCreateOrderHandler>();
```

**Por que compensa:** o resto do pipeline está intacto (logging, validação, telemetria). Só o handler é trocado — o teste usa todo behaviour que o caminho de produção usa.

---

## Veja também

- [Primeiros passos](getting-started.md) — o setup mínimo
- [Despachando · `IAxisMediatorHandler`](dispatching.md) — o que o dispatcher faz em runtime
- [Pipeline behaviours](pipeline-behaviors.md) — o ponto de extensão open-generic
- [`AxisLogger`](../../1-Observability/AxisLogger/README.md) · [`AxisValidator`](../AxisValidator/README.md) · [`AxisTelemetry`](../../1-Observability/AxisTelemetry/README.md) — `Add*` extensions próprias dos behaviours embarcados

---

↩ [Voltar à documentação do AxisMediator](README.md)
