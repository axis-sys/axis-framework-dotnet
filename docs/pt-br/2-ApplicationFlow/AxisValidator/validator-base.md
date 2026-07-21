# Base e regras do validador · `AxisValidatorBase<T>`

> Um `FluentValidation.AbstractValidator<T>` com quatorze helpers de sabor Axis. Herde, chame os helpers no construtor, e o scanner de assembly do framework descobre automaticamente.

```csharp
public class CreatePersonValidator : AxisValidatorBase<CreatePersonCommand>
{
    public CreatePersonValidator()
    {
        RequiredEmail (x => x.Email,    "PERSON_EMAIL_INVALID");
        RequiredSlug  (x => x.Username, "PERSON_USERNAME_INVALID", length: 32);
        RequiredGuid7 (x => x.TenantId, "TENANT_ID_INVALID");

        NotNullOrEmpty(x => x.Address, "PERSON_ADDRESS_REQUIRED", () =>
        {
            RequiredWithMaxLength(x => x.Address!.Street, "ADDRESS_STREET_INVALID");
        });
    }
}
```

---

## Quando usar

Para todo comando, query e mensagem de integração que tenha um validator. Herde de `AxisValidatorBase<T>` para receber os helpers mais tudo o que `AbstractValidator<T>` oferece — `RuleFor`, `RuleSet`, `When`, `Unless`, regras async, tudo.

## Quando *não* usar

| Você quer… | Use no lugar |
|---|---|
| validar **invariantes de domínio** na borda da entidade | a própria entidade, retornando `AxisError.BusinessRule(...)` |
| validar **com estado do banco** | a camada de aplicação (`Then(...)` + repositório) |
| construir um validator que **não** pluga no framework | um `AbstractValidator<T>` simples (o framework não vai registrar como `IAxisValidator<T>`) |

---

## Os quatorze helpers

| Helper | O que checa | Código de falha |
|---|---|---|
| `NotNullOrEmpty(x => x.Prop, code)` | não null, não default, não só whitespace | `code` |
| `NotNullOrEmpty(x => x.Prop, code, Action dependentRules)` | não null; quando presente, rodar regras FluentValidation aninhadas | `code` (depois códigos aninhados) |
| `NotNullOrEmpty(x => x.Prop, code, Action<TProperty> dependentRules)` | igual, com o valor entregue ao lambda | `code` (depois códigos aninhados) |
| `DependentRules(x => a, code1, x => b, code2, (a, b) => AxisResult)` | ambas as props não-null, depois uma regra `AxisResult` custom | `code1` ou `code2` |
| `RequiredGuid7(x => x.Prop, code)` | não vazio + uma string de UUID v7 válida | `code` |
| `RequiredWithMaxLength(x => x.Prop, code, length = 255)` | não vazio + `.Length <= length` | `code` |
| `RequiredSlug(x => x.Prop, code, length = 255)` | não vazio + somente `[a-zA-Z0-9_-]` + `.Length <= length` | `code` |
| `RequiredEmail(x => x.Prop, code)` | não vazio + email válido | `code` |
| `RequiredTryParse(x => x.Prop, code, parse)` | não vazio + `parse(value)` retorna `true` | `code` |
| `Range(x => x.Prop, code, min, max)` | valor struct dentro de `[min, max]` (ambos os limites opcionais); ignorado quando a propriedade é `null` | `code` |
| `RequiredCollection(x => x.Prop, code)` | coleção não-null e com ao menos um item | `code` |
| `MaxCount(x => x.Prop, code, max)` | coleção com no máximo `max` itens (`null` passa) | `code` |
| `Satisfies(x => x.Prop, code, value => bool)` | predicado custom sobre só o valor da propriedade | `code` |
| `Satisfies(x => x.Prop, code, (instance, value) => bool)` | predicado custom com acesso à instância inteira | `code` |
| `EachSatisfies(x => x.Items, code, item => bool)` | todo item de uma propriedade `IEnumerable<TItem>` satisfaz um predicado | `code` |
| `EachUsesValidator(x => x.Items, itemValidator)` | todo item validado por um `AxisValidatorBase<TItem>` aninhado | os próprios códigos do validator aninhado |
| `UsesValidator(x => x.Prop, validator)` | propriedade de tipo referência validada por um `AxisValidatorBase<TProperty>` aninhado | os próprios códigos do validator aninhado |

