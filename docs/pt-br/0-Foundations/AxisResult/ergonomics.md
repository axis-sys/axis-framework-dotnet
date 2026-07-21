# Ergonomia · desestruturação e debugger

> Pequenos detalhes de qualidade de vida: desestruturar resultados sem `try/catch` e enxergar o estado direto no debugger, sem expandir campos privados.

---

## Desestruturação

Tanto `AxisResult` quanto `AxisResult<T>` suportam desestruturação posicional:

```csharp
var (isSuccess, errors) = AxisResult.Ok();                 // não-genérico
var (isSuccess, value, errors) = AxisResult.Ok(42);        // genérico

// Também usável em padrões posicionais:
if (result is (true, var v, _))
    Console.WriteLine($"got {v}");
```

Num `AxisResult<T>` que falhou, `value` é `default(T)` (não uma exceção) — a desestruturação **nunca lança**, então é seguro usá-la em pattern matching sem checar `IsSuccess` antes.

---

## Experiência no debugger

`AxisResult`, `AxisResult<T>` e `AxisError` todos carregam atributos `[DebuggerDisplay]`. No debugger você vê:

- `Ok` / `Ok(42)` para sucesso
- `Error[2]: USER_NOT_FOUND, INVALID_EMAIL` para falhas
- `NotFound USER_NOT_FOUND` para erros individuais

**Por que compensa:** chega de inspecionar campos privados ou expandir cada nó — o estado relevante aparece direto na visão padrão.

---

## Veja também

- [Sair · `Match`](match.md) — a outra forma de extrair o valor com segurança
- [Erros e tipos](errors-and-types.md) — o que o `[DebuggerDisplay]` de um `AxisError` mostra
- [Referência da API](api-reference.md) — a tabela de desestruturação

---

↩ [Voltar à documentação do AxisResult](README.md)
