# AxisCache — Documentação

> 🌐 [English (README principal)](../../../en-us/3-Infra/AxisCache/README.md)

**Uma pequena abstração `IAxisCache`** — cinco operações async (`Get`, `Set`, `GetOrCreate`, `Remove`, `Exists`), todas retornando `AxisResult`, com cancelamento fluindo pelo `AxisMediator`. Duas famílias de adapters first-party já vêm na caixa: um adapter in-memory pronto (`AxisMemoryCache`) para apps single-process, e um adapter SQL de dois níveis (`AxisCache.Postgres` / `AxisCache.MySql`) para quando o estado do cache precisa sobreviver a um restart ou ser compartilhado entre instâncias. Coloque o seu para Redis, Memcached ou qualquer outro.

```csharp
public Task<AxisResult<Person>> GetByIdAsync(AxisEntityId personId)
    => cache.GetOrCreateAsync(
        key:        $"person:{personId}",
        factory:    () => repository.GetByIdAsync(personId),
        expiration: TimeSpan.FromMinutes(10));
```

Use esta página como **mapa**: leia o tronco abaixo (~5 min) e salte direto para o detalhe do grupo que você precisa — sem ler centenas de linhas.

---

## O tronco (leia primeiro)

### A interface em 60 segundos

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

Cinco métodos. Todo resultado é um `AxisResult`. O cancelamento é **implícito** — o adapter puxa o `CancellationToken` do escopo do `AxisMediator` atual. Seu código de aplicação nunca passa um token nas chamadas de cache. → **[O contrato `IAxisCache`](iaxiscache.md)**

### `GetOrCreateAsync` — o operador mais importante

O padrão cache-aside, numa só chamada. Se a chave existe, devolve. Se não, roda a factory, armazena o `AxisResult<T>` dela (só em sucesso) e devolve o valor. Se a factory **falhar**, a falha flui adiante e nada é cacheado. → **[Padrão get-or-create](get-or-create.md)**

### Adapter in-memory

`AxisMemoryCache` registra `IAxisCache` em cima de `Microsoft.Extensions.Caching.Memory`:

```csharp
services.AddAxisMemoryCache();   // liga IMemoryCache + IAxisCache → MemoryCacheAdapter
```

→ **[Adapter `AxisMemoryCache`](memory-adapter.md)**

### Adapter com backend SQL

Precisa que o cache sobreviva a um restart ou seja compartilhado entre instâncias? `AxisCache.Postgres` / `AxisCache.MySql` registram o mesmo `IAxisCache`, com um L1 rápido in-process na frente de uma tabela SQL L2 durável e compartilhada:

```csharp
services.AddAxisCachePostgres(new AxisCacheRepositorySettings { ConnectionString = "Host=…" });
```

→ **[Adapter SQL](sql-adapter.md)**

### Instalação

```
dotnet add package AxisCache             # a abstração (depende de AxisResult)
dotnet add package AxisMemoryCache       # o adapter in-memory
dotnet add package AxisCache.Postgres    # ou AxisCache.MySql — o adapter com backend SQL
```

→ Guia completo: **[Primeiros passos](getting-started.md)**

---

## O mapa (salte para o que precisa)

| Grupo | Você quer… | Detalhe |
|---|---|---|
| **Contrato · `IAxisCache`** | as cinco operações e suas semânticas | [iaxiscache.md](iaxiscache.md) |
| **Get-or-create · `GetOrCreateAsync`** ⭐ | padrão cache-aside com factory falível | [get-or-create.md](get-or-create.md) |
| **In-memory · `AxisMemoryCache`** | o adapter `IMemoryCache` pronto | [memory-adapter.md](memory-adapter.md) |
| **SQL · `AxisCache.Postgres` / `AxisCache.MySql`** | o adapter de dois níveis pronto que sobrevive a restart e é compartilhado entre instâncias | [sql-adapter.md](sql-adapter.md) |
| **Adapter custom** | escreva o seu (Redis, Memcached, híbrido) | [custom-adapter.md](custom-adapter.md) |
| **Por quê?** | o argumento contra usar `IDistributedCache` direto | [why-axiscache.md](why-axiscache.md) |
| **Referência** | cada método num só lugar | [api-reference.md](api-reference.md) |

**Comece aqui:** [Primeiros passos](getting-started.md) · [O contrato `IAxisCache`](iaxiscache.md) · [Por que AxisCache?](why-axiscache.md)

**Fundamentos:** [Padrão get-or-create](get-or-create.md) · [Adapter `AxisMemoryCache`](memory-adapter.md) · [Adapter SQL](sql-adapter.md)

**Referência e extras:** [Adapter custom](custom-adapter.md) · [Referência da API](api-reference.md)

---

## Princípios de design

1. **Cinco métodos, nada mais.** Uma superfície maior convida microtimizações que vazam o modelo do fornecedor. Mantenha os chamadores honestos.
2. **Todo resultado é um `AxisResult`.** Falhas de cache são fatos, não exceções. O pipeline decide o que fazer com elas.
3. **Cancelamento é implícito.** O adapter puxa o token do `IAxisMediatorAccessor`, então nenhuma assinatura precisa carregá-lo.
4. **O adapter é substituível.** `services.AddAxisMemoryCache()` é uma linha; troque por `AddAxisCachePostgres()`/`AddAxisCacheMySql()` — ou por um `AddAxisRedisCache()` custom — e nada muda na aplicação.
5. **`GetOrCreateAsync` é o destaque.** A factory tem permissão de falhar — essa falha **não** é cacheada.

---

## Licença

Apache 2.0
