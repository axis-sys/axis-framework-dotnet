# Contrato · `IAxisUnitOfWork`

> Quatro primitivas — abrir a transação, commitar, fazer rollback, liberar a conexão — mais dois helpers default-interface (`InTransactionAsync`) que orquestram o trio contra um delegate ciente de `AxisResult`.

```csharp
public interface IAxisUnitOfWork : IDisposable, IAsyncDisposable
{
    Task<AxisResult> StartAsync();
    Task<AxisResult> SaveChangesAsync();
    Task<AxisResult> RollbackAsync();
    Task ReleaseConnectionAsync();

    Task<AxisResult>    InTransactionAsync(Func<Task<AxisResult>> work);
    Task<AxisResult<T>> InTransactionAsync<T>(Func<Task<AxisResult<T>>> work);
}
```

---

## Quando usar

Sempre que um caso de uso escreve no banco e você quer que a escrita seja atômica com o resto do pipeline. Pareie com command handlers, handlers de integração, sagas — em qualquer lugar onde o `SaveChangesAsync` caberia no EF Core.

## Quando *não* usar

| Você quer… | Use no lugar |
|---|---|
| **ler** sem escrever | chame o repositório direto; sem transação para uma leitura única |
| rodar um workflow que **atravessa serviços** | uma [`Saga`](../../2-ApplicationFlow/AxisSaga/README.md) com compensações |
| compartilhar uma transação entre dois stores (Postgres + Redis) | um adapter custom que coordena |

---

## As quatro primitivas

| Método | O que faz | Retorna `IsFailure` quando |
|---|---|---|
| `StartAsync` | abre (ou reutiliza) a conexão e inicia uma transação | a conexão / `BEGIN` falhou |
| `SaveChangesAsync` | commita a transação e a esquece | o `COMMIT` falhou ou a transação já tinha sido limpa |
| `RollbackAsync` | reverte a transação (no-op se nunca foi iniciada) | o `ROLLBACK` falhou |
| `ReleaseConnectionAsync` | devolve a conexão atualmente retida (revertendo qualquer trabalho não commitado) ao pool, para que uma chamada externa lenta no meio de um unit of work não prenda uma conexão pooled e uma transação aberta ociosa durante ela — o próximo comando reabre transparentemente uma conexão e transação novas | nunca falha; é um `Task`, não um `AxisResult` — não tem default de interface, então cada implementador precisa fornecer um; implementações que não fazem pool de conexão simplesmente retornam `Task.CompletedTask` |

Dispor o unit-of-work dispõe a conexão por baixo — não há estado residual entre requisições porque o scope de DI é por requisição.

---

## Os wrappers default-interface

Lendo `IAxisUnitOfWork.InTransactionAsync` direto:

```csharp
async Task<AxisResult> InTransactionAsync(Func<Task<AxisResult>> work)
{
    var start = await StartAsync();
    if (start.IsFailure) return start;

    try
    {
        var result = await work();
        if (result.IsFailure)
        {
            await RollbackAsync();
            return result;
        }
        return await SaveChangesAsync();
    }
    catch
    {
        await RollbackAsync();
        throw;
    }
}
```

| Desfecho do `work()` | Resultado | Side effect |
|---|---|---|
| `Ok()` (ou `Ok(value)`) | `await SaveChangesAsync()` | commit |
| `Error(errors)` | o mesmo `Error(errors)` | rollback (o valor de retorno é a falha do work, não do commit) |
| uma exceção | relança a exceção | rollback e depois relança |
| `StartAsync` em si falha | propagado como `Error` | nenhum `work` é executado |

O overload genérico `InTransactionAsync<T>` adiciona uma sutileza: quando `SaveChangesAsync` falha depois de um `work()` bem-sucedido, ele retorna os **erros do save** (porque o valor nunca alcançou durabilidade).

---

## Exemplos reais

### 1. Persistir + publicar — *dentro* da transação

```csharp
public Task<AxisResult<CreateOrderResponse>> HandleAsync(CreateOrderCommand cmd)
    => uow.InTransactionAsync(() =>
        factory.CreateAsync(cmd)
            .ThenAsync(order => writer.CreateAsync(order))
            .ThenAsync(_     => outboxBus.PublishAsync(new OrderCreatedEvent(cmd.OrderId)))
            .MapAsync(_      => new CreateOrderResponse { OrderId = cmd.OrderId }));
```

**Por que compensa:** o `OutboxBusAdapter` grava numa tabela outbox dentro da mesma transação. Commit = tanto o pedido quanto o evento desembarcam atomicamente; rollback = nenhum persiste.

### 2. Ler, validar, depois escrever — curto-circuita na trilha de falha

```csharp
public Task<AxisResult> HandleAsync(UpdatePersonCommand cmd)
    => uow.InTransactionAsync(() =>
        reader.GetByIdAsync(cmd.PersonId)
            .ThenAsync(person => person.UpdateAsync(cmd))
            .ThenAsync(person => writer.UpdateAsync(person)));
```

**Por que compensa:** se `GetByIdAsync` retorna `NotFound`, o pipeline curto-circuita e `InTransactionAsync` faz rollback — a transação vazia simplesmente não commita nada.

### 3. Capturar uma exceção inesperada limpamente

```csharp
public Task<AxisResult> HandleAsync(BackfillProjectionCommand cmd)
    => uow.InTransactionAsync(() => projection.RebuildAsync(cmd));
```

Se `RebuildAsync` lançar (`OutOfMemoryException`, uma exceção Postgres que escapou do repository base, o que for), `InTransactionAsync` faz rollback e relança. A exceção ainda sobe pela stack — mas pelo menos o banco fica consistente.

---

## Veja também

- [`InTransactionAsync`](in-transaction.md) — o wrapper, em profundidade
- [Adapter Postgres](postgres-adapter.md) — uma implementação embarcada
- [Adapter MySQL](mysql-adapter.md) — a outra implementação embarcada
- [Repository base](repository-base.md) — `ExecuteAsync`/`GetAsync`/`ListAsync`

---

↩ [Voltar à documentação do AxisRepository](README.md)
