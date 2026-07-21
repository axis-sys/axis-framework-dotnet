# Adapter custom · escreva seu próprio `IAxisUnitOfWork`

> Troque o adapter Postgres por SQL Server, Mongo, EF Core — ou escreva um test double que grava cada transação. Implemente quatro métodos (mais dispose), registre sua classe como `IAxisUnitOfWork`.

```csharp
public class SqlServerUnitOfWork(IAxisMediator mediator, SqlConnection conn, IAxisLogger<SqlServerUnitOfWork> log)
    : IAxisUnitOfWork
{
    private SqlTransaction? _tx;

    public async Task<AxisResult> StartAsync()
    {
        try
        {
            if (conn.State != ConnectionState.Open) await conn.OpenAsync(mediator.CancellationToken);
            _tx = (SqlTransaction)await conn.BeginTransactionAsync(mediator.CancellationToken);
            return AxisResult.Ok();
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Failed to start SQL Server transaction");
            return AxisError.InternalServerError("SQLSERVER_ERROR_STARTING_CONNECTION");
        }
    }

    // SaveChangesAsync, RollbackAsync, ReleaseConnectionAsync, Dispose, DisposeAsync …
}
```

---

## Quando usar

- Outro banco relacional (SQL Server, Oracle, SQLite).
- Um store não-relacional com semântica de transação (Mongo com transações multi-documento).
- Uma stack de ORM (EF Core, Linq2Db) — embrulhe o ciclo de vida do `DbContext`.
- Um test double que captura cada commit/rollback para assertion.

## Quando *não* usar

| Você quer… | Use no lugar |
|---|---|
| ficar em Postgres | o [`AxisRepository.Postgres`](postgres-adapter.md) embarcado |
| ficar em MySQL | o [`AxisRepository.MySql`](mysql-adapter.md) embarcado |
| adicionar transações cross-store | uma saga com compensações |

---

## O contrato que você precisa honrar

| Comportamento | Obrigatório | Razão |
|---|---|---|
| Retorne `Task<AxisResult>` de cada primitiva; nunca lance cooperativamente | sim | a ferrovia depende disso |
| `StartAsync` é idempotente dentro de um scope | sim | `InTransactionAsync` pode chamá-lo depois que outro chamador já iniciou; não estoure |
| `SaveChangesAsync` limpa o handle da transação | sim | para que uma chamada de follow-up sem re-start falhe limpa |
| `RollbackAsync` é no-op quando não há transação | sim | senão `InTransactionAsync` pode fazer double-rollback durante o desempilhamento |
| Cancelamento vem de `IAxisMediator.CancellationToken` | recomendado | espelha o adapter embarcado e cada outra package do Axis |
| Trace via `IAxisTelemetry` e logue via `IAxisLogger` | recomendado | observabilidade fica uniforme |

---

## Exemplo real — um test double em memória

```csharp
public class FakeUnitOfWork : IAxisUnitOfWork
{
    public bool Started   { get; private set; }
    public bool Committed { get; private set; }
    public bool RolledBack{ get; private set; }

    public Task<AxisResult> StartAsync()       { Started   = true;  return Task.FromResult(AxisResult.Ok()); }
    public Task<AxisResult> SaveChangesAsync() { Committed = true;  return Task.FromResult(AxisResult.Ok()); }
    public Task<AxisResult> RollbackAsync()    { RolledBack = true; return Task.FromResult(AxisResult.Ok()); }
    public Task ReleaseConnectionAsync()       => Task.CompletedTask;

    public void Dispose() { }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

// num teste
services.AddScoped<IAxisUnitOfWork, FakeUnitOfWork>();
// depois
Assert.True(uow.Started);
Assert.True(uow.Committed);
Assert.False(uow.RolledBack);
```

**Por que compensa:** o handler é dirigido por `InTransactionAsync`, mas o teste não precisa de um banco. O unit-of-work fake grava cada chamada para assertion — e você pode inverter uma propriedade para simular falha de `Save` se quiser testar o caminho de rollback.

---

## Veja também

- [O contrato `IAxisUnitOfWork`](iaxisunitofwork.md) — a abstração que você precisa implementar
- [`InTransactionAsync`](in-transaction.md) — a máquina de estados que suas primitivas precisam satisfazer
- [Adapter Postgres](postgres-adapter.md) — a referência da caixa
- [Adapter MySQL](mysql-adapter.md) — a outra referência da caixa, mesma base compartilhada

---

↩ [Voltar à documentação do AxisRepository](README.md)
