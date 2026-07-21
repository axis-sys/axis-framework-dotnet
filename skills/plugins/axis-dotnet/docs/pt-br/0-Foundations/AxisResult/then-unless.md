# Guarda condicional · `ThenUnless`

> Executa o passo fallível **somente quando o predicado é falso**. Quando o predicado é verdadeiro — o estado desejado já vale — o sucesso passa adiante com o valor intacto, e o `next` nunca roda.

---

## Quando usar

Pular trabalho que já está feito: "publique o produto — a menos que já esteja publicado", "feche o lote — a menos que a política diga que não está ativo". O predicado lê o valor da trilha e responde "ainda há algo a fazer?".

Atenção ao alcance: `ThenUnless` protege **um passo**, não o resto da cadeia. O sucesso devolvido continua fluindo — um `.ThenAsync` depois dele roda nos dois ramos. Para pular uma sub-cadeia inteira, componha-a **dentro** do `next`.

## Quando *não* usar

| Você quer…                                            | Use no lugar            |
|-------------------------------------------------------|-------------------------|
| **falhar** quando a condição não vale                 | [`Ensure`](ensure.md)   |
| executar o passo incondicionalmente                   | [`Then`](then.md)       |
| transformar o valor condicionalmente (**mesmo tipo**) | [`ThenWhen`](then-when.md) |
| transformar condicionalmente para um **tipo diferente** | [`Then`](then.md) com o branch dentro da lambda |
| recuperar de uma falha quando uma condição casa       | [`RecoverWhen`](recover.md) |

---

## Operadores

| Método | Assinatura | O que faz |
|---|---|---|
| `ThenUnless` | `(Func<T,bool> predicate, Func<T,AxisResult> next)` | predicado true → pass-through; false → executa `next`, propagando os erros dele |
| `ThenUnlessAsync` | `(Func<T,bool> predicate, Func<T,Task<AxisResult>> next)` | o mesmo, com passo assíncrono |

Definido apenas em `AxisResult<TValue>` — o predicado lê o valor da trilha (entre na trilha com [`Rop()`](getting-started.md) quando partir de um valor cru). O predicado é sempre síncrono: é roteamento barato sobre um valor já em memória; o trabalho fallível vive no `next`. Variantes `Async` existem para `Task`/`ValueTask` e [com `CancellationToken`](cancellation.md), além dos lifts de extensão sobre `Task<AxisResult<T>>`/`ValueTask<AxisResult<T>>`.

---

## Exemplo 1 — escrita idempotente ("já está marcado? não faça nada")

```csharp
return productsReader.GetByIdAsync(productId)                 // AxisResult<IProductEntityProperties>
    .ThenUnlessAsync(
        p => p.IsPublished,                                   // já está publicado → nada a fazer
        p => productsWriter.PublishAsync(p.ProductId))
    .ThenAsync(p => notifier.NotifyPublishedAsync(p.ProductId)); // roda nos DOIS ramos
```

**Por que compensa:** o ternário manual — `p.IsPublished ? AxisResult.Ok().AsTaskAsync() : productsWriter.PublishAsync(...)` — desaparece, e a intenção "pule se já está feito" vira um passo declarativo e nomeado que mantém o valor na trilha.

## Exemplo 2 — guard clause no início do método

```csharp
// ANTES: um early return fora da trilha
// if (!OrderBatchClosurePolicy.IsActive(batch.StatusId))
//     return AxisResult.Ok().AsTaskAsync();

return batch.Rop()
    .ThenUnlessAsync(
        b => !OrderBatchClosurePolicy.IsActive(b.StatusId),   // não ativa → nada a fazer
        b => CloseOrderBatchAsync(b));
```

**Por que compensa:** o `if` de guarda que quebrava o Railway-Oriented Programming vira parte do pipeline — mesmo comportamento, sem saída imperativa.

## Pulando uma sub-cadeia

O sucesso devolvido continua fluindo. Quando a condição deve pular **vários** passos, aninhe-os dentro do `next`:

```csharp
.ThenUnlessAsync(
    p => p.IsPublished,
    p => productsWriter.PublishAsync(p.ProductId)
            .ThenAsync(() => auditWriter.LogPublicationAsync(p.ProductId)))  // sub-cadeia inteira protegida
```

---

## Veja também

- [Encadear · `Then`](then.md) — o passo incondicional
- [Passo condicional · `ThenWhen`](then-when.md) — o espelho: transformação de mesmo tipo, roda quando o predicado é **true**
- [Garantir · `Ensure`](ensure.md) — a guarda inversa: condição falha → a trilha falha
- [Cancelamento](cancellation.md) — os overloads com `CancellationToken`

---

↩ [Voltar aos docs do AxisResult](README.md)
