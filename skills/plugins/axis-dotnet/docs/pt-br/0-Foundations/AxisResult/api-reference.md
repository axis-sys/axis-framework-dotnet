# Referência da API

> O catálogo completo de operadores, agrupado por responsabilidade. Use como consulta — cada grupo tem uma página de detalhe com exemplos.

---

## Criar resultados

| Método | Descrição |
|--------|-------------|
| `AxisResult.Ok()` | sucesso sem valor |
| `AxisResult.Ok(value)` | sucesso com valor |
| `AxisResult.Error(error)` | falha a partir de um único erro |
| `AxisResult.Error(errors)` | falha a partir de múltiplos erros |
| `AxisResult.Try(action)` | embrulha uma ação que pode lançar |
| `AxisResult.Try(func)` | embrulha uma função que pode lançar |
| `AxisResult.TryAsync(action)` | versão async de `Try` |
| `AxisResult.TryAsync(func)` | versão async de `Try` com valor de retorno |
| `AxisResult.TryBind(func)` | como `Try`, mas a func retorna um `AxisResult` — capturado e **achatado** (não aninhado) |
| `AxisResult.TryBindAsync(func)` | versão async de `TryBind` |
| Implícito: `value` | atribui qualquer valor onde se espera `AxisResult<T>` |
| Implícito: `AxisError` | atribui um erro onde se espera `AxisResult` |

→ [Primeiros passos](getting-started.md) · [Exceções na borda · `Try`](boundary-and-try.md)

---

## Transformar (trilho de sucesso)

| Método | Assinatura | Descrição |
|--------|-----------|-------------|
| `Map` | `T -> TNew` | transforma o valor (puro, não pode falhar) |
| `MapAsync` | `T -> Task<TNew>` | transformação async |
| `Then` | `T -> AxisResult<TNew>` | encadeia uma operação falível que retorna um novo valor |
| `Then` | `T -> AxisResult` | encadeia um efeito falível, **preserva o valor original** |
| `ThenAsync` | versões async de ambas as sobrecargas de `Then` | |
| `ToAxisResult` | `T -> AxisResult` | encadeia um efeito falível, retorna um `AxisResult` sem valor |
| `ToAxisResultAsync` | versão async de `ToAxisResult` | |

→ [Transformar · `Map`](map.md) · [Encadear · `Then`](then.md)

---

## Efeitos colaterais

| Método | Descrição |
|--------|-------------|
| `Tap(action)` | roda efeito no sucesso, devolve o resultado original |
| `TapAsync(func)` | efeito async no sucesso |
| `TapError(action)` | roda efeito na falha (logging, métricas) |
| `TapErrorAsync(func)` | efeito async na falha |

→ [Efeitos · `Tap`](tap.md)

---

## Validação

| Método | Descrição |
|--------|-------------|
| `Ensure(predicate, error)` | guarda — falha se o predicado for falso |
| `Ensure(func)` | validação delegada — `func` retorna `AxisResult` |
| `EnsureAsync` | versões async |
| `ThenUnless(predicate, next)` | guarda de sucesso — executa o `next` fallível só quando o predicado é falso; senão passa adiante |
| `ThenUnlessAsync` | versões async |
| `ThenWhen(predicate, next)` | passo condicional — executa o `next` transformador de mesmo tipo só quando o predicado é verdadeiro; senão passa adiante |
| `ThenWhenAsync` | versões async |

→ [Garantir · `Ensure`](ensure.md) · [Guarda condicional · `ThenUnless`](then-unless.md) · [Passo condicional · `ThenWhen`](then-when.md)

---

## Efeitos colaterais falíveis

| Método | Descrição |
|--------|-------------|
| `ActionAsync(func)` | roda uma operação falível (`T -> ValueTask<AxisResult>`) e **preserva o valor original** no sucesso. Disponível apenas em `ValueTask<AxisResult<T>>`. Diferente de `ThenAsync`, que substitui o valor, `ActionAsync` o mantém — ideal para validação de domínio, persistência, ou qualquer passo em que você precise do valor adiante |

→ [Encadear · `Then`](then.md)

---

## Combinar valores

| Método | Descrição |
|--------|-------------|
| `Zip(func)` | combina o valor atual com um novo numa tupla |
| `ZipAsync(func)` | versão async |
| Encadeado: `.Zip().Zip()` | constrói tuplas até `(T1, T2, T3, T4)` |
| `MapAsync((a, b) => ...)` | desestrutura tuplas no mapper |
| `ThenAsync((a, b) => ...)` | passo falível sobre a tupla, parâmetros *spread* — a forma sem valor preserva a tupla, a com valor substitui (tuplas de 2, 3 e 4) |

→ [Combinar · `Zip`](zip.md)

---

## Agregação

| Método | Descrição |
|--------|-------------|
| `Combine(results...)` | junta N resultados — coleta **todos** os erros |
| `CombineAsync(tasks)` | versão async — **paralelo** (`Task`) ou sequencial (`ValueTask`) |
| `All(results)` | junta N resultados tipados em `IReadOnlyList<T>` |
| `AllAsync(tasks)` | versão async — **paralelo** (`Task`) ou sequencial (`ValueTask`) |
| `AllAsync(items, operation)` | sequencial: uma chamada por vez, resultado tipado por item |
| `CombineAsync(items, operation)` | sequencial: uma chamada por vez, sem valor tipado |
| `ZipParallelAsync(() => other)` | roda uma op independente em paralelo, junta na tupla, acumula erros se ambos falharem |
| `ZipParallelAsync(ct => other, ct)` | variante ciente de CT |

