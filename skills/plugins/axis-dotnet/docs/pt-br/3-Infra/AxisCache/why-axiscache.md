# Por que AxisCache? · comparação

> Há outras maneiras de cachear em .NET. Esta página diz por que o AxisCache é diferente — uma comparação direta, sem mão na cintura.

---

## vs. `IMemoryCache` (direto)

`IMemoryCache` é o carro-chefe e o AxisCache o usa por baixo. Chamá-lo **diretamente** do código de aplicação tem três problemas:

1. Ele lança — cada site precisa de `try/catch` ou aceita crashes.
2. Não tem um `GetOrCreate` que retorne falha tipada — o overload `IMemoryCache.GetOrCreate` cacheia alegremente o que a factory devolver, inclusive null ou um valor parcial.
3. Amarra seus handlers ao `Microsoft.Extensions.Caching.Memory`. Troque para Redis depois e cada call site muda.

**AxisCache** retorna `AxisResult`, deixa a factory ciente da ferrovia e permite trocar adapters sem mexer no código da aplicação.

## vs. `IDistributedCache`

Mesmos trade-offs de acima, mais uma API mais rasa — `IDistributedCache` só lida com `byte[]`, então cada chamador precisa serializar/desserializar à mão. Serialização do lado do adapter é o lugar certo para essa preocupação.

## vs. `FusionCache` / `EasyCaching`

Ambas excelentes e ricas em features (multi-level, jitter, fail-safe, proteção contra stampede). Também são **maiores** e **mais opinativas** sobre como o cache deve funcionar. Se você precisa das features avançadas delas, use. Se você quer uma interface **pequena, focada e em formato Axis** que retorne `AxisResult` e que viva ao lado de `AxisResult`/`AxisLogger`/`AxisMediator`, use `AxisCache`. (Nada te impede de implementar `IAxisCache` em cima do `FusionCache` — é exatamente para isso que existem adapters custom.)

## vs. um `ICacheService<T>` caseiro

A abstração DIY. Mesma ideia do `IAxisCache`, mas você escreve o contrato, o adapter in-memory e os testes. `IAxisCache` poupa o custo — e padroniza os modos de falha em todas as packages do Axis.

---

## A comparação

| Característica | AxisCache | `IMemoryCache` direto | `IDistributedCache` direto | FusionCache | Caseiro |
|---|:--:|:--:|:--:|:--:|:--:|
| Retorna `AxisResult` | **Sim** | Não | Não | Não | Talvez |
| `GetOrCreate` não cacheia falhas | **Sim** | Não | n/a | Sim | Talvez |
| Cancelamento implícito via `AxisMediator` | **Sim** | Não | Não | Não | Talvez |
| Troca memória ↔ distribuído sem mudar a aplicação | **Sim** | Não | Não | Sim | Talvez |
| Superfície minúscula, sem curva de aprendizado | **Sim** | Sim | Sim | Não | Sim |
| Adapter in-memory embarcado | **Sim** | n/a | n/a | Sim | Não |
| Adapter persistente/compartilhado embarcado (backend SQL) | **Sim** | Não | Não | Não | Não |
| Zero deps NuGet além de `Microsoft.Extensions.Caching.*` (adapter de memória) | **Sim** | Sim | Sim | Não | Sim |

---

## Veja também

- [O contrato `IAxisCache`](iaxiscache.md) — os cinco métodos
- [Padrão get-or-create](get-or-create.md) — o operador-destaque
- [Adapter SQL](sql-adapter.md) — o adapter persistente/compartilhado embutido
- [Referência da API](api-reference.md) — cada método num só lugar

---

↩ [Voltar à documentação do AxisCache](README.md)
