# Encadear · `Then`

> **O coração do railway.** `Then` encadeia um passo que **pode falhar**. Se ele falhar, todos os passos seguintes são pulados — nunca mais um `if (result.IsFailure) return;`.

No **trilho de sucesso**, `Then` executa a próxima operação com o valor atual. No **trilho de falha**, ele não faz nada e deixa os erros existentes seguirem intactos.

```csharp
var result = await GetUserAsync(id)            // AxisResult<User>
    .ThenAsync(user => ValidateAsync(user))    // só roda se GetUser teve sucesso
    .ThenAsync(user => SaveAsync(user));        // só roda se Validate teve sucesso
```

---

## Quando usar

Use `Then` quando o próximo passo **retorna um `AxisResult`** (pode falhar) e você quer que essa falha **interrompa (curto-circuite)** o resto do pipeline.

## Quando *não* usar

| Você quer… | Use no lugar |
|---|---|
| transformar o valor com algo que **não pode falhar** (montar um DTO) | [`Map`](map.md) |
| **observar** o valor (log, métrica) sem mudar o trilho | [`Tap`](tap.md) |
| manter **os dois** valores, o antigo e o novo | [`Zip`](zip.md) |
| executar o passo **só quando uma condição sobre o valor é falsa** (pular se já está feito) | [`ThenUnless`](then-unless.md) |
| executar um passo **transformador de mesmo tipo** só quando uma condição sobre o valor é verdadeira | [`ThenWhen`](then-when.md) |

---

## As quatro formas (a parte que mais confunde)

O comportamento depende **do que o delegate retorna**, não do nome do método:

| Forma                                | Delegate retorna         | O que segue adiante                  | Use quando                                                          |
|--------------------------------------|--------------------------|--------------------------------------|---------------------------------------------------------------------|
| `Then` / `ThenAsync`                 | `AxisResult<TNew>`       | o **novo** valor                     | o passo produz um novo valor                                        |
| `Then` / `ThenAsync`                 | `AxisResult` (sem valor) | o valor **original**                 | um passo falível sem retorno (persistir, invalidar cache)           |
| `ToAxisResult` / `ToAxisResultAsync` | `AxisResult`             | **nada** (reduz para o não-genérico) | o valor tipado não é mais necessário (ex.: um command sem resposta) |

> **Regra prática:** se o próximo passo *retorna* um valor → ele **substitui**. Se retorna um `AxisResult` simples → o valor **sobrevive**.

---

## Sobrecargas disponíveis

Toda forma existe para o `AxisResult<T>` síncrono, para pipelines `Task<AxisResult<T>>` e `ValueTask<AxisResult<T>>`. Cada uma também tem uma variante [ciente de `CancellationToken`](cancellation.md), em que o delegate recebe o token como último parâmetro.

```csharp
// substitui o valor
AxisResult<TNew>  Then(Func<T, AxisResult<TNew>> next)
Task<AxisResult<TNew>>  ThenAsync(Func<T, Task<AxisResult<TNew>>> next)

// preserva o valor (o delegate retorna um AxisResult sem valor)
AxisResult<T>  Then(Func<T, AxisResult> next)
Task<AxisResult<T>>  ThenAsync(Func<T, Task<AxisResult>> next)

// descarta o valor por completo
AxisResult  ToAxisResult(Func<T, AxisResult> next)
AxisResult  ToAxisResult()                       // apenas estreita AxisResult<T> → AxisResult
```

### Trilhos de tupla: sobrecargas *spread*

Quando o trilho carrega uma tupla (depois de um [`Zip`](zip.md)), o `ThenAsync` também existe na forma **spread** — o delegate recebe os elementos da tupla como **parâmetros separados e nomeáveis**, sem nunca tocar em `.Value1` / `.Value2`. Disponível para tuplas de 2, 3 e 4 elementos em pipelines `Task`:

```csharp
// spread sem valor: executa o passo e PRESERVA a tupla inteira
Task<AxisResult<(T1, T2)>>  ThenAsync(Func<T1, T2, AxisResult> next)
Task<AxisResult<(T1, T2)>>  ThenAsync(Func<T1, T2, Task<AxisResult>> next)

// spread com valor: o passo produz TNew, que substitui a tupla
Task<AxisResult<TNew>>  ThenAsync(Func<T1, T2, AxisResult<TNew>> next)
Task<AxisResult<TNew>>  ThenAsync(Func<T1, T2, Task<AxisResult<TNew>>> next)
```

```csharp
await LoadUserAsync(id)                                     // AxisResult<User>
    .ZipAsync(user => IssueTokenAsync(user))                // (user, token)
    .ThenAsync((user, token) => store.SaveAsync(token))     // side effect; (user, token) sobrevive
    .MapAsync((user, token) => new Response(user.Name, token.Value));
```

> A convenção *spread* é uniforme na superfície de tupla: `ZipAsync((a, b) => …)` estende, `ThenAsync((a, b) => …)` executa um passo falível, `MapAsync((a, b) => …)` colapsa.

---

## Exemplos reais

### 1. Pipeline de comando: regenerar o segredo de uma API

Carregar a entidade, alterá-la, persistir, invalidar o cache, retornar a resposta — cinco passos falíveis, zero `try/catch`. Se **qualquer** passo falhar, tudo depois dele (inclusive o `SaveChanges`) é pulado.

