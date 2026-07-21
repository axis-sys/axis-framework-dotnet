# Agregar · `Combine` / `All`

> Reduz **muitos** resultados a **um**. Diferente do [`Zip`](zip.md) (que combina valores diferentes numa tupla), aqui você dobra uma **coleção** — e coleta **todos** os erros, não só o primeiro.

---

## Quando usar

Validar vários campos de uma vez (querendo ver todas as falhas), ou consolidar uma lista de operações do mesmo tipo num único resultado.

---

## Operadores

| Método | Entra | Sai |
|---|---|---|
| `Combine(params results)` | N × `AxisResult` (sem valor) | `AxisResult` — **todos** os erros juntos |
| `All(results)` | N × `AxisResult<T>` | `AxisResult<IReadOnlyList<T>>` |
| `CombineAsync(tasks)` / `AllAsync(tasks)` | `IEnumerable<Task<…>>` ou `IEnumerable<ValueTask<…>>` | idem — **paralelo** (`Task`) ou sequencial (`ValueTask`) |
| `AllAsync(items, operation)` | `IEnumerable<T>` + delegate | `AxisResult<IReadOnlyList<TResult>>` — **sequencial** |
| `CombineAsync(items, operation)` | `IEnumerable<T>` + delegate | `AxisResult` — **sequencial** |

---

## Exemplo 1 — validar tudo e mostrar todas as falhas

```csharp
var result = AxisResult.Combine(
    ValidateName(cmd.Name),
    ValidateEmail(cmd.Email),
    ValidateAge(cmd.Age));
// coleta TODOS os erros, não só o primeiro
```

**Por que compensa:** o usuário vê de uma vez "nome vazio **e** e-mail inválido", em vez de corrigir um, reenviar, e só então descobrir o próximo. Um único *round-trip* de validação.

## Exemplo 2 — consolidar uma lista do mesmo tipo (paralelo)

```csharp
var result = await AxisResult.AllAsync(
    userIds.Select(id => GetUserAsync(id)));
// AxisResult<IReadOnlyList<User>> — ou todos os usuários, ou todos os erros
```

**Por que compensa:** "buscar N e seguir só se todos vieram" vira uma linha; se algum falhar, os erros agregados sobem juntos.

Todas as chamadas começam de forma concorrente via `Task.WhenAll` — use quando as operações são independentes e a ordem não importa.

## Exemplo 3 — execução sequencial sobre uma coleção

```csharp
var result = await AxisResult.AllAsync(userIds, GetUserAsync);
// AxisResult<IReadOnlyList<User>> — sequencial: cada chamada começa só depois da anterior terminar
```

**Por que compensa:** quando as operações precisam correr uma após a outra — processamento ordenado, limite de quota, operações que dependem de efeitos colaterais da anterior — você passa a coleção e o delegate diretamente em vez de envolver cada chamada numa lambda dentro de um `Select`. A convenção de chamada é idêntica à versão paralela, tornando a intenção clara pela assinatura.

```csharp
// Também disponível sem valor de retorno:
var result = await AxisResult.CombineAsync(commands, cmd => SendAsync(cmd));
```

---

## Paralelo vs sequencial — escolhendo a sobrecarga certa

| Cenário | Sobrecarga |
|---------|------------|
| Operações independentes, ordem não importa | `AllAsync(items.Select(op))` — paralelo |
| Ordenado, limitado por quota ou com dependência de efeito colateral | `AllAsync(items, op)` — sequencial |

---

## `Combine`/`All` vs `Zip`

- **`Combine`/`All`** → N itens do **mesmo** tipo → uma lista (ou um void agregado).
- **[`Zip`](zip.md)** → 2–4 valores **diferentes** → uma tupla.

---

## Veja também

- [Combinar · `Zip`](zip.md) — para valores heterogêneos numa tupla
- [Erros e tipos](errors-and-types.md) — por que acumular todos os erros importa
- [Garantir · `Ensure`](ensure.md) — validação de um único valor na trilha

---

↩ [Voltar à documentação do AxisResult](README.md)
