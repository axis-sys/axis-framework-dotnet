# Primeiros passos · instalação e uso

> Instale a abstração e um adapter, registre na DI, e cacheie seu primeiro valor em menos de cinco minutos.

---

## Instalação

```
dotnet add package AxisCache             # a abstração
dotnet add package AxisMemoryCache       # adapter in-memory (Microsoft.Extensions.Caching.Memory)
```

`AxisCache` depende de `AxisResult` (pelo tipo de retorno). `AxisMemoryCache` adiciona `Microsoft.Extensions.Caching.Memory`. Ambos são pequenos e enxutos em dependências.

Precisa que o cache sobreviva a um restart ou seja compartilhado entre instâncias? Instale o adapter SQL de dois níveis já embutido, no lugar de (ou junto com) `AxisMemoryCache`:

```
dotnet add package AxisCache.Postgres    # adapter com backend SQL sobre PostgreSQL
dotnet add package AxisCache.MySql       # adapter com backend SQL sobre MySQL
```

Veja [Adapter SQL](sql-adapter.md) para o formato dos settings e a fiação de DI.

---

## Registrando o adapter

```csharp
using AxisMemoryCache;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAxisMemoryCache();   // registra IMemoryCache + IAxisCache como singleton
```

> O adapter resolve o `CancellationToken` ambiente a partir do `IAxisMediatorAccessor`. Garanta que `AxisMediator` está plugado — caso contrário, o cancelamento cai para `CancellationToken.None`.

---

## Lendo e escrevendo

```csharp
public class PersonService(IAxisCache cache, IPersonReaderPort repo)
{
    public Task<AxisResult<Person?>> GetCachedAsync(AxisEntityId id)
        => cache.GetAsync<Person>($"person:{id}");

    public Task<AxisResult> CacheAsync(Person person)
        => cache.SetAsync($"person:{person.PersonId}", person, TimeSpan.FromMinutes(10));

    public Task<AxisResult> InvalidateAsync(AxisEntityId id)
        => cache.RemoveAsync($"person:{id}");
}
```

> `GetAsync<T>` retorna `AxisResult<T?>` — sucesso com `null` é um *miss*, não uma falha. Falhas só aparecem quando o próprio adapter estourar (ou a operação for cancelada).

---

## O destaque: `GetOrCreateAsync`

```csharp
public Task<AxisResult<Person>> GetByIdAsync(AxisEntityId id)
    => cache.GetOrCreateAsync(
        key:        $"person:{id}",
        factory:    () => repository.GetByIdAsync(id),      // Task<AxisResult<Person>>
        expiration: TimeSpan.FromMinutes(10));
```

**Por que compensa:** o padrão cache-aside vira uma única chamada, a factory pode falhar (e *não* é cacheada em falha), e o caminho de cache miss fica fora do call site. Para adicionar ou remover cache, basta inverter uma linha — o restante do pipeline não muda.

---

## Veja também

- [O contrato `IAxisCache`](iaxiscache.md) — cada método, sua semântica e modos de falha
- [Padrão get-or-create](get-or-create.md) — o operador cache-aside em profundidade
- [Adapter `AxisMemoryCache`](memory-adapter.md) — o que `AddAxisMemoryCache()` registra
- [Adapter SQL](sql-adapter.md) — o que `AddAxisCachePostgres()` / `AddAxisCacheMySql()` registram
- [Adapter custom](custom-adapter.md) — implemente `IAxisCache` para Redis ou seu storage de escolha
- [Por que AxisCache?](why-axiscache.md) — o argumento contra usar `IDistributedCache` direto
- [Referência da API](api-reference.md) — cada método num só lugar

---

↩ [Voltar à documentação do AxisCache](README.md)
