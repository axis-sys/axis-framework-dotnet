# Sair · `Match`

> O fim da trilha. `Match` colapsa um `AxisResult` em um **valor final** — rodando exatamente **um** dos dois ramos (sucesso ou falha).

```csharp
string msg = result.Match(
    onSuccess: value  => $"ok: {value}",
    onFailure: errors => $"falhou: {errors[0].Code}");
```

---

## Quando usar

No limite do pipeline, para converter o resultado em uma resposta HTTP, uma mensagem, um DTO — qualquer tipo concreto.

---

## Operadores

| Método / sintaxe | O que faz |
|---|---|
| `Match(onSuccess, onFailure)` | roda **um** ramo e retorna o tipo final |
| `MatchAsync` | versão async (`Task`/`ValueTask`) |
| `var (ok, value, errors) = result` | desestruturação posicional |
| `IsSuccess` / `IsFailure` | inspeção booleana |
| `Value` | o valor (**lança** se acessado numa falha) |
| `Errors` / `JoinErrorCodes()` | a lista de erros / os códigos concatenados |

---

## Exemplo real — borda HTTP

O lugar canônico do `Match` é o controller, traduzindo a trilha para um status code:

```csharp
return result.Match(
    onSuccess: value  => Ok(value),
    onFailure: errors => Problem(
        title:  errors[0].Type.ToString(),
        detail: messageResolver.Resolve(errors[0], CultureInfo.CurrentUICulture)));
```

**Por que compensa:** sucesso e falha são tratados no **mesmo lugar**, sem `try/catch`, e é impossível esquecer um dos ramos — os dois são exigidos.

## Desestruturação segura

```csharp
var (isSuccess, value, errors) = await GetUserAsync(id);
// numa falha, `value` é default(T) — a desestruturação NUNCA lança
```

Diferente de `.Value` (que lança numa falha), a desestruturação é segura sem checar `IsSuccess` antes — ideal em *pattern matching*.

---

## Veja também

- [Erros e tipos](errors-and-types.md) — o `Type` que vira status HTTP
- [Encadear · `Then`](then.md) — o que vem antes do `Match`
- [Recuperar · `Recover`](recover.md) — tratar a falha **sem** sair do pipeline

---

↩ [Voltar à documentação do AxisResult](README.md)