```csharp
public Task<AxisResult<GenerateNewSecretResponse>> HandleAsync(GenerateNewSecretCommand cmd)
{
    var plain  = ExternalApiSecret.Generate();
    var hashed = ExternalApiSecret.Hash(plain);

    return factory.GetByIdAsync(cmd.ExternalApiId)  // AxisResult<ExternalApi> → NotFound se não existe
        .ThenAsync(api => api.UpdateSecretAsync(hashed))  // AxisResult → preserva a api
        .ThenAsync(_ => uow.SaveChangesAsync())  // AxisResult → preserva a api
        .ThenAsync(_ => cacheResolver.RemoveAsync(cmd.ExternalApiId))  // → preserva a api
        .MapAsync(_ => new GenerateNewSecretResponse { ExternalApiId = cmd.ExternalApiId, Secret = plain });
}
```

**Por que compensa:** os três passos do meio retornam um `AxisResult` sem valor, então o `App` continua fluindo — o `MapAsync` final ainda tem tudo de que precisa, e um `UpdateSecret` que falhe nunca chega ao `SaveChanges`.

### 2. Criar, se não existir: preservar para validar, depois persistir

```csharp
public Task<AxisResult<IPersonAggregateApplication>> CreateAsync(NewArgs args)
    => readerPort.GetByDocumentAsync(args.Document)
        .RequireNotFoundAsync(AxisError.ValidationRule("DOCUMENT_ALREADY_EXISTS")) // achou → falha
        .WithValueAsync(new PersonEntity(args.Document, args.DisplayName)) // AxisResult → AxisResult<Entity>
        .MapAsync(NewInstance) // Entity → New Instance of Entity Application (não pode falhar → Map, não Then)
        .ThenAsync(app => app.IsValidAsync()) // valida; PRESERVA o app
        .ThenAsync(writePort.CreateAsync); // persiste, mas não salva (pode ser salvo por unit of work depois)
```

**Por que compensa:** `ThenAsync` vs `MapAsync` — a validação pode falhar (então está no railway e preserva o valor - ThenAsync), enquanto mapear a entidade não falha (é um `Map` simples). Se houver uma exception no mapping, é erro de programação e deve ser propagado como um tal.

### 3. Sem resposta? Descarte o valor com `ToAxisResult`

Um command sem payload estreita o pipeline tipado de volta para um `AxisResult` simples:

```csharp
public Task<AxisResult> HandleAsync(DeleteExternalApiCommand cmd)
    => factory.GetByIdAsync(cmd.ExternalApiId) // AxisResult<ExternalApiApplication>
        .ToAxisResultAsync(app => app.DeleteAsync());  // a operação é finalizada e o app é descartado, retornando um AxisResult
```

**Por que compensa:** a assinatura do handler é `Task<AxisResult>` (sem resposta), e `ToAxisResultAsync` faz o pipeline terminar exatamente nesse tipo — sem valor de mentira, sem `Map(_ => Unit)`.

### 4. Checkout: o produto sobrevive à reserva de estoque

Um `ThenAsync` sem valor (reservando estoque) executa seu passo mas segue com o **produto original** adiante, então o `MapAsync` final ainda o tem. Este trecho é extraído verbatim do [`Scaffolds` sample](../../../../src/scaffolds/Core/Scaffolds.ECommerce.Application/Catalog/UseCases/Checkout/v1/CheckoutHandler.cs) compilado e testado por unidade — não pode divergir da API real.

<!-- scaffold:checkout -->

```csharp
public Task<AxisResult<CheckoutResponse>> HandleAsync(CheckoutCommand command)
{
    return products
        .GetByIdAsync(command.ProductId)
        .EnsureAsync(product => product.Stock >= command.Quantity, AxisError.BusinessRule(CatalogErrors.InsufficientStock))
        .ThenAsync(product => products.ReserveStockAsync(product.ProductId, command.Quantity))
        // Publish before SaveChangesAsync: the atomic outbox drains the enqueued event in the very same
        // transaction (architecture-events-published-in-unit-of-work). The cart consumer picks this up
        // out of the band and associates the reserved product with the cart (architecture-bus-events).
        .ThenAsync(product => bus.PublishAsync(
                new ProductCheckedOutEvent(
                    command.CartId,
                    product.ProductId.ToString(),
                    command.Quantity
                ), ProductCheckedOutEvent.Topic))
        .ThenAsync(_ => unitOfWork.SaveChangesAsync())
        .MapAsync(product => new CheckoutResponse
        {
            Customer = mediator.AxisEntityId!.Value,
            ProductId = product.ProductId,
        });
}
```

<!-- /scaffold -->

**Por que compensa:** `EnsureAsync` protege o invariante, o `ThenAsync` sem valor reserva o estoque preservando o produto, e o `MapAsync` (que não pode falhar) monta a resposta — uma ferrovia, zero `if`/`try`.

---

## Veja também

- [`Map`](map.md) — transformar um valor que não pode falhar
- [`Ensure`](ensure.md) — garantir um invariante inline (`RequireNotFound`, `WithValueAsync`)
- [`Zip`](zip.md) — manter o valor antigo *e* um novo
- [Erros e tipos](errors-and-types.md) — o que um `AxisError` carrega e as 12 categorias
- [`Task` vs `ValueTask`](async-task-vs-valuetask.md) — qual forma async encadear

---

↩ [Voltar à documentação do AxisResult](README.md)
