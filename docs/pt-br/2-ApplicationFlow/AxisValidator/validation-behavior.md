# `ValidationBehavior` — enforcement no pipeline

> Um `IAxisPipelineBehavior` que roda `IAxisValidator<TRequest>.ValidateAsync(request)` antes de cada request do mediator. Em `Failure`, o pipeline curto-circuita com os erros de validação — o handler nunca é invocado.

```csharp
services.AddAxisValidator(Assembly.GetExecutingAssembly());   // registra ValidationBehavior automaticamente
```

---

## Quando usar

Sempre — a menos que você realmente queira chamar o validator por conta própria dentro do handler. O behaviour não custa nada quando nenhum validator está registrado para o tipo do request (`GetService<IAxisValidator<TRequest>>()` retorna `null`, o behaviour chama `next()` direto).

## Quando *não* usar

| Você quer… | Use no lugar |
|---|---|
| validar **só num subconjunto** dos requests | marque os outros sem validator (o behaviour vira no-op) |
| rodar **múltiplos** validators por request | um behaviour custom que compõe |
| validar **depois** de outros behaviours | reordene pipelines no lado do mediator |

---

## O que o behaviour faz

Lendo `ValidationBehavior<TRequest>` e `<TRequest, TResponse>` direto:

```csharp
public async Task<AxisResult> HandleAsync(TRequest request, AxisPipelineContext context, Func<Task<AxisResult>> next)
{
    var validator = serviceProvider.GetService<IAxisValidator<TRequest>>();
    if (validator is null) return await next();          // sem validator → passa direto

    var result = await validator.ValidateAsync(request);
    return result.IsFailure ? result : await next();     // falha → curto-circuito
}
```

O overload `<TRequest, TResponse>` é idêntico, exceto que a falha é convertida de volta para `AxisResult<TResponse>.Error(errors)` via a conversão implícita array → `AxisResult<TResponse>`.

---

## O que é registrado

`AddAxisValidator(...)` (de `DependencyInjection.cs`):

```csharp
services.AddValidatorsFromAssemblies(assemblies, includeInternalTypes: true);
services.AddScoped(typeof(IAxisValidator<>), typeof(FluentValidatorAdapter<>));
services.AddTransient(typeof(IAxisPipelineBehavior<>), typeof(ValidationBehavior<>));
services.AddTransient(typeof(IAxisPipelineBehavior<,>), typeof(ValidationBehavior<,>));
```

- FluentValidation descobre seu `AbstractValidator<T>` (e `AxisValidatorBase<T>`) nos assemblies dados.
- `IAxisValidator<>` resolve para `FluentValidatorAdapter<>` (scoped, então consegue ler `IAxisMediator.CancellationToken`).
- Ambos os validation behaviours são registrados como open-generic transients para o pipeline do mediator.

---

## Exemplo real — tratamento de falha silenciosa na borda

```csharp
public Task<AxisResult<CreatePersonResponse>> HandleAsync(CreatePersonCommand cmd)
{
    // cmd já está válido aqui — o behaviour fez o gating.
    return factory.CreateAsync(cmd)
        .ThenAsync(person => writer.CreateAsync(person))
        .MapAsync(_ => new CreatePersonResponse { PersonId = cmd.PersonId });
}
```

```csharp
// Na borda HTTP (seu controller / Gateway)
return await mediator.Cqrs.ExecuteAsync<CreatePersonCommand, CreatePersonResponse>(cmd)
    .Match(
        onSuccess: r      => Results.Ok(r),
        onFailure: errors => Results.Problem(
            title: "VALIDATION_FAILED",
            detail: string.Join(",", errors.Select(e => e.Code))));
```

**Por que compensa:** o handler nunca re-valida. A borda HTTP vê os erros `ValidationRule` tipados e mapeia para 400 — sem `try/catch (ValidationException)` em lugar nenhum.

---

## Veja também

- [O contrato `IAxisValidator<T>`](iaxisvalidator.md) — o que o behaviour chama
- [Base e regras do validador](validator-base.md) — como seu validator parece
- [Validadores brasileiros](brazil.md) — o pack de localização que pluga no mesmo pipeline
- [Validadores americanos](usa.md) — a mesma ideia, para SSN e telefone dos EUA

---

↩ [Voltar à documentação do AxisValidator](README.md)
