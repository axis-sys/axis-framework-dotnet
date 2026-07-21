# `InTransactionAsync` — o wrapper de transação certo

> Helpers default-interface em `IAxisUnitOfWork` que orquestram `StartAsync` / `SaveChangesAsync` / `RollbackAsync` contra um delegate ciente de `AxisResult`. Commit no `Ok`, rollback no `Error`, rollback-e-relança em exceção.

```csharp
return uow.InTransactionAsync(() =>
    factory.CreateAsync(cmd)
        .ThenAsync(person => writer.CreateAsync(person))
        .MapAsync(_       => new CreatePersonResponse { PersonId = cmd.PersonId }));
```

---

## Quando usar

Qualquer pipeline que **escreve** e deve ser atômico. Pareie com `Then`/`Map`/`Tap` do `AxisResult` para encadear os passos; deixe `InTransactionAsync` observar o resultado.

## Quando *não* usar

| Você quer… | Use no lugar |
|---|---|
| rodar um pipeline somente-leitura | chame o repositório direto; sem transação |
| commitar um resultado parcial em falha | as três primitivas manualmente (`StartAsync` / `SaveChangesAsync` / `RollbackAsync`) — seja muito deliberado |
| espalhar a transação por dois stores | um adapter custom; um único `IAxisUnitOfWork` não coordena Postgres + Mongo |

---

## Os dois overloads

| Overload | Retorna | Notas |
|---|---|---|
| `InTransactionAsync(Func<Task<AxisResult>>)` | `Task<AxisResult>` | para pipelines sem valor de saída (comandos sem response tipada) |
| `InTransactionAsync<T>(Func<Task<AxisResult<T>>>)` | `Task<AxisResult<T>>` | preserva o valor tipado na saída; se o `SaveChangesAsync` falha, retorna os erros do **save** |

---

## A máquina de estados

| Estado | Retorna | Side effect |
|---|---|---|
| `StartAsync` falha | o `Error(errors)` do start | nenhum `work` roda |
| `work()` retorna `Ok` e `SaveChangesAsync` tem sucesso | `Ok` (ou `Ok(value)`) | commitado |
| `work()` retorna `Ok` e `SaveChangesAsync` falha | o `Error(errors)` do save (valor perdido no overload tipado) | nada commitado |
| `work()` retorna `Error` | o `Error(errors)` do work | rollback |
| `work()` lança | relança | rollback, depois relança |

---

## Exemplos reais

### 1. Duas escritas em uma transação

```csharp
public Task<AxisResult<CreateInvoiceResponse>> HandleAsync(CreateInvoiceCommand cmd)
    => uow.InTransactionAsync(() =>
        invoiceFactory.CreateAsync(cmd)
            .ThenAsync(invoice => invoiceWriter.CreateAsync(invoice))
            .ThenAsync(invoice => ledgerWriter.PostAsync(invoice))
            .MapAsync(invoice => new CreateInvoiceResponse { InvoiceId = invoice.InvoiceId }));
```

**Por que compensa:** a invoice e sua entrada de ledger desembarcam atomicamente. Uma falha em `ledgerWriter.PostAsync` faz rollback da invoice, sem linhas órfãs.

### 2. Outbox transacional

```csharp
public Task<AxisResult<CreateOrderResponse>> HandleAsync(CreateOrderCommand cmd)
    => uow.InTransactionAsync(() =>
        factory.CreateAsync(cmd)
            .ThenAsync(order => writer.CreateAsync(order))
            .ThenAsync(_     => outboxBus.PublishAsync(new OrderCreatedEvent(cmd.OrderId)))
            .MapAsync(_      => new CreateOrderResponse { OrderId = cmd.OrderId }));
```

**Por que compensa:** o bus é um adapter outbox — `PublishAsync` escreve uma linha na mesma conexão. Commit = duas linhas; rollback = nenhuma. A clássica race de dual-write some.

### 3. Validar dentro da transação (leituras + escritas baratas)

```csharp
public Task<AxisResult> HandleAsync(TransferFundsCommand cmd)
    => uow.InTransactionAsync(() =>
        accountReader.GetForUpdateAsync(cmd.FromAccountId)
            .ThenAsync(from => from.DebitAsync(cmd.Amount))
            .ThenAsync(_    => accountReader.GetForUpdateAsync(cmd.ToAccountId))
            .ThenAsync(to   => to.CreditAsync(cmd.Amount))
            .ThenAsync(to   => accountWriter.UpdateAsync(to)));
```

**Por que compensa:** os locks de `FOR UPDATE` viajam dentro de uma transação. Um débito falho (saldo insuficiente) faz rollback do workflow inteiro — e o crédito nunca desembarca sem o débito.

---

## Veja também

- [O contrato `IAxisUnitOfWork`](iaxisunitofwork.md) — as primitivas que o wrapper chama
- [Adapter Postgres](postgres-adapter.md) — como `StartAsync`/`SaveChangesAsync`/`RollbackAsync` mapeiam para Postgres

---

↩ [Voltar à documentação do AxisRepository](README.md)
