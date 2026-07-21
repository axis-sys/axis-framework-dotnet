# Adapter custom · escreva seu próprio `IAxisCache`

> Trocar o adapter in-memory por Redis, Memcached ou um híbrido (L1 in-memory + L2 distribuído) é a razão de ser da abstração. Implemente cinco métodos e registre sua classe como `IAxisCache`.

> **Já precisa que o estado do cache sobreviva a um restart ou seja compartilhado entre instâncias?** Confira primeiro o [adapter SQL](sql-adapter.md) já embutido (`AxisCache.Postgres` / `AxisCache.MySql`) — é uma implementação first-party de dois níveis (L1 memória + L2 SQL) exatamente disso, sem precisar escrever código custom. Recorra a um adapter custom quando você especificamente precisar de latência classe Redis/Memcached, invalidação via pub/sub, ou um backend que o adapter SQL não atinge.

```csharp
public class RedisCacheAdapter(IConnectionMultiplexer redis, IAxisMediatorAccessor accessor) : IAxisCache
{
    private readonly CancellationToken _ct =
        accessor.AxisMediator?.CancellationToken ?? CancellationToken.None;

    public Task<AxisResult<T?>> GetAsync<T>(string key)
        => AxisResult.TryAsync(async () =>
        {
            _ct.ThrowIfCancellationRequested();
            var db = redis.GetDatabase();
            var raw = await db.StringGetAsync(key);
            return raw.HasValue ? JsonSerializer.Deserialize<T>(raw!, JsonSerializerOptions.Web) : default;
        });

    // … SetAsync, GetOrCreateAsync, RemoveAsync, ExistsAsync
}
```

---

## Quando usar

- Produção com múltiplos processos (Redis, Memcached).
- Necessidade de invalidação via pub/sub.
- Um test double que grava cada chamada.
- Um cache híbrido L1+L2 (memoize chaves quentes localmente, caia para Redis).

## Quando *não* usar

| Você quer… | Use no lugar |
|---|---|
| rodar só em um processo | o [`AxisMemoryCache`](memory-adapter.md) pronto |
| sobreviver a restart ou compartilhar estado entre instâncias, em Postgres ou MySQL | o [adapter SQL](sql-adapter.md) pronto — sem precisar de código custom |
| usar uma feature do SDK do fornecedor que o contrato não expõe | chame o SDK diretamente dentro do adapter — mantenha `IAxisCache` honesto |
| adicionar tags / padrões / batch | estenda o contrato com uma nova interface e exija que o *adapter* implemente as duas |

---

## O contrato que você precisa honrar

| Comportamento | Obrigatório em | Razão |
|---|---|---|
| Todo método retorna um `AxisResult`, nunca lança | todos | os chamadores encadeiam com `Then`/`Map` e esperam falhas como valores |
| `GetAsync<T>` retorna `Ok(null)` em miss, **não** falha | `GetAsync` | um miss é normal e não pode curto-circuitar a ferrovia |
| `GetOrCreateAsync` **não** cacheia em falha da factory | `GetOrCreateAsync` | falhas não são memoizadas; a próxima chamada tenta de novo |
| `RemoveAsync` é sucesso mesmo se a chave estava ausente | `RemoveAsync` | remoção idempotente — chamadores não precisam checar antes |
| Cancelamento flui de `IAxisMediatorAccessor.AxisMediator?.CancellationToken` | todos | espelha o adapter da caixa e o resto do Axis |

---

## Registrando seu adapter

```csharp
public static class DependencyInjection
{
    public static IServiceCollection AddAxisRedisCache(this IServiceCollection services, string connectionString)
    {
        services.AddSingleton<IConnectionMultiplexer>(
            _ => ConnectionMultiplexer.Connect(connectionString));

        services.AddSingleton<IAxisCache, RedisCacheAdapter>();
        return services;
    }
}

// Program.cs
builder.Services.AddAxisRedisCache(builder.Configuration.GetConnectionString("Redis")!);
```

---

## Exemplo real — híbrido L1+L2

Um cache de dois níveis: lê primeiro do `IMemoryCache` local, cai para o Redis em miss, escreve em ambos no preenchimento.

```csharp
public class HybridCacheAdapter(IMemoryCache l1, IConnectionMultiplexer l2, IAxisMediatorAccessor accessor) : IAxisCache
{
    private readonly CancellationToken _ct =
        accessor.AxisMediator?.CancellationToken ?? CancellationToken.None;

    public async Task<AxisResult<T?>> GetAsync<T>(string key)
    {
        _ct.ThrowIfCancellationRequested();

        if (l1.TryGetValue(key, out T? local))
            return AxisResult.Ok<T?>(local);

        var db = l2.GetDatabase();
        var raw = await db.StringGetAsync(key);
        if (!raw.HasValue) return AxisResult.Ok<T?>(default);

        var value = JsonSerializer.Deserialize<T>(raw!, JsonSerializerOptions.Web);
        l1.Set(key, value, TimeSpan.FromMinutes(1));   // esquenta o L1
        return AxisResult.Ok<T?>(value);
    }

    // … restante de IAxisCache
}
```

**Por que compensa:** o código da aplicação fica idêntico — `cache.GetOrCreateAsync(...)` — enquanto operacionalmente o caminho quente é memória in-process e só misses frios atravessam a rede. Voltar a usar Redis puro é uma linha de registro.

---

## Veja também

- [O contrato `IAxisCache`](iaxiscache.md) — a superfície completa
- [Padrão get-or-create](get-or-create.md) — o operador que seu adapter deve implementar com cuidado
- [Adapter `AxisMemoryCache`](memory-adapter.md) — a referência da caixa para single-process
- [Adapter SQL](sql-adapter.md) — a referência da caixa para persistente/compartilhado, antes de escrever o seu

---

↩ [Voltar à documentação do AxisCache](README.md)
