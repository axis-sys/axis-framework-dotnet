# Referência da API

> O catálogo completo, agrupado por responsabilidade. Use para consulta — cada grupo linka de volta à sua página de detalhe.

---

## O contrato — `IAxisValidator<T>`

| Método | Assinatura | Descrição |
|---|---|---|
| `Validate` | `AxisResult Validate(T instance)` | validação síncrona |
| `ValidateAsync` | `Task<AxisResult> ValidateAsync(T instance)` | validação async (honra `IAxisMediator.CancellationToken` no adapter embarcado) |

→ [O contrato `IAxisValidator<T>`](iaxisvalidator.md)

---

## Base — `AxisValidatorBase<T>` (adapter FluentValidation)

| Helper | Assinatura | Descrição |
|---|---|---|
| `NotNullOrEmpty` | `void NotNullOrEmpty<TProperty>(Expression<Func<T, TProperty?>>, string errorCode)` | não null, não default, não whitespace |
| `NotNullOrEmpty` | `void NotNullOrEmpty<TProperty>(Expression<Func<T, TProperty?>>, string errorCode, Action dependentRules)` | + regras aninhadas quando presente |
| `NotNullOrEmpty` | `void NotNullOrEmpty<TProperty>(Expression<Func<T, TProperty?>>, string errorCode, Action<TProperty> dependentRules)` | + regras aninhadas sobre o valor tipado |
| `DependentRules` | `void DependentRules<T1, T2>(Expression<Func<T, T1?>>, string codeA, Expression<Func<T, T2?>>, string codeB, Func<T1, T2, AxisResult> rule)` | regra cross-field retornando `AxisResult` |
| `RequiredGuid7` | `void RequiredGuid7(Expression<Func<T, string?>>, string errorCode)` | não vazio + string UUID v7 válida |
| `RequiredWithMaxLength` | `void RequiredWithMaxLength(Expression<Func<T, string?>>, string errorCode, int? length = 255)` | não vazio + restrição de tamanho |
| `RequiredSlug` | `void RequiredSlug(Expression<Func<T, string?>>, string errorCode, int? length = 255)` | não vazio + `[a-zA-Z0-9_-]` + tamanho |
| `RequiredEmail` | `void RequiredEmail(Expression<Func<T, string?>>, string errorCode)` | não vazio + email válido |
| `RequiredTryParse` | `void RequiredTryParse(Expression<Func<T, string?>>, string errorCode, Func<object?, bool> parse)` | não vazio + parser custom |
| `Range` | `void Range<TValue>(Expression<Func<T, TValue?>>, string errorCode, TValue? min = null, TValue? max = null) where TValue : struct, IComparable<TValue>` | valor struct dentro de `[min, max]`; ignorado quando `null` |
| `RequiredCollection` | `void RequiredCollection<TProperty>(Expression<Func<T, TProperty?>>, string errorCode) where TProperty : IEnumerable` | coleção não-null e com ao menos um item |
| `MaxCount` | `void MaxCount<TProperty>(Expression<Func<T, TProperty?>>, string errorCode, int max) where TProperty : IEnumerable` | coleção com no máximo `max` itens (`null` passa) |
| `Satisfies` | `void Satisfies<TProperty>(Expression<Func<T, TProperty?>>, string errorCode, Func<TProperty?, bool> predicate)` | predicado custom sobre o valor da propriedade |
| `Satisfies` | `void Satisfies<TProperty>(Expression<Func<T, TProperty?>>, string errorCode, Func<T, TProperty?, bool> predicate)` | predicado custom com acesso à instância inteira |
| `EachSatisfies` | `void EachSatisfies<TItem>(Expression<Func<T, IEnumerable<TItem>?>>, string errorCode, Func<TItem?, bool> predicate)` | todo item da coleção satisfaz um predicado |
| `EachUsesValidator` | `void EachUsesValidator<TItem>(Expression<Func<T, IEnumerable<TItem>?>>, AxisValidatorBase<TItem> itemValidator)` | todo item validado por um validator aninhado |
| `UsesValidator` | `void UsesValidator<TProperty>(Expression<Func<T, TProperty?>>, AxisValidatorBase<TProperty> validator) where TProperty : class` | propriedade de tipo referência validada por um validator aninhado |

Constante `DefaultMaxLength = 255`.

→ [Base e regras do validador](validator-base.md)

