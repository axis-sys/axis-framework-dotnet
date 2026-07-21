# Exceções na borda · `Try` e adaptadores

> "Exceções na borda, resultados em todo o resto." Esta página mostra como converter superfícies de infraestrutura que lançam exceções (banco, HTTP, brokers) em `AxisResult` num único ponto — e o que o `Try` **não** captura.

---

## Quando usar

Em qualquer ponto onde código externo lança exceções: drivers de banco, `HttpClient`, brokers de mensagem, I/O de arquivo. Acima dessa borda, tudo é livre de exceções.

## Quando *não* usar

| Você quer… | Use no lugar |
|---|---|
| encadear passos que já retornam `AxisResult` | [`Then`](then.md) |
| recuperar de uma falha transiente | [`Recover`](recover.md) |

---

## Exemplo real — acesso a banco sem exceções

```csharp
protected async Task<AxisResult<T>> GetAsync<T>(
    string sql,
    Action<NpgsqlParameterCollection> addParams,
    Func<NpgsqlDataReader, T> map,
    string notFoundCode)
{
    try
    {
        await using var command = await uow.NewCommandAsync(sql);
        addParams(command.Parameters);
        await using var reader = await command.ExecuteReaderAsync(CancellationToken);
        if (!await reader.ReadAsync(CancellationToken))
            return AxisError.NotFound(notFoundCode);
        return AxisResult.Ok(map(reader));
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "POSTGRES_GET_ERROR");
        return AxisError.InternalServerError("POSTGRES_GET_ERROR");
    }
}
```

**Por que compensa:** o único `try/catch` de toda a arquitetura vive na borda de infraestrutura. As exceções do banco são convertidas em `AxisResult` uma vez, na borda. Tudo acima — handlers, factories, regras de domínio — é livre de exceções.

---

## Padrão de adaptador de borda

Para `HttpClient`, drivers de banco, brokers de mensagem e qualquer outra infraestrutura com uma superfície de exceções vazante, o padrão recomendado é escrever um **adaptador** fino cujo trabalho é *exclusivamente* converter a superfície externa em `AxisResult<Response>`. Cada exceção que o cliente subjacente pode lançar é mapeada para um `AxisError` tipado (timeouts → `Timeout`, 5xx → `ServiceUnavailable`, 401 → `Unauthorized`, etc.). O resto da aplicação consome apenas `AxisResult<Response>` e nunca vê um `HttpResponseMessage` cru. Libraries auxiliares dedicadas para os adaptadores de HTTP e de repositório estão no roadmap.

---

## Achatar uma borda que retorna resultado · `TryBind`

`Try` envolve uma função que retorna um **valor puro**. Quando a operação de borda *ao mesmo tempo* lança (rede, desserialização) *e* reporta suas próprias falhas controladas (um status HTTP não-sucesso mapeado para um `AxisError`), o callback naturalmente retorna um `AxisResult<T>` — e o `Try` o aninharia em `AxisResult<AxisResult<T>>`. Use **`TryBind` / `TryBindAsync`** no lugar: ele captura as exceções *e* achata o resultado retornado.

```csharp
private Task<AxisResult<List<Role>>> GetRolesAsync(string path)
    => AxisResult.TryBindAsync(
        async () =>
        {
            using var response = await httpClient.GetAsync(path);          // pode lançar (rede/timeout)
            if (!response.IsSuccessStatusCode)                             // falha controlada — retornada, não lançada
                return AxisError.ServiceUnavailable("V1_UNAVAILABLE");
            var roles = await response.Content.ReadFromJsonAsync<List<Role>>();  // pode lançar (JSON malformado)
            return AxisResult.Ok(roles ?? []);
        },
        ex => AxisError.ServiceUnavailable("V1_UNAVAILABLE"));             // ambas as exceções mapeadas aqui
```

Um `AxisError` controlado retornado pelo callback passa direto; uma exceção lançada por ele é capturada e mapeada (mesmas regras de relançamento `IsCritical` do `Try`). Sem `TryBind`, você cairia num truque de lançar-pra-controle (`response.EnsureSuccessStatusCode()`) ou num `try/catch` escrito à mão.

> **Task-only por design.** Não há sobrecarga `ValueTask` de `Try` / `TryAsync` / `TryBindAsync`. Uma factory estática recebe o callback como lambda, e `async () => …` é ambíguo entre `Func<Task…>` e `Func<ValueTask…>` (CS0121) — uma irmã `ValueTask` quebraria todo caller `…(async () => …)`. É um limite intencional dos helpers em estilo factory, não um esquecimento.

---

## Nota sobre `Try` / `TryBind`

`AxisResult.Try` e `AxisResult.TryBind` **não** capturam exceções de "erro de programação" — `NullReferenceException`, `ArgumentNullException`, `OperationCanceledException`, `OutOfMemoryException`, `StackOverflowException` e `ThreadAbortException` são relançadas. Elas representam bugs ou situações genuinamente irrecuperáveis e não devem ser silenciosamente transformadas em um valor de resultado. Se você quiser capturar um tipo de exceção específico, passe um override `errorHandler` ou capture-a manualmente no seu adaptador.

---

## Veja também

- [Encadear · `Then`](then.md) — o que consome o `AxisResult` que a borda produz
- [Erros e tipos](errors-and-types.md) — os tipos para os quais mapear cada exceção
- [Remapear erros · `MapError`](map-errors.md) — traduzir códigos ao cruzar camadas

---

↩ [Voltar à documentação do AxisResult](README.md)
