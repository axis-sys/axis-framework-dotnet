# Get-or-create · `GetOrCreateAsync`

> O padrão cache-aside, em uma única chamada. Se a chave existe, devolve. Se não, roda a factory; se a factory tem sucesso, armazena o valor e devolve. Se a factory **falha**, a falha flui adiante e **nada é cacheado**.

```csharp
Task<AxisResult<Person>> result = cache.GetOrCreateAsync(
    key:        $"person:{id}",
    factory:    () => repository.GetByIdAsync(id),       // Task<AxisResult<Person>>
    expiration: TimeSpan.FromMinutes(10));
```

---

## Quando usar

Uma leitura que é **cara de recomputar** (round-trip ao banco, chamada de API externa) e **segura de servir a partir de um snapshot** durante o TTL escolhido. A factory pode ser um pipeline `AxisResult` completo — incluindo validação, chamadas ao repositório e projeções.

## Quando *não* usar

| Você quer… | Use no lugar |
|---|---|
| forçar refresh, ignorando o que está cacheado | `RemoveAsync` seguido de `GetOrCreateAsync` |
| só cachear em sucesso **e** engolir falhas | embrulhe com `.RecoverNotFound(...)` do `AxisResult` |
| apenas gravar um valor (sem ler) | [`SetAsync`](iaxiscache.md) |
| apenas ler um valor (sem preencher) | [`GetAsync`](iaxiscache.md) |

---

## Tabela de comportamento

| Estado do cache | Desfecho da factory | O que você recebe de volta | O que acaba no cache |
|---|---|---|---|
| **hit** | (não chamada) | `Ok(cachedValue)` | inalterado |
| **miss** | `Ok(value)` | `Ok(value)` | `value`, com `expiration` se fornecida |
| **miss** | `Error(errors)` | `Error(errors)` | **nada** — a falha não é memoizada |
| **miss** | factory lança | `Error(InternalServerError(ex.Message))` | **nada** |
| **cancelado** | (não chamada) | `Error(InternalServerError("…"))` | **nada** |

Lendo `MemoryCacheAdapter.GetOrCreateAsync` direto: um hit retorna na hora; num miss, o resultado da factory é inspecionado — só `IsSuccess` dispara `memoryCache.Set(key, result.Value[, expiration])`. O cancelamento é checado no início do método.

---

## Exemplos reais

### 1. Lookup de pessoa com TTL

```csharp
public Task<AxisResult<Person>> GetByIdAsync(AxisEntityId personId)
    => cache.GetOrCreateAsync(
        key:        $"person:{personId}",
        factory:    () => readerPort.GetByIdAsync(personId),
        expiration: TimeSpan.FromMinutes(10));
```

**Por que compensa:** se o repositório retornar `NotFound`, essa falha **não** é memoizada — a próxima chamada bate de novo no repositório. Você não cacheia acidentalmente "este id não existe" e perde uma linha recém-inserida.

### 2. Snapshot de configuração (TTL mais longo)

```csharp
public Task<AxisResult<FeatureFlags>> GetFlagsAsync(string tenantKey)
    => cache.GetOrCreateAsync(
        key:        $"flags:{tenantKey}",
        factory:    () => featureFlagPort.GetForTenantAsync(tenantKey),
        expiration: TimeSpan.FromHours(1));
```

**Por que compensa:** feature flags mudam pouco; um TTL de uma hora corta a carga no serviço de feature flag sem comprometer frescor. A factory é o mesmo código que rodaria sem cache.

### 3. Cache sem TTL (vive até evict ou restart)

```csharp
public Task<AxisResult<IReadOnlyList<string>>> GetSupportedCurrenciesAsync()
    => cache.GetOrCreateAsync<IReadOnlyList<string>>(
        key:     "currencies:all",
        factory: () => currencyPort.LoadAllAsync());
        // sem argumento de expiration → sem expiração por tempo
```

**Por que compensa:** a lista de moedas suportadas é efetivamente imutável durante a vida do processo; a factory roda no máximo uma vez por start, e o cache guarda a resposta até o `IMemoryCache` evictar sob pressão.

---

## Veja também

- [O contrato `IAxisCache`](iaxiscache.md) — cada método
- [Adapter `AxisMemoryCache`](memory-adapter.md) — a implementação na caixa
- [Adapter custom](custom-adapter.md) — o que o seu `GetOrCreateAsync` deve garantir

---

↩ [Voltar à documentação do AxisCache](README.md)
