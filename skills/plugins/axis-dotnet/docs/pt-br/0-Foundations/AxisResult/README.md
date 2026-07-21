# AxisResult — Documentação

> 🌐 [English (README principal)](../../../en-us/0-Foundations/AxisResult/README.md)

**Railway-Oriented Programming para C#** — uma *Result monad* sem dependências, com `async`/`ValueTask` completos, categorias de erro tipadas e composição monádica (`Then` / `Map` / `Zip`).

```csharp
public Task<AxisResult<AddCellphoneResponse>> HandleAsync(AddCellphoneCommand cmd)
    => personFactory.GetByIdAsync(cmd.PersonId)
        .ThenAsync(person => cellphoneMediator.AddAsync(new() { Number = cmd.Number }))
        .ThenAsync(response => response.AddCellphoneAsync(cmd.CellphoneId))
        .ThenAsync(_ => unitOfWork.SaveChangesAsync())
        .MapAsync(_ => new AddCellphoneResponse { CellphoneId = cmd.CellphoneId });
```

Use esta página como **mapa**: leia o tronco abaixo (~5 min) e salte direto para o detalhe do grupo que você precisa — sem ler centenas de linhas.

---

## O tronco (leia primeiro)

### Railway em 60 segundos

Imagine seu código como uma ferrovia de dois trilhos:

```
Sucesso ━━━━━●━━━━━━━━━━●━━━━━━━━━━●━━━━▶  resultado
             │          │          │
          validar     buscar     salvar
             │          │          │
Falha   ━━━━━╋━━━━━━━━━━╋━━━━━━━━━━╋━━━━▶  erros
```

Cada operação **ou** tem sucesso e segue no trilho de cima, **ou** falha e cai no de baixo — pulando todo o resto. Sem `try/catch`, sem `if (x == null)`, sem `return` no meio do handler. → **[Railway-Oriented Programming](railway-oriented-programming.md)**

### `AxisResult` vs `AxisResult<T>` — "sem dados" e "com dados"

- **`AxisResult`** — o desfecho de uma operação que **não produz valor**: só importa se deu certo (salvar, deletar, validar, verificar senha).
- **`AxisResult<T>`** — carrega um **valor** pela trilha de sucesso (buscar entidade, calcular total). `.Value` lança numa falha → prefira a [desestruturação segura ou `Match`](match.md).
- Transitar entre os dois: [`ToAxisResult`](then.md) descarta o valor; [`WithValueAsync`](ensure.md) promove um `AxisResult` para `AxisResult<T>`.

### Criar resultados

```csharp
AxisResult         ok    = AxisResult.Ok();
AxisResult<int>    typed = AxisResult.Ok(42);
AxisResult<int>    fail  = AxisError.BusinessRule("INSUFFICIENT_STOCK"); // AxisError → falha (implícito)
AxisResult<string> name  = "John";                                       // valor → Ok (implícito)
AxisResult<int>    parse = AxisResult.Try(() => int.Parse(input));        // exceção → AxisResult, só na borda
AxisResult<string> rop   = usuario.Email.Rop();                            // valor → Ok, fluente: inicia o fluxo ROP
```

### Tratamento de erros

Um erro é um **valor** (`AxisError` = `Code` + `Type`), não uma exceção. As 12 categorias mapeiam para status HTTP, e `IsTransient` habilita retry. → **[Erros e tipos](errors-and-types.md)**

### `Task` vs `ValueTask`

Na dúvida, use `Task`. `ValueTask` só em *hot paths* que completam de forma síncrona. → **[Task vs ValueTask](async-task-vs-valuetask.md)**

### Instalação

```
dotnet add package AxisResult
```

→ Guia completo: **[Primeiros passos](getting-started.md)**

---

## O mapa (salte para o que precisa)

| Grupo                           | Você quer…                                        | Detalhe                            |
|---------------------------------|---------------------------------------------------|------------------------------------|
| **Transformar · `Map`**         | mudar o valor (não pode falhar)                   | [map.md](map.md)                   |
| **Encadear · `Then`** ⭐         | um passo que **pode falhar** (coração da library) | [then.md](then.md)                 |
| **Garantir · `Ensure`**         | validar um invariante inline                      | [ensure.md](ensure.md)             |
| **Guarda · `ThenUnless`**       | executar um passo fallível só quando a condição é falsa | [then-unless.md](then-unless.md) |
| **Passo condicional · `ThenWhen`** | executar um passo transformador de mesmo tipo só quando a condição é verdadeira | [then-when.md](then-when.md) |
| **Sair · `Match`**              | colapsar o pipeline num valor final               | [match.md](match.md)               |
| **Efeitos · `Tap`**             | observar (log/métrica) sem mudar a trilha         | [tap.md](tap.md)                   |
| **Recuperar · `Recover`**       | tratar a falha e voltar ao sucesso                | [recover.md](recover.md)           |
| **Combinar · `Zip`**            | juntar valores **diferentes** numa tupla          | [zip.md](zip.md)                   |
| **Agregar · `Combine`/`All`**   | reduzir **N** resultados a um                     | [aggregate.md](aggregate.md)       |
| **Remapear erros · `MapError`** | reescrever erros entre camadas                    | [map-errors.md](map-errors.md)     |
| **Cancelamento**                | passar `CancellationToken` pela cadeia            | [cancellation.md](cancellation.md) |

**Comece aqui:** [Primeiros passos](getting-started.md) · [Railway-Oriented Programming](railway-oriented-programming.md) · [Por que AxisResult?](why-axisresult.md)

**Fundamentos:** [Erros e tipos](errors-and-types.md) · [`Task` vs `ValueTask`](async-task-vs-valuetask.md) · [Exceções na borda](boundary-and-try.md)

**Referência e extras:** [Referência da API](api-reference.md) · [Sintaxe de query LINQ](linq-query-syntax.md) · [Ergonomia](ergonomics.md)

---

## Princípios de design

1. **Erros são valores, não exceções.** Uma operação que pode falhar diz isso no tipo de retorno.
2. **O sistema de tipos é a documentação.** `Task<AxisResult<User>>` já diz tudo que pode acontecer.
3. **Composição em vez de cerimônia.** Operações pequenas e focadas que se compõem.
4. **Falhe rápido, recupere de propósito.** Erros propagam sozinhos; recuperação é sempre explícita.
5. **Exceções na borda, resultados em todo o resto.** `AxisResult.Try()` nas bordas de infraestrutura; acima disso, livre de exceções.

---

## Licença

Apache 2.0
