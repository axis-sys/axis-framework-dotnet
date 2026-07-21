# Referência da API

> O catálogo completo, agrupado por responsabilidade. Use para consulta — cada grupo linka de volta à sua página de detalhe.

---

## O contrato — `IAxisCache`

| Método | Assinatura | Descrição |
|---|---|---|
| `GetAsync<T>` | `Task<AxisResult<T?>> GetAsync<T>(string key)` | leitura; sucesso com `null` é miss |
| `SetAsync<T>` | `Task<AxisResult> SetAsync<T>(string key, T value, TimeSpan? expiration = null)` | escrita, sobrescrevendo qualquer entrada existente |
| `GetOrCreateAsync<T>` | `Task<AxisResult<T>> GetOrCreateAsync<T>(string key, Func<Task<AxisResult<T>>> factory, TimeSpan? expiration = null)` | cache-aside; nunca cacheia falhas |
| `RemoveAsync` | `Task<AxisResult> RemoveAsync(string key)` | remoção idempotente |
| `ExistsAsync` | `Task<AxisResult<bool>> ExistsAsync(string key)` | checagem não-lançante |

→ [O contrato `IAxisCache`](iaxiscache.md) · [Padrão get-or-create](get-or-create.md)

---

## Adapter in-memory — `AxisMemoryCache`

| Membro | Descrição |
|---|---|
| `MemoryCacheAdapter(IMemoryCache, IAxisMediatorAccessor)` | construtor; resolve o `CancellationToken` ambiente |
| `services.AddAxisMemoryCache()` | extensão DI; registra `IMemoryCache` + `IAxisCache → MemoryCacheAdapter` (singleton) |

→ [Adapter `AxisMemoryCache`](memory-adapter.md)

---

## Adapter com backend SQL — `AxisCache.Postgres` / `AxisCache.MySql`

| Membro | Descrição |
|---|---|
| `AxisCacheRepositorySettings` | `ConnectionString` (obrigatória), `L1Ttl` (padrão 60s; `TimeSpan.Zero` pula o L1), `RunStartupMigration` (padrão `true`), `SweepEnabled` (padrão `true`), `SweepInterval` (padrão 5min) |
| `services.AddAxisCachePostgres(settings)` | extensão DI; registra o data source/dialeto do Postgres + o core compartilhado de dois níveis (`IAxisCache → RepositoryCacheAdapter`) |
| `services.AddAxisCacheMySql(settings)` | extensão DI; registra o data source/dialeto do MySQL + o mesmo core compartilhado de dois níveis |

→ [Adapter SQL](sql-adapter.md)

---

## Contrato de comportamento (para adapters)

| Operação | Estado do cache | Desfecho da factory | `AxisResult` retornado | Estado do cache depois |
|---|---|---|---|---|
| `GetAsync<T>` | hit | n/a | `Ok(value)` | inalterado |
| `GetAsync<T>` | miss | n/a | `Ok(null)` | inalterado |
| `SetAsync<T>` | qualquer | n/a | `Ok()` | sobrescrito |
| `GetOrCreateAsync<T>` | hit | n/a | `Ok(value)` | inalterado |
| `GetOrCreateAsync<T>` | miss | `Ok(value)` | `Ok(value)` | armazenado |
| `GetOrCreateAsync<T>` | miss | `Error(errors)` | `Error(errors)` | inalterado |
| `RemoveAsync` | qualquer | n/a | `Ok()` | removido |
| `ExistsAsync` | hit | n/a | `Ok(true)` | inalterado |
| `ExistsAsync` | miss | n/a | `Ok(false)` | inalterado |
| qualquer | n/a | adapter lançou | `Error(InternalServerError(...))` | inalterado |
| qualquer | n/a | cancelado | `Error(...)` | inalterado |

→ [Adapter custom](custom-adapter.md)

---

## Veja também

- [Primeiros passos](getting-started.md) — instale, registre, cacheie seu primeiro valor
- [Adapter SQL](sql-adapter.md) — o adapter persistente/compartilhado embutido em profundidade
- [Por que AxisCache?](why-axiscache.md) — o argumento pela abstração
- [Documentação completa](README.md) — o mapa de toda a documentação

---

↩ [Voltar à documentação do AxisCache](README.md)