> Todo helper falha com **o mesmo `code` para faltando-e-inválido**. O contrato é simples — uma regra, um código. Se você precisa distinguir "ausente" de "inválido", divida em dois `RuleFor` à mão. `EachUsesValidator` e `UsesValidator` são a exceção — eles delegam a um validator aninhado, então as falhas carregam os códigos desse validator.

---

## Regras dependentes — três sabores

### Sabor 1 — `NotNullOrEmpty(prop, code, Action)`

Rode regras aninhadas **somente** quando a propriedade não-null:

```csharp
NotNullOrEmpty(x => x.Address, "PERSON_ADDRESS_REQUIRED", () =>
{
    RequiredWithMaxLength(x => x.Address!.Street, "ADDRESS_STREET_INVALID");
    RequiredWithMaxLength(x => x.Address!.City,   "ADDRESS_CITY_INVALID");
});
```

### Sabor 2 — `NotNullOrEmpty(prop, code, Action<TProperty>)`

Mesma ideia, mas o lambda recebe o valor da propriedade:

```csharp
NotNullOrEmpty(x => x.Address, "PERSON_ADDRESS_REQUIRED", address =>
{
    // address é o valor Address não-null
});
```

### Sabor 3 — `DependentRules(propA, codeA, propB, codeB, (a, b) => AxisResult)`

Duas propriedades precisam ambas ser não-null; depois uma regra custom sobre as duas:

```csharp
DependentRules(
    x => x.From, "TRANSFER_FROM_REQUIRED",
    x => x.To,   "TRANSFER_TO_REQUIRED",
    (from, to) => from != to
        ? AxisResult.Ok()
        : AxisError.ValidationRule("TRANSFER_FROM_AND_TO_MUST_DIFFER"));
```

---

## Exemplo real — um command com objeto aninhado + regra cross-field

```csharp
public class CreateOrderValidator : AxisValidatorBase<CreateOrderCommand>
{
    public CreateOrderValidator()
    {
        RequiredGuid7        (x => x.CustomerId,  "CUSTOMER_ID_INVALID");
        RequiredWithMaxLength(x => x.OrderRef,    "ORDER_REF_INVALID", length: 32);

        NotNullOrEmpty(x => x.Item, "ORDER_ITEM_REQUIRED", () =>
        {
            RequiredGuid7        (x => x.Item!.ProductId, "PRODUCT_ID_INVALID");
            RuleFor              (x => x.Item!.Quantity).GreaterThan(0).WithErrorCode("QUANTITY_INVALID");
        });

        DependentRules(
            x => x.Item, "ORDER_ITEM_REQUIRED",
            x => x.PromoCode, "PROMO_CODE_REQUIRED",
            (item, promo) => promo.StartsWith("VIP-") && item.Quantity < 10
                ? AxisError.ValidationRule("PROMO_VIP_REQUIRES_QTY_GTE_10")
                : AxisResult.Ok());
    }
}
```

**Por que compensa:** o validator lê como um checklist. Helpers cobrem o boilerplate (null-check, empty-check, length-check, format-check) e o `RuleFor` do FluentValidation continua aí para qualquer coisa custom.

---

## Veja também

- [O contrato `IAxisValidator<T>`](iaxisvalidator.md) — o que a base finalmente implementa
- [`ValidationBehavior`](validation-behavior.md) — invocação automática no pipeline do mediator
- [Validadores brasileiros](brazil.md) — `RequiredTryParse(x => x.Cpf, code, CpfValidator.Validate)`
- [Validadores americanos](usa.md) — `RequiredTryParse(x => x.Ssn, code, SsnValidator.Validate)`

---

↩ [Voltar à documentação do AxisValidator](README.md)
