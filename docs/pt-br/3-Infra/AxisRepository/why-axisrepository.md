# Por que AxisRepository? · comparação

> Há outras maneiras de cuidar de persistência em .NET. Esta página diz por que o AxisRepository é diferente — uma comparação direta, sem mão na cintura.

---

## vs. `DbContext` (EF Core) direto

`DbContext` é excelente para domínios em formato CRUD. Três dores aparecem na escala:

1. **Change tracking vaza.** `SaveChangesAsync` commita *o que você mutou*; rastrear um side effect perdido é trabalho de forense.
2. **`SaveChangesAsync` lança.** Você precisa de `try/catch` ao redor de cada pipeline de comando — ou embrulha tudo no seu próprio `Result`. `AxisRepository` já retorna `AxisResult`.
3. **Transações não são first-class na ferrovia.** O `IDbContextTransaction` do EF está OK, mas compor com helpers que retornam `Result` significa escrever o mesmo wrapper que todo mundo escreve.

`AxisRepository` **não** é um substituto para ORM. Você pode embrulhar um `DbContext` dentro de um `IAxisUnitOfWork` custom e manter o fluxo `AxisResult` na camada de aplicação — o melhor dos dois mundos.

## vs. Dapper direto

Dapper também é excelente — e o executor `IAxisDbRepository` do AxisRepository é uma camada fina sobre o driver que preenche o mesmo nicho. A diferença: AxisRepository adiciona **códigos de erro tipados**, **retry transiente** e **orquestração de transação via `InTransactionAsync`**. Se você só precisa de queries pontuais, Dapper basta; se entrega pipelines multi-passo, o executor poupa o boilerplate.

## vs. um `IUnitOfWork` caseiro

DIY. Mesma forma (`Start`/`Commit`/`Rollback`), mas você redescobre: como retornar erros, como tratar exceções dentro de `InTransaction`, como plugar retries, como registrar providers keyed. `IAxisUnitOfWork` poupa o custo — e integra limpinho com `AxisResult`, `AxisMediator` (cancelamento), `AxisLogger`, `AxisTelemetry`.

## vs. bibliotecas de Event Sourcing

Problema diferente. AxisRepository é para sistemas **state-stored** com integridade relacional. Event sourcing substitui tanto a persistência quanto o modelo.

---

## A comparação

| Característica | AxisRepository | EF Core direto | Dapper direto | `IUnitOfWork` caseiro |
|---|:--:|:--:|:--:|:--:|
| Retorna `AxisResult` | **Sim** | Não | Não | Talvez |
| Wrapper `InTransactionAsync(railway)` | **Sim** | Não | Não | Talvez |
| Cancelamento implícito via `IAxisMediator` | **Sim** | Não | Não | Talvez |
| Violação unique-key → `Conflict` tipado | **Sim (na caixa)** | Não | Não | Talvez |
| Retry em `SqlState` transiente com backoff | **Sim (na caixa)** | Não | Não | Talvez |
| Registro multi-database keyed | **Sim** | Manual | Manual | Talvez |
| Adapter Postgres embarcado | **Sim** | n/a | n/a | Não |
| Brinca com `AxisLogger` / `AxisTelemetry` | **Sim** | Indireto | Indireto | Manual |
| Zero deps NuGet na abstração | **Sim** | n/a | n/a | Sim |

---

## Veja também

- [O contrato `IAxisUnitOfWork`](iaxisunitofwork.md) — a superfície
- [`InTransactionAsync`](in-transaction.md) — o operador que justifica a abstração
- [Adapter Postgres](postgres-adapter.md) — a implementação na caixa

---

↩ [Voltar à documentação do AxisRepository](README.md)
