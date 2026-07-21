# Contrato · `IAxisCache`

> A interface única que todo adapter implementa. Cinco métodos, todos async, todos retornando `AxisResult`. A semântica é simples de propósito — não tem `Refresh`, não tem batch, não tem tags.

```csharp
public interface IAxisCache
{
    Task<AxisResult<T?>>   GetAsync<T>(string key);
    Task<AxisResult>       SetAsync<T>(string key, T value, TimeSpan? expiration = null);
    Task<AxisResult<T>>    GetOrCreateAsync<T>(string key, Func<Task<AxisResult<T>>> factory, TimeSpan? expiration = null);
    Task<AxisResult>       RemoveAsync(string key);
    Task<AxisResult<bool>> ExistsAsync(string key);
}
```

---

## Quando usar

Em qualquer lugar que sua aplicação leia um valor **caro de computar** e **aceitável devolver de uma cópia memoizada**. Perfis de pessoa, configuração, lookups, agregados que mudam raramente. Chaves de cache são strings simples — escolha uma convenção (`"<entidade>:<id>"`) e mantenha.

## Quando *não* usar

| Você quer… | Use no lugar |
|---|---|
| compartilhar **estado de sessão** entre nós web | um session store dedicado |
| armazenar valores que **precisam sobreviver a crash**, ou compartilhar entre instâncias | o [adapter SQL](sql-adapter.md) embutido (Postgres/MySQL) — ou um banco de verdade |
| coordenar locks entre processos | uma primitiva de lock distribuído (`SET NX EX` do Redis etc.) |
| invalidar por **tag** ou **padrão** | estenda a interface no seu adapter, ou use o SDK do fornecedor por baixo |

---

## As cinco operações

| Método | Sucesso significa | Retorna `IsFailure` quando |
|---|---|---|
| `GetAsync<T>(key)` | a chamada completou; `Value` pode ser `null` (miss) | o adapter lançou |
| `SetAsync<T>(key, value, expiration?)` | o valor foi armazenado (sobrescrevendo qualquer entrada existente) | o adapter lançou |
| `GetOrCreateAsync<T>(key, factory, expiration?)` | o valor veio do cache **ou** a factory rodou e armazenou seu `Value` | o adapter lançou, a operação foi cancelada, **ou a própria factory retornou falha** (seu `Value` nunca é cacheado) |
| `RemoveAsync(key)` | a chave não está mais no cache (remover uma chave ausente também é sucesso) | o adapter lançou |
| `ExistsAsync(key)` | a chamada completou; `Value` diz se a chave estava lá | o adapter lançou |

Cancelamento implícito: todo método do `MemoryCacheAdapter` embarcado chama `_cancellationToken.ThrowIfCancellationRequested()` antes de tocar o store (o token vem de `mediatorAccessor.AxisMediator?.CancellationToken`). Os quatro métodos embrulhados em `TryAsync` — `GetAsync`, `SetAsync`, `RemoveAsync`, `ExistsAsync` — **relançam** o `OperationCanceledException`, porque o `AxisResult.TryAsync` trata cancelamento como crítico em vez de convertê-lo em `AxisResult` falho; uma chamada cancelada ali *lança*. Só o `GetOrCreateAsync`, que embrulha o corpo no próprio `try/catch (Exception)`, o captura e devolve uma falha.

---

## Exemplos reais

### 1. Cache-aside num query handler

```csharp
public Task<AxisResult<GetPersonResponse>> HandleAsync(GetPersonQuery query)
    => cache.GetOrCreateAsync(
            key:        $"person:{query.PersonId}",
            factory:    () => readerPort.GetByIdAsync(query.PersonId),
            expiration: TimeSpan.FromMinutes(10))
        .MapAsync(p => new GetPersonResponse { PersonId = p.PersonId, DisplayName = p.DisplayName });
```

**Por que compensa:** uma única linha decide se a resposta vem do cache ou do repositório, e a trilha de falha cobre os dois caminhos — `NotFound` do repositório vira um cache miss que *não* é cacheado.

### 2. Invalidação write-through num command handler

```csharp
public Task<AxisResult<UpdatePersonResponse>> HandleAsync(UpdatePersonCommand cmd)
    => factory.GetByIdAsync(cmd.PersonId)
        .ThenAsync(person => person.UpdateDisplayNameAsync(cmd.DisplayName))
        .ThenAsync(_ => unitOfWork.SaveChangesAsync())
        .ThenAsync(_ => cache.RemoveAsync($"person:{cmd.PersonId}"))   // invalida
        .MapAsync(_ => new UpdatePersonResponse { PersonId = cmd.PersonId });
```

**Por que compensa:** o cache é invalidado **somente depois** que a escrita commitou. Se `SaveChangesAsync` falhar, a ferrovia curto-circuita e o cache fica quente com o valor antigo (que ainda está correto).

### 3. Checagem de existência antes de uma operação cara

```csharp
public async Task<AxisResult> RebuildIfStaleAsync(AxisEntityId id)
{
    var exists = await cache.ExistsAsync($"projection:{id}");
    if (exists.IsSuccess && exists.Value)
        return AxisResult.Ok();

    return await rebuilder.RebuildAsync(id)
        .ThenAsync(value => cache.SetAsync($"projection:{id}", value, TimeSpan.FromHours(1)));
}
```

**Por que compensa:** a checagem é barata e explícita. O `RebuildAsync` caro só é pago quando a projeção ainda não está quente.

---

## Veja também

- [Padrão get-or-create](get-or-create.md) — o operador-destaque em profundidade
- [Adapter `AxisMemoryCache`](memory-adapter.md) — a implementação na caixa para single-process
- [Adapter SQL](sql-adapter.md) — a implementação na caixa que sobrevive a restart e é compartilhada entre instâncias
- [Adapter custom](custom-adapter.md) — implemente `IAxisCache` para seu storage
- [Referência da API](api-reference.md) — cada método, num só lugar

---

↩ [Voltar à documentação do AxisCache](README.md)
