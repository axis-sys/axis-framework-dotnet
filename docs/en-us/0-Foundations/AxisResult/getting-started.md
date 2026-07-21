# Getting started · installation and usage

> Install the package, create results, inspect them and chain the first operation — the minimum to get off the ground in a few minutes.

---

## Installation

```
dotnet add package AxisResult
```

No external dependencies, ~240 KB. Works in any modern .NET project.

---

## Creating results

```csharp
// Success
AxisResult success = AxisResult.Ok();
AxisResult<int> typed = AxisResult.Ok(42);

// Failure
AxisResult failure = AxisError.NotFound("USER_NOT_FOUND");     // implicit conversion
AxisResult<int> typedFail = AxisError.BusinessRule("INSUFFICIENT_STOCK");

// From values (implicit)
AxisResult<string> name = "John";   // auto-wraps in Ok

// From exceptions (safe boundary)
AxisResult result = AxisResult.Try(() => riskyOperation());
AxisResult<int> parsed = AxisResult.Try(() => int.Parse(input));
```

> `AxisResult.Try` is for the infrastructure **boundary** — see [Exceptions at the boundary · `Try`](boundary-and-try.md).

---

## Inspecting results

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

> Prefer [`Match`](match.md) or safe deconstruction to accessing `.Value` directly — `.Value` throws on a failure.

---

## Chaining operations

```csharp
// Each step only runs if the previous one succeeded
var result = await GetUserAsync(userId)
    .ThenAsync(user => ValidateOrder(user, productId))
    .ThenAsync(order => CalculateTotal(order))
    .MapAsync(total => new OrderResponse { Total = total });
```

**Why it pays off:** the single `await` at the start and the fluent chaining replace a ladder of `if/return` — and any failure short-circuits the rest with no `try/catch`.

---

## See also

- [Railway-Oriented Programming](railway-oriented-programming.md) — the why behind the model, in 5 minutes
- [Chain · `Then`](then.md) — the heart of the pipeline
- [Transform · `Map`](map.md) — the transformation that cannot fail
- [API reference](api-reference.md) — every method in one table

---

↩ [Back to AxisResult docs](README.md)
