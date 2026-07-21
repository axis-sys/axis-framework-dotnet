# Keyed multi-database

> `AddPostgresUnitOfWork(serviceKey, connectionString)` aceita uma key, então você pode plugar **mais de um database** no mesmo app e resolver cada unit-of-work via `[FromKeyedServices(key)]`.

```csharp
services.AddPostgresUnitOfWork(serviceKey: "main",     connectionString: mainCs);
services.AddPostgresUnitOfWork(serviceKey: "auditing", connectionString: auditCs);

public class TransferHandler(
    [FromKeyedServices("main")]     IAxisUnitOfWork mainUow,
    [FromKeyedServices("auditing")] IAxisUnitOfWork auditUow) { /* ... */ }
```

---

## Quando usar

- Um **database separado para auditoria / read models / analytics**.
- Um modelo **per-tenant database** onde a key é o tenant id.
- Um cenário de **migração** onde você lê do banco antigo e escreve no novo.

## Quando *não* usar

| Você quer… | Use no lugar |
|---|---|
| ficar em um banco | uma única chamada `AddPostgresUnitOfWork(serviceKey: "main", …)` |
| coordenar **transações distribuídas** | uma saga com compensações — nenhum `IAxisUnitOfWork` faz two-phase commit Postgres + Postgres |

---

## Como o registro keyed funciona

Lendo `DependencyInjection.AddPostgresUnitOfWork`:

```csharp
services.AddNpgsqlDataSource(connectionString, …, serviceKey: serviceKey);
services.AddKeyedScoped<PostgresUnitOfWorkProvider>(serviceKey);
services.AddKeyedScoped<IAxisUnitOfWork>     (serviceKey, (sp, key) => sp.GetRequiredKeyedService<PostgresUnitOfWorkProvider>(key).GetUnitOfWork(sp, key));
services.AddKeyedScoped<IPostgresUnitOfWork> (serviceKey, (sp, key) => sp.GetRequiredKeyedService<PostgresUnitOfWorkProvider>(key).GetUnitOfWork(sp, key));
```

- Cada chamada `AddPostgresUnitOfWork(key, cs)` registra um `NpgsqlDataSource` **keyed** por `key`.
- Um `PostgresUnitOfWorkProvider` keyed cacheia um `PostgresUnitOfWork` por key dentro do scope.
- Tanto `IAxisUnitOfWork` quanto `IPostgresUnitOfWork` resolvem para a mesma instância para aquela key — leitura e escrita pela mesma conexão.

> **Importante:** o `IAxisUnitOfWork` resultante é **scoped**. Dois databases significam dois unit-of-works, e um `InTransactionAsync` só consegue commitar o **seu próprio**.

---

## Escolhendo keys

Algumas convenções úteis:

| Convenção | Exemplo |
|---|---|
| Nome lógico de database | `"main"`, `"audit"`, `"reporting"` |
| Por tenant | `tenantKey` (a key do tenant como string) |
| Por feature | `"payments"`, `"identity"` |

Qualquer que seja a key, chamadores precisam usar a **mesma string** quando injetam (`[FromKeyedServices("main")]`).

---

## Exemplos reais

### 1. Dois databases, duas transações

```csharp
public class CreateAuditedOrderHandler(
    [FromKeyedServices("main")]     IAxisUnitOfWork mainUow,
    [FromKeyedServices("auditing")] IAxisUnitOfWork auditUow,
    OrderWriter orderWriter,
    AuditWriter auditWriter)
{
    public Task<AxisResult<CreateOrderResponse>> HandleAsync(CreateOrderCommand cmd)
        => mainUow.InTransactionAsync(() =>
            orderWriter.CreateAsync(cmd)
                .ThenAsync(order => auditUow.InTransactionAsync(() => auditWriter.RecordCreateAsync(order)).Map(_ => order))
                .MapAsync(order => new CreateOrderResponse { OrderId = order.OrderId }));
}
```

**Por que compensa:** cada database commita por si. O pedido persiste atomicamente em `main`; a linha de auditoria persiste atomicamente em `auditing`. Uma falha no lado da auditoria **não** faz rollback do pedido — leia o trade-off com cuidado.

> Se você precisa que ambos sejam atômicos, não dá — isso é território de two-phase commit. Use uma saga, ou grave a auditoria via um **outbox** no banco main e drene assincronamente.

### 2. Slicing por tenant

```csharp
public class PerTenantUowFactory(IServiceProvider sp)
{
    public IAxisUnitOfWork For(string tenantKey)
        => sp.GetRequiredKeyedService<IAxisUnitOfWork>(tenantKey);
}

// registro
foreach (var tenantKey in tenantKeys)
    services.AddPostgresUnitOfWork(serviceKey: tenantKey, connectionString: ConnectionFor(tenantKey));
```

**Por que compensa:** o mesmo código de handler funciona contra cada database de tenant; o roteamento é uma única consulta keyed-DI.

---

## Veja também

- [Adapter Postgres](postgres-adapter.md) — o que cada chamada `AddPostgresUnitOfWork` fia
- [`InTransactionAsync`](in-transaction.md) — roda contra um unit-of-work por vez
- [O contrato `IAxisUnitOfWork`](iaxisunitofwork.md) — a abstração de que o chamador depende

---

↩ [Voltar à documentação do AxisRepository](README.md)
