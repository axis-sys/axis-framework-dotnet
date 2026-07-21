# Primeiros passos · instalação e uso

> Instale o pacote, crie resultados, inspecione-os e encadeie a primeira operação — o mínimo para sair do zero em poucos minutos.

---

## Instalação

```
dotnet add package AxisResult
```

Sem dependências externas, ~240 KB. Funciona em qualquer projeto .NET moderno.

---

## Criar resultados

```csharp
// Sucesso
AxisResult success = AxisResult.Ok();
AxisResult<int> typed = AxisResult.Ok(42);

// Falha
AxisResult failure = AxisError.NotFound("USER_NOT_FOUND");     // conversão implícita
AxisResult<int> typedFail = AxisError.BusinessRule("INSUFFICIENT_STOCK");

// A partir de valores (implícito)
AxisResult<string> name = "John";   // embrulha em Ok automaticamente

// A partir de exceções (borda segura)
AxisResult result = AxisResult.Try(() => riskyOperation());
AxisResult<int> parsed = AxisResult.Try(() => int.Parse(input));
```

> `AxisResult.Try` é para a **borda** de infraestrutura — veja [Exceções na borda · `Try`](boundary-and-try.md).

---

## Inspecionar resultados

```csharp
if (result.IsSuccess)
    Console.WriteLine($"Value: {result.Value}");

if (result.IsFailure)
    Console.WriteLine($"Errors: {result.JoinErrorCodes()}");

// Pattern matching
var message = result.Match(
    onSuccess: value  => $"Got {value}",
    onFailure: errors => $"Failed: {errors[0].Code}");
```

> Prefira [`Match`](match.md) ou a desestruturação segura a acessar `.Value` direto — `.Value` lança numa falha.

---

## Encadear operações

```csharp
// Cada passo só roda se o anterior teve sucesso
var result = await GetUserAsync(userId)
    .ThenAsync(user => ValidateOrder(user, productId))
    .ThenAsync(order => CalculateTotal(order))
    .MapAsync(total => new OrderResponse { Total = total });
```

**Por que compensa:** o `await` único no início e o encadeamento fluente substituem uma escada de `if/return` — e qualquer falha curto-circuita o resto sem `try/catch`.

---

## Veja também

- [Railway-Oriented Programming](railway-oriented-programming.md) — o porquê do modelo, em 5 minutos
- [Encadear · `Then`](then.md) — o coração do pipeline
- [Transformar · `Map`](map.md) — a transformação que não pode falhar
- [Referência da API](api-reference.md) — todos os métodos numa tabela

---

↩ [Voltar à documentação do AxisResult](README.md)
