# Cancelamento · `CancellationToken`

> Todo operador async tem uma sobrecarga **ciente de `CancellationToken`**: o delegate recebe o token como último parâmetro e o operador o repassa adiante — o token flui pelo pipeline sem poluir *closures*.

---

## Quando usar

Quando você precisa passar um `CancellationToken` explicitamente por toda a cadeia (requisições canceláveis, *timeouts*).

---

## Como funciona

A sobrecarga entrega o token ao seu delegate e o encaminha:

```csharp
public Task<AxisResult<CreateOrderResponse>> HandleAsync(CreateOrderCommand cmd, CancellationToken ct)
    => customerFactory.GetByIdAsync(cmd.CustomerId, ct)
        .ThenAsync((customer, ct) => orderFactory.CreateAsync(new()
        {
            CustomerId = customer.CustomerId,
            ProductId  = cmd.ProductId,
            Quantity   = cmd.Quantity
        }, ct), ct)
        .ThenAsync((order, ct) => unitOfWork.SaveChangesAsync(ct), ct)
        .MapAsync((order, ct) => Task.FromResult(new CreateOrderResponse { OrderId = order.Id }), ct);
```

## Operadores com variante CT

`ThenAsync` · `MapAsync` · `TapAsync` · `EnsureAsync` · `ZipAsync` · `ZipParallelAsync` — em `Task<AxisResult<T>>` **e** `ValueTask<AxisResult<T>>`.

`ToAxisResultAsync` — **somente `Task<AxisResult<T>>`** (sem variante `ValueTask`).

As sobrecargas **sem** CT continuam existindo: você pode fechar sobre um token via lambda, ou misturar os dois estilos na mesma cadeia.

## Padrão preferido (DI)

Em apps com injeção de dependência, registre o `CancellationToken` do request como serviço *scoped* (resolvido de `IHttpContextAccessor` ou de um contexto ambiente) — assim os handlers não precisam enfiar o token em cada método. As sobrecargas CT-aware existem para os casos em que o *threading* explícito é preferido, ou quando o padrão ambiente não está disponível.

---

## Veja também

- [`Task` vs `ValueTask`](async-task-vs-valuetask.md) — as duas famílias async que ganham variantes CT
- [Encadear · `Then`](then.md) — o operador mais comum a receber o token

---

↩ [Voltar à documentação do AxisResult](README.md)
