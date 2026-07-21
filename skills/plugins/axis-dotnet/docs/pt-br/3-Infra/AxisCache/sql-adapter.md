# Adapter SQL · `AxisCache.Postgres` / `AxisCache.MySql`

> O `IAxisCache` de dois níveis, com backend SQL, que já vem na caixa: um L1 rápido e in-process (`IMemoryCache`) na frente de um L2 durável e compartilhado (uma tabela SQL simples). Mesmo tipo de settings, mesmo formato de DI, mesmo comportamento em runtime nos dois bancos — os dois pacotes diferem só no nome do método de DI e na biblioteca de conexão por baixo.

```csharp
services.AddAxisCachePostgres(new AxisCacheRepositorySettings
{
    ConnectionString = "Host=…",
    L1Ttl            = TimeSpan.FromSeconds(60),
});
```

---

## Quando usar

O estado do cache precisa **sobreviver a um restart** ou ser **compartilhado entre instâncias** — as duas coisas que o adapter in-memory não consegue fazer. Casos típicos: uma API com múltiplas instâncias atrás de um load balancer, onde toda réplica precisa ver o mesmo valor cacheado; um worker que reinicia com frequência e não deveria fazer cold-start em cada lookup; um cache que você quer inspecionar com SQL puro. Ele reaproveita o banco que a aplicação já provisiona — nenhuma peça nova para operar.

Fique com o [`AxisMemoryCache`](memory-adapter.md) para um app single-process, testes, ou desenvolvimento local, onde um round-trip de rede/banco por miss de cache não traz benefício nenhum.

## Quando *não* usar

| Você quer… | Use no lugar |
|---|---|
| rodar em um único processo sem necessidade de sobreviver a restart | o [`AxisMemoryCache`](memory-adapter.md) pronto |
| um cache distribuído em velocidade de memória, sub-milissegundo (Redis, Memcached) | um [adapter custom](custom-adapter.md) — round-trips SQL são mais lentos que um store in-memory |
| invalidação via pub/sub entre instâncias | um [adapter custom](custom-adapter.md) sobre um backend com pub/sub nativo |
| atingir um banco diferente de Postgres ou MySQL | um [adapter custom](custom-adapter.md) implementando `IAxisCacheConnectionFactory` / `IAxisCacheSqlDialect` / `IAxisCacheStorageInitializer` sobre o core compartilhado `AxisCache.Repository` |

---

## O modelo de dois níveis

Toda leitura e escrita passa por duas camadas:

- **L1** — um `IMemoryCache` in-process, singleton, compartilhado por toda requisição na instância. Rápido, mas privado ao processo e perdido no restart.
- **L2** — o store SQL (`ICacheEntryStore`, tabela `AXIS_CACHE.CACHE_ENTRIES`), a fonte de verdade durável compartilhada por toda instância apontando para a mesma connection string.

Escritas vão primeiro para o L2 com a falha **propagada** — se a escrita no banco falhar, `SetAsync` falha, ponto final — e só depois esquentam o L1 em melhor esforço. Leituras servem um hit de L1 imediatamente; num miss de L1, caem para o L2 e reidratam o L1 por no máximo `L1Ttl` (limitado ainda pela expiração do próprio valor, então o L1 nunca sobrevive além da entrada autoritativa do L2). Sobreviver a um restart e ser visível a toda instância vêm do L2; a janela de `L1Ttl` é a *única* obsolescência que um leitor pode observar entre instâncias.

Configure **`L1Ttl = TimeSpan.Zero`** para pular o L1 completamente — toda leitura vai direto ao L2. Use isso quando precisar de consistência estrita entre instâncias (sem nenhuma janela de obsolescência) e puder aceitar o round-trip extra em cada leitura; é também como os próprios testes de integração do adapter exercitam o L2 isoladamente.

---

## `AxisCacheRepositorySettings`

```csharp
public sealed class AxisCacheRepositorySettings
{
    public required string ConnectionString { get; init; }
    public TimeSpan L1Ttl { get; init; } = TimeSpan.FromSeconds(60);
    public bool RunStartupMigration { get; init; } = true;
    public bool SweepEnabled { get; init; } = true;
    public TimeSpan SweepInterval { get; init; } = TimeSpan.FromMinutes(5);
}
```