→ [Agregar · `Combine`/`All`](aggregate.md) · [Combinar · `Zip`](zip.md)

---

## Recuperação e fallbacks

| Método | Descrição |
|--------|-------------|
| `Recover(value)` | na falha, substitui por um padrão |
| `Recover(func)` | na falha, computa um fallback |
| `RecoverWhen(predicate, func)` | recupera só se os erros casam com uma condição |
| `RecoverWhen(AxisErrorType, func)` | recupera só para um tipo de erro específico |
| `RecoverWhen(code, func)` | recupera só para um código de erro específico |
| `RecoverNotFound(func)` | recupera só se todos os erros são `NotFound` |
| `OrElse(fallback)` | tenta uma operação alternativa |
| `OrElse(fallback, combineErrors: true)` | alternativa com acúmulo de erros |

→ [Recuperar · `Recover`](recover.md)

---

## Convergência de tipo para NotFound

Não faz parte da família `Recover` — também roda no sucesso (veja [Além do Recover · `ElseNotFound`](recover.md#além-do-recover--elsenotfound)):

| Método | Descrição |
|--------|-------------|
| `ElseNotFound<TNew>(map, recovery)` | converge para um **tipo novo**: `map` no sucesso, `recovery` só se todos os erros são `NotFound`; qualquer outro erro propaga |

→ [Além do Recover · `ElseNotFound`](recover.md#além-do-recover--elsenotfound)

---

## Guardas de existência

| Método | Descrição |
|--------|-------------|
| `RequireNotFound(error)` | achou → erro, `NotFound` → `Ok`, outros erros → propaga |
| `RequireNotFoundAsync(error)` | versão async |
| `WithValueAsync(value)` | promove `AxisResult` para `AxisResult<T>` com um valor |

→ [Garantir · `Ensure`](ensure.md)

---

## Transformação de erros

| Método | Descrição |
|--------|-------------|
| `MapError(func)` | transforma cada erro individualmente |
| `MapError(func<list>)` | transforma/filtra a lista inteira de erros |
| `MapErrorAsync` | versões async |

→ [Remapear erros · `MapError`](map-errors.md)

---

## Terminal

| Método | Descrição |
|--------|-------------|
| `Match(onSuccess, onFailure)` | converte para um tipo final — roda exatamente um ramo |
| `MatchAsync` | versão async |

→ [Sair · `Match`](match.md)

---

## LINQ

| Sintaxe | Equivalente |
|--------|------------|
| `from x in result select f(x)` | `result.Map(f)` |
| `from x in r1 from y in r2 select ...` | `r1.Then(x => r2).Map(...)` |
| `SelectManyAsync` | encadeamento LINQ async |

→ [Sintaxe de query LINQ](linq-query-syntax.md)

---

## Conversão

| Método | Descrição |
|--------|-------------|
| `AsTaskAsync()` | embrulha um resultado síncrono em `Task` |
| `AsValueTaskAsync()` | embrulha um resultado síncrono em `ValueTask` |

→ [`Task` vs `ValueTask`](async-task-vs-valuetask.md)

---

## Cancelamento

Todo operador async central tem uma sobrecarga ciente de CT cujo delegate recebe o token como segundo parâmetro. Disponível em `Task<AxisResult<T>>` **e** `ValueTask<AxisResult<T>>`:

| Método | Forma do delegate |
|--------|----------------|
| `ThenAsync` | `(T, CancellationToken) => Task<AxisResult<TNew>>` |
| `ThenAsync` | `(T, CancellationToken) => Task<AxisResult>` (preserva o valor) |
| `ToAxisResultAsync` | `(T, CancellationToken) => Task<AxisResult>` |
| `MapAsync` | `(T, CancellationToken) => Task<TNew>` |
| `TapAsync` | `(T, CancellationToken) => Task` |
| `EnsureAsync` | `(T, CancellationToken) => Task<bool>` |
| `EnsureAsync` | `(T, CancellationToken) => Task<AxisResult>` |
| `ThenUnlessAsync` | `(T, CancellationToken) => Task<AxisResult>` (predicado continua síncrono; preserva o valor) |
| `ThenWhenAsync` | `(T, CancellationToken) => Task<AxisResult<T>>` (predicado continua síncrono; substitui o valor quando roda) |
| `ZipAsync` | `(T, CancellationToken) => Task<TNew>` |
| `ZipAsync` | `(T, CancellationToken) => Task<AxisResult<TNew>>` |
| `ActionAsync` | `(T, CancellationToken) => ValueTask<AxisResult>` (preserva o valor, apenas `ValueTask`) |
| `ZipParallelAsync` | `(CancellationToken) => Task<AxisResult<TNew>>` |

→ [Cancelamento](cancellation.md)

---

## Desestruturação

| Sintaxe | Produz |
|--------|--------|
| `var (isSuccess, errors) = result` | `AxisResult` |
| `var (isSuccess, value, errors) = result` | `AxisResult<T>` (`value` é `default` na falha) |

→ [Ergonomia](ergonomics.md)

---

## Veja também

- [Primeiros passos](getting-started.md) — como criar e inspecionar resultados
- [Encadear · `Then`](then.md) — o operador central do railway
- [Documentação completa](README.md) — o mapa de toda a documentação

---

↩ [Voltar à documentação do AxisResult](README.md)
