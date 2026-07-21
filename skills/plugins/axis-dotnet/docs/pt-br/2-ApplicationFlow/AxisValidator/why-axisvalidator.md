# Por que AxisValidator? · comparação

> Há outras maneiras de validar input em .NET. Esta página diz por que o AxisValidator é diferente — uma comparação direta, sem mão na cintura.

---

## vs. FluentValidation (direto)

FluentValidation é o substrato. Chamá-lo direto tem duas questões:

1. **Lança.** `validator.ValidateAndThrow(...)` lança `ValidationException`; cada handler precisa de um wrapper para virar `Result` tipado.
2. **Mensagens de erro são o contrato.** `e.ErrorMessage` é uma string assada em tempo de construção — não ótimo para decisões dirigidas por código.

`AxisValidator` projeta falhas do FluentValidation em entradas tipadas de `AxisError.ValidationRule(code)` e retorna `AxisResult`. O **código** do erro é o contrato — mensagens vivem no seu resolver de apresentação.

## vs. `IValidatableObject` / Data Annotations

OK para modelos triviais, dolorido na escala. Sem regras cross-field, sem async, sem DI — e exceções na saída. `AxisValidator` te dá a mesma forma declarativa com a ferrovia, com FluentValidation por baixo.

## vs. validators `Result<T>` à mão

DIY. Mesma forma, mas você redescobre os helpers (`NotNullOrEmpty`, `RequiredSlug`, `RequiredEmail`), a integração com pipeline, a estratégia de localização. `AxisValidatorBase<T>` poupa o custo.

---

## A comparação

| Característica | AxisValidator | FluentValidation direto | `IValidatableObject` | `IValidator` caseiro |
|---|:--:|:--:|:--:|:--:|
| Retorna `AxisResult` | **Sim** | Não | Não | Talvez |
| Pipeline behaviour para enforcement automático | **Sim** | Não | Não | Talvez |
| Helpers testados em batalha (`NotNullOrEmpty`, `RequiredEmail`, `RequiredGuid7`, `RequiredSlug`) | **Sim** | Manual | Não | Talvez |
| Regras dependentes cross-field | **Sim** | Sim | Não | Talvez |
| Packs de localização (CPF/celular brasileiro, SSN/telefone dos EUA) | **Sim** (`AxisValidator.Brazil`, `AxisValidator.Usa`) | Não | Não | Não |
| Async + cancelamento via `IAxisMediator` | **Sim** | Sim | Não | Talvez |
| Código-como-contrato (sem templates de mensagem) | **Sim** | Configurável | Não | Talvez |
| Zero deps NuGet na abstração | **Sim** | Não | n/a | Sim |

---

## Veja também

- [O contrato `IAxisValidator<T>`](iaxisvalidator.md) — a superfície
- [Base e regras do validador](validator-base.md) — os helpers que justificam a abstração
- [`ValidationBehavior`](validation-behavior.md) — invocação automática

---

↩ [Voltar à documentação do AxisValidator](README.md)