---

## Pipeline behaviour — `ValidationBehavior`

| Tipo | Onde se senta | O que faz |
|---|---|---|
| `ValidationBehavior<TRequest>` | requests sem response | resolve `IAxisValidator<TRequest>`; se presente e `Failure`, curto-circuita com os erros de validação |
| `ValidationBehavior<TRequest, TResponse>` | requests com response tipada | mesmo, mas retorna `AxisResult<TResponse>.Error(errors)` em `Failure` |

→ [`ValidationBehavior`](validation-behavior.md)

---

## Extensões de DI (extension members de C# 14 em `IServiceCollection`)

| Extensão | Efeito |
|---|---|
| `services.AddAxisValidator(params Assembly[] assemblies)` | descobre `AbstractValidator<T>` em `assemblies`, amarra `IAxisValidator<>` → `FluentValidatorAdapter<>` (scoped), registra `ValidationBehavior<>` e `ValidationBehavior<,>` como transient `IAxisPipelineBehavior` |

---

## Validadores brasileiros — `AxisValidator.Brazil`

| Membro | Assinatura | Descrição |
|---|---|---|
| `CpfValidator.Validate` | `static bool Validate(string? cpf)` | validação check-digit de um CPF |
| `CellphoneValidator.Format` | `static string? Format(string? cellphone)` | canônico `"(DD) 9NNNN-NNNN"` ou `null` |
| `CellphoneValidator.OnlyNumbers` | `static string? OnlyNumbers(string? cellphone)` | normalização só-dígitos ou `null` |
| `CellphoneValidator.TryFormat` | `static bool TryFormat(string? cellphone, out string? formatted)` | companheiro não-lançante |
| `BrazilValidator.FormatCellphone` | `static AxisResult<string> FormatCellphone(string? phone)` | `Ok(formatted)` ou `ValidationRule("CELLPHONE_NUMBER_NULL_OR_NOT_VALID")` |
| `BrazilValidator.ValidateCpf` | `static AxisResult<string> ValidateCpf(string? document)` | `Ok(document)` ou `ValidationRule("DOCUMENT_INVALID")` |
| `RandomBrazilianDataHelper.GenerateCpf` | `static string GenerateCpf(bool format = false)` | CPF aleatório válido para testes |

→ [Validadores brasileiros](brazil.md)

---

## Validadores americanos — `AxisValidator.Usa`

| Membro | Assinatura | Descrição |
|---|---|---|
| `SsnValidator.Validate` | `static bool Validate(string? ssn)` | validação estrutural de um SSN (não existe algoritmo público de dígito verificador) |
| `PhoneValidator.Format` | `static string? Format(string? phone)` | canônico `"(NPA) NXX-XXXX"` ou `null` |
| `PhoneValidator.OnlyNumbers` | `static string? OnlyNumbers(string? phone)` | normalização só-dígitos ou `null` |
| `PhoneValidator.TryFormat` | `static bool TryFormat(string? phone, out string? formatted)` | companheiro não-lançante |
| `UsaValidator.FormatPhone` | `static AxisResult<string> FormatPhone(string? phone)` | `Ok(formatted)` ou `ValidationRule("PHONE_NUMBER_NULL_OR_NOT_VALID")` |
| `UsaValidator.ValidateSsn` | `static AxisResult<string> ValidateSsn(string? document)` | `Ok(document)` ou `ValidationRule("DOCUMENT_INVALID")` |
| `RandomUsaDataHelper.GenerateSsn` | `static string GenerateSsn(bool format = false)` | SSN aleatório estruturalmente válido para testes |

→ [Validadores americanos](usa.md)

---

## Contrato de comportamento (para adapters)

| Cenário | `AxisResult` retornado |
|---|---|
| nenhum validator registrado | `Ok()` |
| validator diz válido | `Ok()` |
| validator diz inválido | `Error(errors)` onde cada erro é `AxisError.ValidationRule(code)` (o `ErrorCode` do FluentValidation) |

→ [O contrato `IAxisValidator<T>`](iaxisvalidator.md)

---

## Veja também

- [Primeiros passos](getting-started.md) — instale, registre, valide
- [Por que AxisValidator?](why-axisvalidator.md) — o argumento pela abstração
- [Documentação completa](README.md) — o mapa de toda a documentação

---

↩ [Voltar à documentação do AxisValidator](README.md)
