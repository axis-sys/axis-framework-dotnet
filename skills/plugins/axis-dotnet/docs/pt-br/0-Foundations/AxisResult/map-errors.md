# Remapear erros · `MapError`

> Reescreve os erros da trilha de falha — útil para **traduzir** códigos e tipos quando um resultado cruza uma fronteira de camada ou de contexto.

---

## Quando usar

Um serviço interno falhou com códigos internos e você quer expô-los com os códigos/tipos da sua camada, sem vazar detalhes de baixo nível.

## Quando *não* usar

| Você quer… | Use no lugar |
|---|---|
| **recuperar** da falha (voltar ao sucesso) | [`Recover`](recover.md) |
| só **observar** o erro (log) | [`TapError`](tap.md) |

---

## Operadores

| Método | Transforma |
|---|---|
| `MapError(Func<AxisError, AxisError>)` | **cada** erro, individualmente |
| `MapError(Func<IReadOnlyList<AxisError>, IEnumerable<AxisError>>)` | a **lista inteira** (filtrar/reestruturar) |
| `MapErrorAsync` | versões async (`Task`/`ValueTask`) |

---

## Exemplo real — atravessar uma fronteira de camada

```csharp
var result = await internalService.ProcessAsync()
    .MapErrorAsync(errors => errors.Select(e =>
        AxisError.InternalServerError($"PROCESSING_{e.Code}")));
```

**Por que compensa:** os códigos internos do serviço viram códigos estáveis da sua API (`PROCESSING_*`) num único ponto — a borda. Nada de baixo nível vaza, e o resto do pipeline nunca precisa conhecer os detalhes internos.

---

## Veja também

- [Erros e tipos](errors-and-types.md) — o que um `AxisError` carrega
- [Recuperar · `Recover`](recover.md) — tratar a falha em vez de só reescrevê-la
- [Sair · `Match`](match.md) — onde os erros viram resposta final

---

↩ [Voltar à documentação do AxisResult](README.md)