| Propriedade | Padrão | Significado |
|---|---|---|
| `ConnectionString` | *(obrigatória)* | Aponta para o store SQL do L2 — normalmente o mesmo banco que a aplicação já provisiona para os próprios dados. |
| `L1Ttl` | `TimeSpan.FromSeconds(60)` | Por quanto tempo o L1 confia num valor antes de cair para o L2. `TimeSpan.Zero` desativa o L1 (toda leitura vai ao L2). |
| `RunStartupMigration` | `true` | Quando true, um hosted service cria o schema do L2 no startup (idempotente). Desligue em hosts que não podem tocar no banco no boot — por exemplo, um test host que faqueia as portas de storage. |
| `SweepEnabled` | `true` | Quando true, um hosted service apaga periodicamente as linhas expiradas do L2 (`DeleteExpiredAsync`), para que a reclamação não dependa de a chave ser lida de novo. Desligue em hosts que não podem rodar trabalho de banco em background, ou que delegam a varredura a um processo dedicado — a expiração continua sendo honrada passivamente em toda leitura. |
| `SweepInterval` | `TimeSpan.FromMinutes(5)` | Com que frequência o sweep worker apaga linhas expiradas do L2 enquanto `SweepEnabled` está ligado. |

---

## O que é registrado

`AddAxisCachePostgres` e `AddAxisCacheMySql` fornecem as peças específicas do dialeto e depois delegam para o `AddAxisCacheRepositoryCore` compartilhado:

| Serviço | Lifetime | Fornecido por |
|---|---|---|
| `AxisCacheRepositorySettings` | singleton | core |
| `IMemoryCache` | singleton | core (`AddMemoryCache()`) — o nível L1 |
| `TimeProvider` | singleton | core (`TimeProvider.System`, via `TryAddSingleton`) |
| `ICacheEntryStore` | scoped | core (`CacheEntryStore`) — o nível L2 |
| `IAxisCache` | scoped | core (`RepositoryCacheAdapter`) — o próprio adapter de dois níveis |
| `IAxisCacheConnectionFactory` | singleton | o adapter de storage (`PostgresCacheConnectionFactory` / `MySqlCacheConnectionFactory`) — abre conexões de L2 a partir de um data source com pool |
| `IAxisCacheSqlDialect` | singleton | o adapter de storage (`PostgresCacheSqlDialect` / `MySqlCacheSqlDialect`) — a única instrução SQL que realmente diverge entre os bancos (o upsert) |
| `IAxisCacheStorageInitializer` | singleton | o adapter de storage (`PostgresCacheStorageInitializer` / `MySqlCacheStorageInitializer`) — cria o schema do L2 |
| `AxisCacheStorageInitializerWorker` | hosted service | core, só quando `RunStartupMigration` é `true` — roda o initializer uma vez no startup |
| `AxisCacheSweepWorker` | hosted service | core, só quando `SweepEnabled` é `true` (o padrão) — chama `DeleteExpiredAsync` periodicamente a cada `SweepInterval` |

`IAxisCache` e `ICacheEntryStore` são scoped (não singleton) porque o store depende de `IAxisLogger<T>`, que por sua vez depende do `IAxisMediator` ambiente e scoped. O L1 em si continua sendo o `IMemoryCache` singleton compartilhado, então o cache continua valendo entre requisições — só a fachada do adapter é resolvida por escopo.

---

## `AddAxisCachePostgres`

```csharp
using AxisCache.Postgres;

builder.Services.AddAxisCachePostgres(new AxisCacheRepositorySettings
{
    ConnectionString = builder.Configuration.GetConnectionString("Postgres")!,
    L1Ttl            = TimeSpan.FromSeconds(60),
});
```

Registra um `NpgsqlDataSource` com pool atrás de `IAxisCacheConnectionFactory`, `PostgresCacheSqlDialect`, `PostgresCacheStorageInitializer`, e então chama `AddAxisCacheRepositoryCore(settings)`.

