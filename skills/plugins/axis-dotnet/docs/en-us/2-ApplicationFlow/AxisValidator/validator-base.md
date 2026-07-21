# Validator base and rules · `AxisValidatorBase<T>`

> A `FluentValidation.AbstractValidator<T>` with fourteen Axis-flavoured helpers. Inherit it, call helpers in the constructor, and the framework's assembly scanner discovers it automatically.

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

## When to use

For every command, query and integration message that has a validator. Inherit from `AxisValidatorBase<T>` so you get the helpers plus everything `AbstractValidator<T>` offers — `RuleFor`, `RuleSet`, `When`, `Unless`, async rules, all of it.

## When *not* to use

| You want to… | Use instead |
|---|---|
| validate **domain invariants** at the entity boundary | the entity itself, returning `AxisError.BusinessRule(...)` |
| validate **with database state** | the application layer (`Then(...)` + repository) |
| build a validator that does **not** plug into the framework | a plain `AbstractValidator<T>` (the framework will not register it as `IAxisValidator<T>`) |

---

## The fourteen helpers

| Helper | What it checks | Failure code |
|---|---|---|
| `NotNullOrEmpty(x => x.Prop, code)` | not null, not default, not whitespace-only string | `code` |
| `NotNullOrEmpty(x => x.Prop, code, Action dependentRules)` | not null; when present, run nested FluentValidation rules | `code` (then nested codes) |
| `NotNullOrEmpty(x => x.Prop, code, Action<TProperty> dependentRules)` | same, with the value handed to the lambda | `code` (then nested codes) |
| `DependentRules(x => a, code1, x => b, code2, (a, b) => AxisResult)` | both props non-null, then a custom `AxisResult` rule | `code1` or `code2` |
| `RequiredGuid7(x => x.Prop, code)` | not empty + a valid UUID v7 string | `code` |
| `RequiredWithMaxLength(x => x.Prop, code, length = 255)` | not empty + `.Length <= length` | `code` |
| `RequiredSlug(x => x.Prop, code, length = 255)` | not empty + only `[a-zA-Z0-9_-]` + `.Length <= length` | `code` |
| `RequiredEmail(x => x.Prop, code)` | not empty + valid email | `code` |
| `RequiredTryParse(x => x.Prop, code, parse)` | not empty + `parse(value)` returns `true` | `code` |
| `Range(x => x.Prop, code, min, max)` | struct value within `[min, max]` (either bound optional); skipped when the property is `null` | `code` |
| `RequiredCollection(x => x.Prop, code)` | collection is not null and has at least one item | `code` |
| `MaxCount(x => x.Prop, code, max)` | collection has at most `max` items (`null` passes) | `code` |
| `Satisfies(x => x.Prop, code, value => bool)` | custom predicate over just the property's value | `code` |
| `Satisfies(x => x.Prop, code, (instance, value) => bool)` | custom predicate with access to the whole instance | `code` |
| `EachSatisfies(x => x.Items, code, item => bool)` | every item of an `IEnumerable<TItem>` property satisfies a predicate | `code` |
| `EachUsesValidator(x => x.Items, itemValidator)` | every item validated by a nested `AxisValidatorBase<TItem>` | the nested validator's own codes |
| `UsesValidator(x => x.Prop, validator)` | reference-type property validated by a nested `AxisValidatorBase<TProperty>` | the nested validator's own codes |

> Every helper fails with **the same `code` for missing-and-malformed**. The contract is simple — one rule, one code. If you need to distinguish "missing" from "invalid", split into two `RuleFor`s by hand. `EachUsesValidator` and `UsesValidator` are the exception — they delegate to a nested validator, so failures carry that validator's own codes.

---

## Dependent rules — three flavours

### Flavour 1 — `NotNullOrEmpty(prop, code, Action)`

Run nested rules **only** when the property is non-null:

```csharp
NotNullOrEmpty(x => x.Address, "PERSON_ADDRESS_REQUIRED", () =>
{
    RequiredWithMaxLength(x => x.Address!.Street, "ADDRESS_STREET_INVALID");
    RequiredWithMaxLength(x => x.Address!.City,   "ADDRESS_CITY_INVALID");
});
```

### Flavour 2 — `NotNullOrEmpty(prop, code, Action<TProperty>)`

Same idea, but the lambda receives the property's value:

```csharp
NotNullOrEmpty(x => x.Address, "PERSON_ADDRESS_REQUIRED", address =>
{
    // address is the non-null Address value
});
```

### Flavour 3 — `DependentRules(propA, codeA, propB, codeB, (a, b) => AxisResult)`

Two properties must both be non-null; then a custom rule over both:

```csharp
DependentRules(
    x => x.From, "TRANSFER_FROM_REQUIRED",
    x => x.To,   "TRANSFER_TO_REQUIRED",
    (from, to) => from != to
        ? AxisResult.Ok()
        : AxisError.ValidationRule("TRANSFER_FROM_AND_TO_MUST_DIFFER"));
```

---

## Real-world example — a command with nested object + cross-field rule

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

**Why it pays off:** the validator reads like a checklist. Helpers cover the boilerplate (null-check, empty-check, length-check, format-check) and `RuleFor` from FluentValidation is still there for anything custom.

---

## See also

- [The `IAxisValidator<T>` contract](iaxisvalidator.md) — what the base ultimately implements
- [`ValidationBehavior`](validation-behavior.md) — automatic invocation in the mediator pipeline
- [Brazilian validators](brazil.md) — `RequiredTryParse(x => x.Cpf, code, CpfValidator.Validate)`
- [American validators](usa.md) — `RequiredTryParse(x => x.Ssn, code, SsnValidator.Validate)`

---

↩ [Back to AxisValidator docs](README.md)
