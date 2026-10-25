# Domain

An Axis domain is deliberately thin. An aggregate's **state** is a public read-only interface backed by an
internal record, its **ids and slugs** are typed value objects, an N1 aggregate is **created** by an
application-layer factory, and it is **persisted** through Reader and Write ports that return `AxisResult`.
Behavior is added to an aggregate only when a real business rule asks for it.

This article covers what you write in `{App}.SharedKernel`, `{App}.Domain` and the `Aggregates/` folders of
`{App}.Application`. The ports and their adapters are in [Persistence](persistence.md); the project topology is in
[Architecture](architecture.md); naming and the `{Aggregate}Errors` classes are in [Style](style.md).

| Section | What it settles |
|---|---|
| [Where the pieces live](#where-the-pieces-live) | which project holds which part of an aggregate |
| [Aggregate levels](#aggregate-levels) | N0 lookup, N1 CRUD, N2 behavior-rich, and when to upgrade |
| [The Properties pair](#the-properties-pair) | a public interface plus an internal sealed record as the state contract |
| [Aggregate factories](#aggregate-factories) | `{Aggregate}Factory` with a nested `NewArgs`: how an N1 aggregate is created |
| [Codes instead of enums](#codes-instead-of-enums) | `{X}Codes` const-string catalogs and the layer each one lives in |
| [Value objects](#value-objects) | `[ValueObject]` types, the only place in the core that throws |
| [Checklist: a new aggregate](#checklist-a-new-aggregate) | the files to add, in order |

---

## Where the pieces live

Every project is foldered by bounded context, then aggregate: `Catalog/Products/` exists in the domain, in the
driven contracts and in the repository adapter alike.

| Piece | Project | In the reference application |
|---|---|---|
| Value objects, code catalogs shared across layers | `{App}.SharedKernel` | [`ProductId`][product-id], [`Sku`][sku], [`PermissionCodes`][permission-codes] |
| Properties interface and record, N2 entities, code catalogs local to a bounded context | `{App}.Domain` | [`IProductEntityProperties`][iproduct-properties], [`ProductProperties`][product-properties] |
| Factories, use cases, application services | `{App}.Application` | [`ProductFactory`][product-factory] |
| Reader and Write ports, `{Aggregate}Errors`, `IUnitOfWork` | `{App}.Contracts.Driven` | [`IProductsReaderPort`][iproducts-reader], [`IProductsWritePort`][iproducts-write], [`ProductErrors`][product-errors] |

---

## Aggregate levels

Every aggregate sits at exactly one of three levels. Start at the lowest level that fits and climb only when the
code forces it.

| Level | Use it for | What exists |
|---|---|---|
| **N0 · lookup** | seeded, read-only reference data | a public `I{Entity}Properties` interface and an `I{Entities}ReaderPort`; no factory and no write port |
| **N1 · CRUD** | state that is created and changed but guards no decision of its own | the [Properties pair](#the-properties-pair), Reader and Write ports, and an internal `{Aggregate}Factory` in the application layer ([factories](#aggregate-factories)) |
| **N2 · behavior-rich** | an aggregate that guards a decision on its own state | a domain entity carrying its rules in `{App}.Domain`, plus an `AggregateFactory` and an `AggregateApplication` in the application layer |

**The upgrade trigger.** The first business rule that guards a decision on an aggregate's state ("may this
happen, given what the aggregate holds now?") is the moment to introduce that aggregate's N2 shape, in the same
change. Two mistakes sit on either side of that line:

| Don't | Why |
|---|---|
| build the N2 chain before a rule forces it | N0 and N1 are the normal resting levels; an empty entity is ceremony |
| hide the first rule in a handler, a factory or a policy to avoid the upgrade | a rule that lives outside its aggregate is a rule the next use case forgets |

Two kinds of logic never become entity methods:

| Logic | Where it lives |
|---|---|
| pure, stateless computation that belongs to no single aggregate | an `internal static` domain service in `{App}.Domain` |
| a cross-cutting rule that guards a decision across aggregates | an internal policy or service in the application layer: an interface plus its implementation, the same shape as the reference application's [`IOrderPricingService`][iorder-pricing] and [`OrderPricingService`][order-pricing] |

**In the reference application** all six aggregates (Products, Orders, CartItems, Customers, ValidationCodes and
Members) are N1; none has reached N2. Its one guard on aggregate state, the stock check of the checkout, is an
`Ensure` step of [`CheckoutProductHandler`][checkout-handler]:

```csharp
.EnsureAsync(product => product.Stock >= command.Quantity, ProductErrors.InsufficientStock)
```

In your own application, a guard like this one is the signal to give the aggregate its N2 shape. That shape is
decided when the first N2 aggregate appears; the one constraint fixed upfront is the entity's accessibility (see
the [exceptions](#exceptions) below).

---

## The Properties pair

Every persisted entity is two types in the same folder and namespace of `{App}.Domain`:

- a **public read-only interface**: `I{Entity}EntityProperties` for aggregates and child entities, or
  `I{Entity}Properties` for an N0 lookup, exposing get-only properties;
- an **`internal sealed` positional record**, `{Entity}Properties`, implementing it with the same properties in
  the same order.

[`IProductEntityProperties`][iproduct-properties] and [`ProductProperties`][product-properties]:

```csharp
public interface IProductEntityProperties
{
    ProductId ProductId { get; }
    Sku Sku { get; }
    string Name { get; }
    int Stock { get; }
}

internal sealed record ProductProperties(
    ProductId ProductId,
    Sku Sku,
    string Name,
    int Stock
) : IProductEntityProperties;
```

**Why.** Ports, factories and use cases depend only on the interface. The concrete record can be built in exactly
two places: the application layer, through `InternalsVisibleTo`, and the repository's `{Entity}DbEntity`, which
implements the same interface when it maps a row (see [row mapping](persistence.md#row-mapping)). No other layer
can construct domain state and bypass the factory or the port contract. The domain project
([`Scaffolds.ECommerce.Domain.csproj`][domain-csproj]) opens its internals to two projects only:

```xml
<ItemGroup>
  <InternalsVisibleTo Include="Scaffolds.ECommerce.Application" />
  <InternalsVisibleTo Include="Scaffolds.ECommerce.UnitTests" />
</ItemGroup>
```

Checklist:

- [ ] Ids and slugs are value objects, everything else is a BCL primitive, and an optional value object is a
      nullable value-object type (`CustomerId?`). [`OrderProperties`][order-properties] types all of its ids as
      value objects; [`ICustomerEntityProperties`][icustomer-properties] shows optional primitives (`string?`).
- [ ] The record is the state contract only: no methods, no validation. A rule belongs to the aggregate's N2
      entity.
- [ ] The pair sits directly in the aggregate folder, `{BC}/{Aggregate}/`, with no `Properties/` subfolder. A
      child entity lives in a nested folder under its aggregate root's folder.
- [ ] `InternalsVisibleTo` names only the application project and the core unit-test project.

| Do | Don't |
|---|---|
| `internal sealed record ProductProperties(...) : IProductEntityProperties` | a `public` record, or one in a `Properties/` subfolder |
| ports return `AxisResult<IProductEntityProperties>` | ports return the record or the `DbEntity` |
| `ProductId ProductId { get; }` | `string ProductId { get; }` or a `Guid` id |
| validate in a value object or a validator | validate or compute inside the Properties record |

The conventions analyzers (`Axis.Conventions.Analyzers`) report a concrete properties record that is not
`internal sealed` ([`AXIS0604`](analyzers.md#axis0604)).

### Exceptions

- Unit tests cannot build the internal record, so they declare a `Fake{Entity}` record implementing the interface,
  such as [`FakeProduct`][fake-product] (see [Testing](testing.md)).
- An N2 domain entity stays `internal`, never `public`, but may be non-sealed when it declares at least one
  `protected` member: the rules that the application-layer `AggregateApplication` subclass inherits. `protected`
  is the only accessibility a subclass in another assembly can reach.

---

## Aggregate factories

An N1 aggregate is created by an application-layer factory, never by building its record inside a handler.

| Part | Convention |
|---|---|
| Location | `{BC}/{Aggregate}/Aggregates/{Aggregate}Factory.cs` in `{App}.Application`, interface and implementation in the same file. A single-aggregate bounded context drops the aggregate segment: [`Customers/Aggregates/CustomerFactory.cs`][customer-factory]. A multi-aggregate one gives each aggregate its own folder: [`Catalog/Products/Aggregates/`][product-factory], [`Catalog/Orders/Aggregates/`][order-factory], [`Catalog/CartItems/Aggregates/`][cart-item-factory] |
| Interface | `internal interface I{Aggregate}Factory` nesting a `sealed record NewArgs` with `required` init properties, and declaring `Task<AxisResult<I{Aggregate}EntityProperties>> CreateAsync(NewArgs args)` |
| Implementation | `internal sealed class {Aggregate}Factory`, injecting reader and write ports |
| Preconditions | every referenced aggregate is read through its reader port before anything is written; a miss stops the railway with that aggregate's not-found code from its `{Aggregate}Errors` |
| Identity | a new id comes from the value-object factory (`OrderId.New`); a generated business key comes from a private static helper |
| Write | the new Properties record is staged through the write port, in the same railway as the checks |
| Commit | never: no `IUnitOfWork`, no `SaveChangesAsync`; the handler that called the factory owns the commit |
| Registration | explicit and scoped, in the bounded context's application DI extension ([`Catalog/DependencyInjection.cs`][catalog-app-di]) |

[`OrderFactory`][order-factory] checks the cart item it refers to, builds the order with a fresh `OrderId`, and
stages it:

```csharp
internal interface IOrderFactory
{
    sealed record NewArgs
    {
        public required CustomerId CustomerId { get; init; }
        public required CartId CartId { get; init; }
        public required int Quantity { get; init; }
    }

    Task<AxisResult<IOrderEntityProperties>> CreateAsync(NewArgs args);
}

internal sealed class OrderFactory(
    ICartItemsReaderPort cartItems,
    IOrdersWritePort orders
) : IOrderFactory
{
    public Task<AxisResult<IOrderEntityProperties>> CreateAsync(IOrderFactory.NewArgs args)
        => cartItems.GetByCartIdAsync(args.CartId)
            .MapAsync(IOrderEntityProperties (cartItem) => new OrderProperties(
                OrderId.New,
                args.CustomerId,
                cartItem.ProductId,
                args.Quantity,
                cartItem.CartId))
            .ThenAsync(orders.CreateAsync);
}
```

The handler composes the factory into its own railway and commits once, at the tail
([`CreateProductHandler`][create-product-handler]):

```csharp
return productsReader
    .GetBySkuAsync(sku)
    .RequireNotFoundAsync(ProductErrors.SkuAlreadyExists)
    .ThenAsync(() => productFactory.CreateAsync(new IProductFactory.NewArgs
    {
        Sku = sku,
        Name = command.Name!,
        InitialStock = command.InitialStock!.Value,
    }))
    .ThenAsync(_ => unitOfWork.SaveChangesAsync())
    .MapAsync(product => new CreateProductResponse { ProductId = product.ProductId });
```

**Why.** The precondition checks and the write stay inside one `AxisResult` chain, and the single
`SaveChangesAsync` of the handler covers everything the factory staged (see
[unit of work](persistence.md#unit-of-work)). `RequireNotFound` is documented in
[AxisResult · Ensure](https://github.com/axis-sys/axis-result/blob/main/docs/ensure.md).

**Create-or-replace.** When the key already exists and the write must replace it, the factory recovers the
declared conflict of `CreateAsync` with an update. [`ValidationCodeFactory`][validation-code-factory] also shows
a generated business key produced by a private static helper:

```csharp
public Task<AxisResult<IValidationCodeEntityProperties>> CreateAsync(IValidationCodeFactory.NewArgs args)
    => CreateOrReplaceAsync(new ValidationCodeProperties(args.CustomerId, NewCode()));

private Task<AxisResult<IValidationCodeEntityProperties>> CreateOrReplaceAsync(IValidationCodeEntityProperties validationCode)
    => validationCodes
        .CreateAsync(validationCode)
        .RecoverConflictAsync(() => validationCodes.UpdateAsync(validationCode));

private static string NewCode() => RandomNumberGenerator.GetHexString(8, lowercase: true);
```

[`MemberFactory`][member-factory] uses the same create-or-replace; why it stays safe under concurrent writers on
every provider is covered in [concurrency traps](persistence.md#concurrency-traps).

**Exceptions.**

- An aggregate created only through a saga may build its Properties inside the saga's stage handlers or services
  instead of a dedicated factory class.
- A multi-aggregate write plan inside a saga uses a static builder class, because it composes several aggregates
  at once.
- At N2, creation moves to the domain-side `AggregateFactory` and `AggregateApplication`; this application-layer
  factory is the N1 seam only.

---

## Codes instead of enums

Seeded and workflow lookup values (statuses, event types, item types, data types, permission codes) are
const-string catalogs in a `public static class {X}Codes` (or `{X}Types`, `{X}Kinds`). An enum is never used for
a seeded, lookup or status value anywhere in the domain, the kernel or the contracts. A code is a contract value:
the same English string is seeded in the database, carried by the API and the bus, and matched by every layer.

[`PermissionCodes`][permission-codes]:

```csharp
public static class PermissionCodes
{
    public const string CatalogRegisterProducts = "catalog.register-products";
    public const string ValidateEmailRuns = "email-validation.validate-email-runs";

    public static IReadOnlyList<string> All { get; } = [CatalogRegisterProducts, ValidateEmailRuns];
}
```

The codes themselves are always `public const string`. Beside them, a catalog may expose an accessor over its
constants: `PermissionCodes.All` lists every code for the validator, and a value-object-typed accessor over one
constant is also fine.

Put each catalog in the **lowest layer that needs it**, so projects that only need to name a value never take a
reference on the domain project:

| The code is used by | It lives in | Example |
|---|---|---|
| one bounded context only | the domain project, next to its aggregate: `{BC}/{Aggregate}/{X}Codes.cs` | |
| several layers (contracts, adapters, host) | the kernel | [`PermissionCodes`][permission-codes] (application validators and host controllers), [`AuthRoles`][auth-roles] |
| an infrastructure concept | the driven infra contracts, next to the descriptor it classifies | |

- Code values are stable English strings. An existing value is never renamed: it is already stored and
  transmitted.
- Error codes follow a sibling convention, one `{Aggregate}Errors` class beside the driven ports
  ([Style](style.md)); this section covers lookup and status catalogs only.
- An enum remains fine for something that is not a domain value, such as the host-only `DatabaseProvider`
  switch.

---

## Value objects

Every value object is a `[ValueObject] public readonly partial record struct` in the kernel, with a
`private {Name}(string? value)` constructor that validates and throws `ArgumentException`, and a
`private string Value { get; }`. The source generator of [AxisTypes](https://github.com/axis-sys/axis-types)
writes the rest of the public surface.

That private constructor is **the only place in the core where throwing is allowed**. Everything else returns
`AxisResult` ([Why AxisResult?](https://github.com/axis-sys/axis-result/blob/main/docs/why-axisresult.md)), and
the generated `TryParse` turns that throw into a plain `false` at the boundary.

| You write | The generator emits |
|---|---|
| `[ValueObject] public readonly partial record struct {Name}` | an implicit conversion from `string?`, which calls your constructor |
| `private {Name}(string? value)`, validating and throwing `ArgumentException` | `TryParse(object?)` and `TryParse(object?, out {Name})`, which return `false` instead of throwing |
| `private string Value { get; }` | an implicit conversion to `string`, `ToString()`, and case-insensitive `Equals` / `GetHashCode` |
| for an id: `public static {Name} New => new(Guid.CreateVersion7().ToString())` | |

An id-style value object validates a UUID v7 and rejects a valid `Guid` of any other version
([`ProductId`][product-id]):

```csharp
[ValueObject]
public readonly partial record struct ProductId
{
    public static ProductId New => new(Guid.CreateVersion7().ToString());

    private ProductId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("ProductId cannot be null or empty");

        if (!Guid.TryParse(value, out var guid))
            throw new ArgumentException($"'{value}' is not a valid ProductId");

        if (guid.Version != 7)
            throw new ArgumentException($"'{value}' is not a valid ProductId (version 7 expected)");

        Value = value;
    }

    private string Value { get; }
}
```

A business value object may also canonicalize its input. [`Sku`][sku] trims and upper-cases, so `"  sku-42a "`
becomes `SKU-42A`, and two SKUs that differ only by case are equal ([`SkuTests`][sku-tests]):

```csharp
[ValueObject]
public readonly partial record struct Sku
{
    private Sku(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("SKU cannot be empty");

        Value = value.Trim().ToUpperInvariant();
    }

    private string Value { get; }
}
```

### Conversions throw, TryParse does not

| Operation | Null input | Invalid input |
|---|---|---|
| `(Sku)value`, or the implicit conversion from `string` | throws | throws |
| `(Sku?)value`, the lifted nullable conversion | throws: it calls the same constructor | throws |
| `Sku.TryParse(value, out var sku)` | returns `false` | returns `false` |

So:

- **Parse at the boundary.** Value objects never reach the driving contracts: commands, queries and responses stay
  string-typed ([`CheckoutProductCommand`][checkout-command]). The validator parses with `RequiredTryParse`, or
  `OptionalTryParse` for an optional input, from [AxisValidator](https://github.com/axis-sys/axis-validator)
  ([`CreateProductValidator`][create-product-validator]):

  ```csharp
  RequiredTryParse(x => x.Sku, ProductErrors.SkuInvalid, Sku.TryParse);
  ```

  Once the validator has passed, the handler converts with a plain cast: `var sku = (Sku)command.Sku!;`.
- **Never convert a possibly-null value.** Map an optional with `x is null ? null : (VO?)x`, as
  [`SubmitOrderHandler`][submit-order-handler] does with the ambient caller:

  ```csharp
  var callerId = mediator.AxisEntityId is { } caller ? (CustomerId?)caller.ToString() : null;
  ```

- **Cast a known-good constant.** Turn a value you know is valid into a value object through the generated cast
  (`(Sku)"SKU-1"`), not by parsing it again.

The analyzers that ship with AxisTypes flag a `[ValueObject]` struct that is not `readonly` (AXIS0200) and a
`null` converted through the implicit conversion from string (AXIS0201).

### Packaging

The kernel references `AxisTypes` directly, **without** `PrivateAssets`
([`Scaffolds.ECommerce.SharedKernel.csproj`][kernel-csproj]):

```xml
<PackageReference Include="AxisResult" />
<PackageReference Include="AxisTypes" />
<PackageReference Include="AxisMediator.Contracts" />
```

The generator and its analyzers run only where the package is referenced directly, so value objects are generated
once, in the kernel. AxisTypes is also a runtime library: `AxisEntityId` (a Guid v7 value object), `EmailAddress`
and the other ready-made value objects must flow transitively to every project that references the kernel. The
driving contracts ([`Scaffolds.ECommerce.Contracts.Driving.csproj`][driving-csproj]) reach AxisTypes only through
the kernel. Setting `PrivateAssets="all"` would cut that flow and break every project that uses `AxisEntityId`
through the kernel.

### What is not a value object

- A plain `sealed record` that groups values without a parsing rule takes no `[ValueObject]` attribute.
- A closed discriminated union in a contract project (an abstract record with a private constructor and nested
  sealed records) models a choice, not a parsed scalar, and is not a value object either.

---

## Checklist: a new aggregate

1. **Value objects** for its id (and any slug) in the kernel, under `{BC}/{Aggregate}/ValueObjects/`; a value
   shared by several bounded contexts goes under `Shared/` ([`CustomerId`][customer-id]).
2. **The Properties pair** in `{App}.Domain`, under `{BC}/{Aggregate}/`.
3. **The ports and `{Aggregate}Errors`** in `{App}.Contracts.Driven`, under `{BC}/{Aggregate}/`; Reader only for
   an N0 lookup ([Reader and Write ports](persistence.md#reader-and-write-ports)).
4. **The factory** (N1) in `{App}.Application`, under `Aggregates/`, registered as scoped.
5. **The repository, row mapping and table** in the repository adapter ([Persistence](persistence.md)).
6. **The level**: stay at N0 or N1 until the first rule guards a decision on the aggregate's state, then move it
   to N2 in that same change.

## See also

- [Persistence](persistence.md): the ports, repositories, unit of work and schema behind these aggregates.
- [Architecture](architecture.md): the projects and the folder-per-feature layout.
- [Style](style.md): naming, the `{Aggregate}Errors` classes and formatting.
- [Testing](testing.md): fakes for the internal records, and where each test belongs.
- [AxisTypes](https://github.com/axis-sys/axis-types) · [AxisResult](https://github.com/axis-sys/axis-result) ·
  [AxisValidator](https://github.com/axis-sys/axis-validator)

[product-id]: https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.SharedKernel/Catalog/Products/ValueObjects/ProductId.cs
[sku]: https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.SharedKernel/Catalog/Products/ValueObjects/Sku.cs
[customer-id]: https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.SharedKernel/Shared/CustomerId.cs
[permission-codes]: https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.SharedKernel/Authorization/PermissionCodes.cs
[auth-roles]: https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.SharedKernel/Auth/AuthRoles.cs
[kernel-csproj]: https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.SharedKernel/Scaffolds.ECommerce.SharedKernel.csproj
[iproduct-properties]: https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Domain/Catalog/Products/IProductEntityProperties.cs
[product-properties]: https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Domain/Catalog/Products/ProductProperties.cs
[order-properties]: https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Domain/Catalog/Orders/OrderProperties.cs
[icustomer-properties]: https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Domain/Customers/Customers/ICustomerEntityProperties.cs
[domain-csproj]: https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Domain/Scaffolds.ECommerce.Domain.csproj
[product-factory]: https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Application/Catalog/Products/Aggregates/ProductFactory.cs
[order-factory]: https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Application/Catalog/Orders/Aggregates/OrderFactory.cs
[cart-item-factory]: https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Application/Catalog/CartItems/Aggregates/CartItemFactory.cs
[customer-factory]: https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Application/Customers/Aggregates/CustomerFactory.cs
[validation-code-factory]: https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Application/EmailValidation/Aggregates/ValidationCodeFactory.cs
[member-factory]: https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Application/Authorization/Aggregates/MemberFactory.cs
[catalog-app-di]: https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Application/Catalog/DependencyInjection.cs
[create-product-handler]: https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Application/Catalog/Products/UseCases/CreateProduct/v1/CreateProductHandler.cs
[create-product-validator]: https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Application/Catalog/Products/UseCases/CreateProduct/v1/CreateProductValidator.cs
[checkout-handler]: https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Application/Catalog/Products/UseCases/CheckoutProduct/v1/CheckoutProductHandler.cs
[submit-order-handler]: https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Application/Catalog/Orders/UseCases/SubmitOrder/v1/SubmitOrderHandler.cs
[iorder-pricing]: https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Application/Catalog/Orders/Services/OrderPricing/IOrderPricingService.cs
[order-pricing]: https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Application/Catalog/Orders/Services/OrderPricing/OrderPricingService.cs
[iproducts-reader]: https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Contracts/Scaffolds.ECommerce.Contracts.Driven/Catalog/Products/IProductsReaderPort.cs
[iproducts-write]: https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Contracts/Scaffolds.ECommerce.Contracts.Driven/Catalog/Products/IProductsWritePort.cs
[product-errors]: https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Contracts/Scaffolds.ECommerce.Contracts.Driven/Catalog/Products/ProductErrors.cs
[checkout-command]: https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Contracts/Scaffolds.ECommerce.Contracts.Driving/Catalog/Products/v1/CheckoutProduct/CheckoutProductCommand.cs
[driving-csproj]: https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Contracts/Scaffolds.ECommerce.Contracts.Driving/Scaffolds.ECommerce.Contracts.Driving.csproj
[fake-product]: https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Tests/Scaffolds.ECommerce.UnitTests/FakeProduct.cs
[sku-tests]: https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Tests/Scaffolds.ECommerce.UnitTests/SkuTests.cs