## `AddAxisCacheMySql`

```csharp
using AxisCache.MySql;

builder.Services.AddAxisCacheMySql(new AxisCacheRepositorySettings
{
    ConnectionString = builder.Configuration.GetConnectionString("MySql")!,
    L1Ttl            = TimeSpan.FromSeconds(60),
});
```

Registra um `MySqlDataSource` com pool atrás de `IAxisCacheConnectionFactory`, `MySqlCacheSqlDialect`, `MySqlCacheStorageInitializer`, e então chama `AddAxisCacheRepositoryCore(settings)` — a mesma chamada ao core que `AddAxisCachePostgres` faz.

---

## Um único storage por processo

O `AxisCache` suporta um único backend de storage por processo, por design. Tanto `AddAxisCachePostgres` quanto `AddAxisCacheMySql` checam se `AxisCacheRepositorySettings` já está registrado e **lançam `InvalidOperationException`** se estiver — seja a chamada anterior para o mesmo método ou para o outro. Chame exatamente um deles, exatamente uma vez, no startup da aplicação. Isso espelha a mesma guarda nos adapters de storage do `AxisSaga`.

---

## Bootstrap do schema

A tabela do L2 vive no schema `AXIS_CACHE` e é declarada uma única vez, de forma agnóstica a dialeto, no core compartilhado; cada adapter a renderiza com seu próprio dialeto SQL. Duas formas de aplicá-la:

- **Automaticamente** — quando `RunStartupMigration` é `true` (o padrão), o `AxisCacheStorageInitializerWorker` (um `BackgroundService`) roda `IAxisCacheStorageInitializer.InitializeAsync()` uma vez depois que o host inicia. Idempotente — seguro em todo restart.
- **Explicitamente** — chame `Persistence.AxisCacheMigrations.InitializePostgresAsync(connectionString)` ou `Persistence.AxisCacheMigrations.InitializeMySqlAsync(connectionString)` diretamente (de `AxisCache.Postgres.Persistence` / `AxisCache.MySql.Persistence`). É assim que fixtures de teste provisionam o schema contra uma instância Testcontainers antes que o worker registrado via DI o faria de qualquer forma, e é como você migraria antes de fazer deploy num host com `RunStartupMigration = false`.

---

## Exemplo real — fiação de produção

```csharp
// Program.cs
builder.Services
    .AddAxisMediator()
    .AddAxisLogger()
    .AddAxisCachePostgres(new AxisCacheRepositorySettings
    {
        ConnectionString    = builder.Configuration.GetConnectionString("Postgres")!,
        L1Ttl               = TimeSpan.FromSeconds(30),
        RunStartupMigration = true,
    });

// Um query handler — idêntico ao que seria escrito contra o AxisMemoryCache
public class GetPersonHandler(IAxisCache cache, IPersonReaderPort reader)
{
    public Task<AxisResult<Person>> HandleAsync(GetPersonQuery q)
        => cache.GetOrCreateAsync(
            key:        $"person:{q.PersonId}",
            factory:    () => reader.GetByIdAsync(q.PersonId),
            expiration: TimeSpan.FromMinutes(10));
}
```

**Por que compensa:** trocar `AddAxisMemoryCache()` por `AddAxisCachePostgres(settings)` (ou `AddAxisCacheMySql(settings)`) é a única mudança na raiz de composição. Todo handler que chama `IAxisCache` — leituras cache-aside, invalidação via `RemoveAsync` — continua funcionando sem alteração, mas agora os valores cacheados sobrevivem a um restart e são compartilhados por toda instância apontando para o mesmo banco.

---

## Veja também

- [Adapter `AxisMemoryCache`](memory-adapter.md) — a alternativa single-process, in-memory, que este adapter estende com um L2 durável e compartilhado
- [O contrato `IAxisCache`](iaxiscache.md) — a interface que os dois adapters implementam
- [Padrão get-or-create](get-or-create.md) — o operador-destaque, inalterado por qual adapter está registrado

---

↩ [Voltar à documentação do AxisCache](README.md)
