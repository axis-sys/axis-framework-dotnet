# Primeiros passos · instalação e uso

> Instale as packages, declare um validator, ligue o behaviour do pipeline — e seus handlers só recebem input válido.

---

## Instalação

```
dotnet add package AxisValidator                        # a abstração + behaviour
dotnet add package AxisValidator.FluentValidation       # a base + adapter
dotnet add package AxisValidator.Brazil                 # opcional: helpers de CPF + celular
dotnet add package AxisValidator.Usa                     # opcional: helpers de SSN + telefone
```

`AxisValidator` depende de `AxisResult` e `AxisMediator.Contracts`. O adapter FluentValidation traz `FluentValidation`.

---

## Registrando

```csharp
using AxisValidator;
using System.Reflection;

builder.Services
    .AddAxisMediator()
    .AddAxisValidator(Assembly.GetExecutingAssembly());
```

`AddAxisValidator(...)` faz quatro coisas:

1. `services.AddValidatorsFromAssemblies(assemblies, includeInternalTypes: true)` — descobre cada `AbstractValidator<T>` nos assemblies dados.
2. Registra `IAxisValidator<>` como scoped, amarrado a `FluentValidatorAdapter<>`.
3. Registra `IAxisPipelineBehavior<>` como transient, amarrado ao open-generic `ValidationBehavior<>` (pipeline void-command).
4. Registra `IAxisPipelineBehavior<,>` como transient, amarrado ao open-generic `ValidationBehavior<,>` (pipeline typed-request).

Se você tem validators em outro assembly, passe também: `AddAxisValidator(typeof(X).Assembly, typeof(Y).Assembly)`.

---

## Declarando um validator

```csharp
using AxisValidator;
using FluentValidation;

public class CreatePersonValidator : AxisValidatorBase<CreatePersonCommand>
{
    public CreatePersonValidator()
    {
        RequiredEmail (x => x.Email,    "PERSON_EMAIL_INVALID");
        RequiredSlug  (x => x.Username, "PERSON_USERNAME_INVALID", length: 32);
        RequiredGuid7 (x => x.TenantId, "TENANT_ID_INVALID");

        // FluentValidation padrão também funciona
        RuleFor(x => x.Age).GreaterThanOrEqualTo(18).WithErrorCode("PERSON_AGE_BELOW_MIN");
    }
}
```

A classe herda de `AxisValidatorBase<T>` (que herda de `FluentValidation.AbstractValidator<T>`), então toda API do FluentValidation continua funcionando.

---

## O que o pipeline faz

```csharp
public Task<AxisResult<CreatePersonResponse>> HandleAsync(CreatePersonCommand cmd)
{
    // quando isso roda, cmd já passou pelo CreatePersonValidator
    return factory.CreateAsync(cmd)
        .ThenAsync(person => writer.CreateAsync(person))
        .MapAsync(_ => new CreatePersonResponse { PersonId = cmd.PersonId });
}
```

Se o request falha validação, o `ValidationBehavior` curto-circuita o pipeline com `Error(errors)` — o handler nunca é chamado. → **[`ValidationBehavior`](validation-behavior.md)**

**Por que compensa:** o handler lê como se o input fosse sempre válido, porque quando ele roda já é. Validação vive uma vez, onde tem que viver.

---

## Veja também

- [O contrato `IAxisValidator<T>`](iaxisvalidator.md) — a superfície
- [Base e regras do validador](validator-base.md) — cada helper em `AxisValidatorBase<T>`
- [`ValidationBehavior` — enforcement no pipeline](validation-behavior.md) — behaviour opt-in do mediator
- [Validadores brasileiros](brazil.md) — CPF, celular, fachada estática `BrazilValidator`
- [Validadores americanos](usa.md) — SSN, telefone, fachada estática `UsaValidator`
- [Por que AxisValidator?](why-axisvalidator.md) — o argumento contra validators que lançam
- [Referência da API](api-reference.md) — cada membro num só lugar

---

↩ [Voltar à documentação do AxisValidator](README.md)
