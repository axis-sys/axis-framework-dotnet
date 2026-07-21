# AxisValidator — Documentação

> 🌐 [English (README principal)](../../../en-us/2-ApplicationFlow/AxisValidator/README.md)

**Validação declarativa que retorna `AxisResult`** — `IAxisValidator<T>` com `Validate` / `ValidateAsync`, um adapter `FluentValidation` embarcado, um `AxisValidatorBase<T>` com helpers de regra batidos em produção (`NotNullOrEmpty`, `RequiredEmail`, `RequiredSlug`, `RequiredGuid7`, regras dependentes), um behaviour opt-in do pipeline do mediator que curto-circuita em `Failure`, uma package `AxisValidator.Brazil` para validação de CPF / celular brasileiro, e uma package `AxisValidator.Usa` para validação de SSN / telefone dos EUA.

```csharp
public class CreatePersonValidator : AxisValidatorBase<CreatePersonCommand>
{
    public CreatePersonValidator()
    {
        RequiredEmail (x => x.Email,    "PERSON_EMAIL_INVALID");
        RequiredSlug  (x => x.Username, "PERSON_USERNAME_INVALID", length: 32);
        RequiredGuid7 (x => x.TenantId, "TENANT_ID_INVALID");
    }
}
```

Use esta página como **mapa**: leia o tronco abaixo (~5 min) e salte direto para o detalhe do grupo que você precisa — sem ler centenas de linhas.

---

## O tronco (leia primeiro)

### A interface em 60 segundos

```csharp
public interface IAxisValidator<in T>
{
    AxisResult       Validate(T instance);
    Task<AxisResult> ValidateAsync(T instance);
}
```

Um sucesso retorna `AxisResult.Ok()`. Uma falha retorna `AxisResult.Error(errors)` onde cada erro tem `Type = AxisErrorType.ValidationRule` e o `Code` é o error code da regra. → **[O contrato `IAxisValidator<T>`](iaxisvalidator.md)**

### A base — `AxisValidatorBase<T>`

Um `FluentValidation.AbstractValidator<T>` com helpers de sabor `Axis`. Você herda, chama os helpers no construtor, e o framework descobre seu validator via scan de assembly. → **[Base e regras do validador](validator-base.md)**

### `ValidationBehavior` — enforcement no pipeline

Um `IAxisPipelineBehavior` opt-in que roda o validator (se houver) antes de cada request do mediator. Em `Failure`, o pipeline curto-circuita com os erros de validação — o handler nunca executa. → **[`ValidationBehavior` — enforcement no pipeline](validation-behavior.md)**

### Localização brasileira — `AxisValidator.Brazil`

CPF, celular brasileiro, mais a fachada estática `BrazilValidator` para "formate este celular" e "valide este CPF". → **[Validadores brasileiros](brazil.md)**

### Localização americana — `AxisValidator.Usa`

SSN, telefone dos EUA (NANP), mais a fachada estática `UsaValidator` para "formate este telefone" e "valide este SSN". → **[Validadores americanos](usa.md)**

### Instalação

```
dotnet add package AxisValidator                        # a abstração + behaviour
dotnet add package AxisValidator.FluentValidation       # a base AbstractValidator + adapter
dotnet add package AxisValidator.Brazil                 # helpers de CPF + celular brasileiro
dotnet add package AxisValidator.Usa                     # helpers de SSN + telefone dos EUA
```

→ Guia completo: **[Primeiros passos](getting-started.md)**

---

## O mapa (salte para o que precisa)

| Grupo | Você quer… | Detalhe |
|---|---|---|
| **Contrato · `IAxisValidator<T>`** | rodar um validator e obter um `AxisResult` | [iaxisvalidator.md](iaxisvalidator.md) |
| **Base · `AxisValidatorBase<T>`** ⭐ | declarar um validator com helpers `Axis` | [validator-base.md](validator-base.md) |
| **Pipeline · `ValidationBehavior`** | enforce validação antes de cada handler | [validation-behavior.md](validation-behavior.md) |
| **Brasil · `AxisValidator.Brazil`** | CPF, celular, extensões de país | [brazil.md](brazil.md) |
| **EUA · `AxisValidator.Usa`** | SSN, telefone, extensões de país | [usa.md](usa.md) |
| **Por quê?** | o argumento por validação que retorna `Result` | [why-axisvalidator.md](why-axisvalidator.md) |
| **Referência** | cada membro num só lugar | [api-reference.md](api-reference.md) |

**Comece aqui:** [Primeiros passos](getting-started.md) · [O contrato `IAxisValidator<T>`](iaxisvalidator.md) · [Por que AxisValidator?](why-axisvalidator.md)

**Fundamentos:** [Base e regras do validador](validator-base.md) · [`ValidationBehavior`](validation-behavior.md) · [Validadores brasileiros](brazil.md) · [Validadores americanos](usa.md)

**Referência e extras:** [Referência da API](api-reference.md)

---

## Princípios de design

1. **Validação retorna `AxisResult`.** Uma regra falha é um valor na trilha, não uma exceção no handler.
2. **Um código de erro = uma regra.** Cada regra falha com um `Code` estável (`"PERSON_EMAIL_INVALID"`). Sem prosa, sem templates de mensagem vazando no contrato.
3. **Enforcement no nível do pipeline.** Registre o behaviour e o handler para de se preocupar com input — quando ele rodar, o request já é válido.
4. **FluentValidation por baixo.** Biblioteca testada em batalha; o adapter traduz falhas em entradas tipadas de `AxisError.ValidationRule(code)`.
5. **Localização é uma package separada.** Regras específicas de país (CPF, celular brasileiro; SSN, telefone dos EUA) vivem em `AxisValidator.Brazil` e `AxisValidator.Usa` para o core ficar neutro.

---

## Licença

Apache 2.0
