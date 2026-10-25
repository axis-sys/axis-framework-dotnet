# Code style

An Axis application follows one small grammar for names, visibility and layout. A reader who knows a use-case
name can predict the file, the folder, the namespace and every type of its slice, and can tell from a type's
visibility which side of the hexagon it serves. The reference application under
[`scaffolds/`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/README.md) applies every
convention on this page; each section links to the files that show it.

| Section | What it fixes |
|---|---|
| [Naming language](#naming-language) | English technical verbs, one domain language, verb-first use cases, the `Async` suffix, the names of ports, facades, events and sagas, file = type, namespace = folder |
| [Project naming](#project-naming) | `{App}.{Layer}.{Role}`, mirrored in the folder path |
| [Properties interfaces](#properties-interfaces) | `I{X}EntityProperties` for entities, `I{X}Properties` for lookups |
| [Value object placement](#value-object-placement) | the `ValueObjects` folder and namespace of each aggregate |
| [Access modifiers](#access-modifiers) | public only across a project boundary, and only for interfaces and dumb records |
| [Errors classes](#errors-classes) | one `{Aggregate}Errors` per aggregate, beside its driven ports |
| [Error member shape](#error-member-shape) | a ready `AxisError` member; a `const string` only for validator codes |
| [Declaration formatting](#declaration-formatting) | a declaration is either one line or fully broken |
| [Entity records](#entity-records) | one constructor parameter per line |

---

## Naming language

Technical and CRUD vocabulary is always English. Domain and business terms use the one human language the
solution declares in its README. The two never mix inside one identifier. What the convention fixes is
consistency: English as the domain language is a valid choice, not a mandate.

- Technical verbs stay English whatever the language of their noun: `Create`, `Update`, `Get`, `List`, `Search`,
  `Import`, `Cancel`, `Grant`, `Confirm`, `Deactivate` and the like.
- The domain language is chosen once and recorded in the README. Every domain term then uses it: identifiers,
  XML docs, error codes, seed values. The reference application declares English in its
  [README](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/README.md).
- A use case is a verb-first imperative phrase (`CheckoutProduct`, `RegisterProduct`, `SubmitOrder`), and every
  artifact of its slice takes its name from it.
- Every method that does I/O ends in `Async`, and that includes private helpers inside handlers.

| Artifact | Pattern | In the reference application |
|---|---|---|
| Slice | `{UseCase}Command` / `Query` / `Response` / `Handler` / `Validator` | `CheckoutProductCommand`, `CheckoutProductResponse`, `CheckoutProductHandler`, `CheckoutProductValidator` |
| Driven ports | `I{Entities}ReaderPort`, `I{Entities}WritePort` | `IProductsReaderPort`, `IProductsWritePort` |
| Facades | `I{Entities}Facade`, using the plural of the aggregate entity. A process-style facade keeps the process name | `IProductsFacade`, `IOrdersFacade`, `ICustomersFacade`; `IAuthFacade`, `IEmailValidationFacade` |
| Events | `{Thing}{PastTenseVerb}Event` | `ProductCheckedOutEvent`, `GrantsUpdatedEvent` |
| Sagas | `{Verb}{Noun}Saga` | `ValidateEmailSaga` |
| Saga stage handlers | `{Stage}StageHandler` | `MarkEmailValidatedStageHandler`, `RevertEmailValidatedStageHandler` |
| Error codes | `UPPER_SNAKE_CASE` | `PRODUCT_NOT_FOUND` |
| Bus topics | lower `snake_case` | `product_checked_out` |

Error codes and bus topics are stable contracts, because clients and subscribers match on them.

**Files and namespaces.**

- A file is named after the single top-level type it declares. The one exception is a file that pairs an
  interface with its only implementation in the same project: it takes the implementation's name.
  [`ProductFactory.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Application/Catalog/Products/Aggregates/ProductFactory.cs)
  holds `IProductFactory` and `ProductFactory`.
- Namespaces mirror folders exactly and are declared file-scoped. The file
  `Catalog/Products/UseCases/CheckoutProduct/v1/CheckoutProductHandler.cs` of the application project declares:

```csharp
namespace Scaffolds.ECommerce.Application.Catalog.Products.UseCases.CheckoutProduct.v1;
```

The reference application has one block-scoped namespace, in
[`Program.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Host/Scaffolds.ECommerce.Host/Program.cs).
That file uses top-level statements, which rule out a file-scoped namespace, and it still has to declare the
`public partial class Program` the test host needs.

See:
[`CheckoutProductHandler.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Application/Catalog/Products/UseCases/CheckoutProduct/v1/CheckoutProductHandler.cs) ·
[`IProductsFacade.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Contracts/Scaffolds.ECommerce.Contracts.Driving/Catalog/Products/v1/IProductsFacade.cs) ·
[`ValidateEmailSaga.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Application/EmailValidation/Sagas/ValidateEmail/ValidateEmailSaga.cs) ·
[`ProductCheckedOutEvent.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.SharedKernel/Catalog/Events/v1/ProductCheckedOutEvent.cs)

---

## Project naming

Every project is named `{App}.{Layer}[.{Role}]`: the solution prefix, then the project's position in the hexagon,
then a role that tells apart siblings in the same layer. The name alone tells you which side of the hexagon the
code is on. In the reference application `{App}` is `Scaffolds.ECommerce`.

| Folder | Projects | Pattern |
|---|---|---|
| `Core/` | `Scaffolds.ECommerce.SharedKernel` · `.Domain` · `.Application` | a core project that is unique in its role drops the redundant layer infix |
| `Contracts/` | `Scaffolds.ECommerce.Contracts.Driving` | `{App}.Contracts.Driving[.{Role}]`: commands, queries, responses, sub-DTOs and the facade interfaces |
| | `Scaffolds.ECommerce.Contracts.Driven` | `{App}.Contracts.Driven[.{Role}]`: the outbound port interfaces |
| `Adapters/` | `Scaffolds.ECommerce.Adapters.Driving.Facade` | `{App}.Adapters.Driving.{Role}` |
| | `Scaffolds.ECommerce.Adapters.Driven.Repository` · `.Postgres` · `.MySql` · `.Jwt` · `.AuthProviders` · `.InMemory` | `{App}.Adapters.Driven.{Role}` |
| `Host/` | `Scaffolds.ECommerce.Host` | the one composition root |
| `Tests/` | `Scaffolds.ECommerce.UnitTests` · `.IntegrationTests` · `.E2ETests` · `.ArchitectureTests` | `{App}.{Kind}Tests` |

The contract projects carry the port direction in their name, so you can tell the driving side from the driven
side without opening them. The folder path mirrors the project name and repeats it as the leaf directory that
holds the project file:

```text
Contracts/Scaffolds.ECommerce.Contracts.Driving/Scaffolds.ECommerce.Contracts.Driving.csproj
```

The convention fixes how a name maps to a path, not the folder layout. You can group folders layer-first, as the
reference application does, or feature-first. Either way, each project name states its position in the hexagon
and its leaf directory repeats that name. A project shipped as an external tool may keep the prefix and set its
`AssemblyName` to the tool's published name (see the [developer CLI](process.md#developer-cli)).
[Architecture](architecture.md) covers what each layer may reference.

See:
[`Scaffolds.ECommerce.Contracts.Driving.csproj`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Contracts/Scaffolds.ECommerce.Contracts.Driving/Scaffolds.ECommerce.Contracts.Driving.csproj) ·
[`Scaffolds.ECommerce.Contracts.Driven.csproj`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Contracts/Scaffolds.ECommerce.Contracts.Driven/Scaffolds.ECommerce.Contracts.Driven.csproj) ·
[`Scaffolds.ECommerce.Adapters.Driving.Facade.csproj`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Adapters/Scaffolds.ECommerce.Adapters.Driving.Facade/Scaffolds.ECommerce.Adapters.Driving.Facade.csproj) ·
[`Scaffolds.ECommerce.Adapters.Driven.AuthProviders.csproj`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Adapters/Scaffolds.ECommerce.Adapters.Driven.AuthProviders/Scaffolds.ECommerce.Adapters.Driven.AuthProviders.csproj)

---

## Properties interfaces

The name of a domain properties interface signals the aggregate's level (see [Domain](domain.md)):

| Aggregate | Interface | Concrete record |
|---|---|---|
| An entity that use cases create and change, backed by a write port | `I{X}EntityProperties` | `{X}Properties` |
| A reader-only seeded lookup, reached only through a reader port | `I{X}Properties` | `{X}Properties` |

Only the interface carries the signal. The internal record drops both the leading `I` and the `Entity` segment,
so its name is the same at both levels. Every aggregate in the reference application has a write port, so all of
them use the entity form:

```csharp
public interface IProductEntityProperties
{
    ProductId ProductId { get; }
    Sku Sku { get; }
    string Name { get; }
    int Stock { get; }
}
```

```csharp
internal sealed record ProductProperties(
    ProductId ProductId,
    Sku Sku,
    string Name,
    int Stock
) : IProductEntityProperties;
```

Don't rename an interface that deviates from this in passing. Ports, repositories and tests all reference it,
so renaming it needs a coordinated sweep.

See:
[`IProductEntityProperties.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Domain/Catalog/Products/IProductEntityProperties.cs) ·
[`ProductProperties.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Domain/Catalog/Products/ProductProperties.cs)

---

## Value object placement

Value objects live in the shared kernel, in a real `ValueObjects/` folder under their aggregate, and declare the
namespace that folder spells, like every other folder. That way a `using` says plainly what is a value object, and
the namespace never drifts from the disk.

| Owner | Folder | Namespace | In the reference application |
|---|---|---|---|
| One aggregate | `{BC}/{Aggregate}/ValueObjects/` | `{Kernel}.{BC}.{Aggregate}.ValueObjects` | `Sku` and `ProductId` in `Catalog/Products/ValueObjects/`, `OrderId` in `Catalog/Orders/ValueObjects/`, `CartId` in `Catalog/CartItems/ValueObjects/` |
| The whole bounded context | `{BC}/ValueObjects/` | `{Kernel}.{BC}.ValueObjects` | (none) |
| Every bounded context | `Shared/` | `{Kernel}.Shared`, with no `ValueObjects` segment | `CustomerId` |

- The folder and the namespace never diverge.
- The `ValueObjects` segment is reserved for `[ValueObject]` types, which the
  [`AxisTypes`](https://github.com/axis-sys/axis-types) generator produces. Never reuse it for plain DTOs.
- Other kernel types never go in a `ValueObjects` folder or namespace. That covers auth and permission constant
  classes, the shared errors class and bus events: `PermissionCodes` stays in `Authorization/`, `SharedErrors` in
  `Shared/`, and events in `{BC}/Events/v1/`.

```csharp
namespace Scaffolds.ECommerce.SharedKernel.Catalog.Products.ValueObjects;

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

See:
[`Sku.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.SharedKernel/Catalog/Products/ValueObjects/Sku.cs) ·
[`OrderId.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.SharedKernel/Catalog/Orders/ValueObjects/OrderId.cs) ·
[`CartId.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.SharedKernel/Catalog/CartItems/ValueObjects/CartId.cs) ·
[`CustomerId.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.SharedKernel/Shared/CustomerId.cs) ·
[`PermissionCodes.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.SharedKernel/Authorization/PermissionCodes.cs)

---

## Access modifiers

Visibility encodes the hexagonal boundary. A type that another project consumes is public: a properties
interface, a port, a facade, a DTO, a code catalog, a kernel record. A type that is an implementation detail is
`internal sealed`: a concrete properties record, a factory, a handler, a domain service, a schema model, an
adapter. Interfaces and dumb records cross the boundary. Implementations never do.

| Project | Public | Internal |
|---|---|---|
| Domain | `I{X}EntityProperties` / `I{X}Properties` interfaces and `{X}Codes` catalogs | every concrete record and domain service, as `internal sealed` |
| Shared kernel | everything, because the kernel is the shared leaf. Its records are `sealed` (`public sealed record ProductCheckedOutEvent`) | nothing |
| Contracts, driving and driven | only interfaces, `sealed record` DTOs and the static errors classes | nothing: no class with behavior, no implementation |
| Application | the project's single `DependencyInjection` entry, plus the options classes the composition root binds (`CatalogOptions`) | handlers, validators, factories, services, sagas |
| Adapters | the single `DependencyInjection` entry; the schema constants `{Entities}Table`, `{Entities}Columns` and `{BC}DbInit`, as `public static`; a seam interface that another adapter implements (`IMemberSqlDialect`) | repositories, facades, row records, dialect implementations |
| Host | controllers, as `public sealed` (see [Edge](edge.md)) | filters, configuration readers, per-feature registrations |

- Port and facade implementations live in the application or in adapters. They are `internal sealed` and are
  registered through DI.
- Schema constants used across projects and the DI root classes are `public static` entry points for the
  composition root. A per-feature `DependencyInjection` class stays `internal static` (see
  [Architecture](architecture.md)).
- Tests reach internals through `InternalsVisibleTo` entries in the project file, never by making a type more
  visible. Every new grant needs a justification and goes to as few consuming projects as possible. In the
  reference application, the domain grants access to the application and the unit tests, the application to the
  unit tests, and the host and the in-memory adapter to the E2E tests:

```xml
<ItemGroup>
  <InternalsVisibleTo Include="Scaffolds.ECommerce.Application" />
  <InternalsVisibleTo Include="Scaffolds.ECommerce.UnitTests" />
</ItemGroup>
```

Two shapes are deliberate exceptions:

- A factory's argument record is nested inside the internal factory interface (`IProductFactory.NewArgs`), which
  makes it internal twice over.
- An N2 entity that declares protected members (the rules that the application's `AggregateApplication`
  subclass inherits) is internal but not sealed. It is never public (see [Domain](domain.md)).

The reference application seals every driving DTO, event and saga payload as a `public sealed record`. Whether to
seal the driving DTOs is up to each solution.

| Do | Don't |
|---|---|
| Expose a port as a public interface and implement it with an `internal sealed` class | Make a repository or a facade public so another project can construct it |
| Grant `InternalsVisibleTo` to the one test project that needs it | Make an internal type public "for the tests" |
| Keep domain state behind `I{X}EntityProperties` | Make the concrete `{X}Properties` record public |

Part of this is checked by the build. It flags handlers, validators and domain properties records that are not
`internal sealed`, and a second public `DependencyInjection` class in the same assembly (see
[Review before a commit](process.md#review-before-a-commit)).

See:
[`Scaffolds.ECommerce.Domain.csproj`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Domain/Scaffolds.ECommerce.Domain.csproj) ·
[`ProductsFacade.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Adapters/Scaffolds.ECommerce.Adapters.Driving.Facade/Catalog/Products/ProductsFacade.cs) ·
[`ProductsRepository.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Adapters/Scaffolds.ECommerce.Adapters.Driven.Repository/Catalog/Products/ProductsRepository.cs) ·
[`ProductsTable.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Adapters/Scaffolds.ECommerce.Adapters.Driven.Repository/Catalog/Products/ProductsTable.cs) ·
[`CatalogDbInit.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Adapters/Scaffolds.ECommerce.Adapters.Driven.Repository/Catalog/CatalogDbInit.cs) ·
[`DependencyInjection.cs` (repository adapter)](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Adapters/Scaffolds.ECommerce.Adapters.Driven.Repository/DependencyInjection.cs) ·
[`CatalogOptions.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Application/Catalog/CatalogOptions.cs)

---

## Errors classes

An error is identified by its code, not by a message. Each aggregate owns exactly one
`public static {Aggregate}Errors` class. It lives in the driven contracts, in the aggregate's folder next to its
reader and write ports: `{App}.Contracts.Driven/{BC}/{Aggregate}/{Aggregate}Errors.cs`. The driven contracts are
the lowest project that both the repository adapter and the application reference, so a single declaration serves
the repository, the application and the tests.

```text
Contracts/Scaffolds.ECommerce.Contracts.Driven/Catalog/Products/
├─ IProductsReaderPort.cs
├─ IProductsWritePort.cs
└─ ProductErrors.cs
```

```csharp
namespace Scaffolds.ECommerce.Contracts.Driven.Catalog.Products;

public static class ProductErrors
{
    public const string ProductIdInvalid = "PRODUCT_ID_INVALID";
    public static readonly AxisError ProductNotFound = AxisError.NotFound("PRODUCT_NOT_FOUND");

    public static readonly AxisError InsufficientStock = AxisError.BusinessRule("INSUFFICIENT_STOCK");

    public const string SkuInvalid = "SKU_INVALID";
    public const string InitialStockInvalid = "INITIAL_STOCK_INVALID";
    public static readonly AxisError SkuAlreadyExists = AxisError.Conflict("PRODUCT_SKU_ALREADY_EXISTS");
}
```

- There is one class per aggregate, named after the aggregate's entity: `ProductErrors` for Products, then
  `CartItemErrors`, `OrderErrors`, `CustomerErrors`, `MemberErrors`, `ValidationCodeErrors`. This holds in a
  bounded context with a single aggregate too.
- It never goes in the application, and never inside a use-case folder.
- A bounded context without an aggregate declares its failures in one `{BC}Errors` class next to its driven ports:
  `AuthErrors` next to `IExternalIdentityContext` and `IAccessTokenSignerPort`, `AdminErrors` next to
  `IDatabaseMigratorPort`.
- A code that several aggregates use lives in the kernel's single `SharedErrors` class (`{Kernel}/Shared/`), for
  example `CART_ID_INVALID` and `QUANTITY_MUST_BE_POSITIVE`. No aggregate declares it again.
- Codes are `UPPER_SNAKE_CASE` and usually start with the aggregate or the field (`PRODUCT_NOT_FOUND`,
  `SKU_INVALID`). Member names are PascalCase.
- Members are grouped by use case in declaration order, with a blank line between groups and no comment header.
- A code never appears as a string literal outside its errors class, and no layer declares again a code that
  another layer already declares. The repository passes `ProductErrors.SkuAlreadyExists.Code` to
  `IAxisDbRepository`, `CreateProductHandler` returns `ProductErrors.SkuAlreadyExists`, and
  `CheckoutProductValidator` takes its codes from `SharedErrors` and `ProductErrors`.
- Once a code ships it never changes, because clients match on it. Add a new code instead of renaming one.

Failures that only the edge produces stay at the edge. The host's permission filter declares its own in an
`internal static AuthorizationErrors` class inside the host, because no other project references them.

See:
[`ProductErrors.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Contracts/Scaffolds.ECommerce.Contracts.Driven/Catalog/Products/ProductErrors.cs) ·
[`CustomerErrors.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Contracts/Scaffolds.ECommerce.Contracts.Driven/Customers/Customers/CustomerErrors.cs) ·
[`AuthErrors.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Contracts/Scaffolds.ECommerce.Contracts.Driven/Auth/AuthErrors.cs) ·
[`AdminErrors.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Contracts/Scaffolds.ECommerce.Contracts.Driven/Admin/AdminErrors.cs) ·
[`SharedErrors.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.SharedKernel/Shared/SharedErrors.cs) ·
[`CreateProductHandler.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Application/Catalog/Products/UseCases/CreateProduct/v1/CreateProductHandler.cs) ·
[`AuthorizationErrors.cs` (host)](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Host/Scaffolds.ECommerce.Host/Authorization/AuthorizationErrors.cs)

---

## Error member shape

A failure has two halves that belong together: a stable code and an `AxisErrorType` category. The edge turns the
category into an HTTP status (see
[Errors and types](https://github.com/axis-sys/axis-result/blob/main/docs/errors-and-types.md)). If you declare
only the code, each call site picks the category, and the same code can leave one handler as a 500 and another as
a 400. That is why the errors class declares the finished error.

| Member | When | Example |
|---|---|---|
| `public static readonly AxisError`, built with the factory for its category | by default | `SkuAlreadyExists = AxisError.Conflict("PRODUCT_SKU_ALREADY_EXISTS")` |
| a `public static` method that returns `AxisError` | when the code is built from runtime context | (none) |
| `public const string` | only when the code feeds an API that takes a raw code and sets the category itself. In practice, that means the rule helpers of [`AxisValidator`](https://github.com/axis-sys/axis-validator) (`RequiredTryParse`, `RequiredRange` and the rest), which always emit `ValidationRule` | `SkuInvalid = "SKU_INVALID"` |

- Each failure is declared once. It is never both a `const string` and an `AxisError` in the same class.
- A `const string` that only ever gets passed to an `AxisError` factory becomes a `static readonly AxisError`.
- The member is `static readonly`, never a mutable static. `AxisError` is an immutable record, so a shared
  instance stays shared and allocates nothing per call.
- Call sites return the declared member and never build it again: calling an `AxisError` factory outside an errors
  class is a violation. The build catches the case it can decide on its own ([`AXIS0608`](analyzers.md#axis0608)): a
  factory call outside an errors class that is given one of that class's `const` codes. Review catches the rest, such
  as a category chosen wrongly or a code that should have been parameterized.
- A test that matches on the raw code reads it from the member's `Code` property, as in
  `ShouldFailWithCode(ProductErrors.InsufficientStock.Code)` from
  [`Axis.Testing`](https://github.com/axis-sys/axis-testing). That does not justify a separate `const string`.

A handler returns the declared member as is:

```csharp
.EnsureAsync(product => product.Stock >= command.Quantity, ProductErrors.InsufficientStock)
```

A validator hands its helpers the raw codes:

```csharp
internal sealed class CheckoutProductValidator : AxisValidatorBase<CheckoutProductCommand>
{
    public CheckoutProductValidator()
    {
        RequiredTryParse(x => x.CartId, SharedErrors.CartIdInvalid, CartId.TryParse);
        RequiredTryParse(x => x.ProductId, ProductErrors.ProductIdInvalid, ProductId.TryParse);
        RequiredRange(x => x.Quantity, SharedErrors.QuantityMustBePositive, min: 1);
    }
}
```

| Do | Don't |
|---|---|
| Return `ProductErrors.InsufficientStock` from the handler | Call `AxisError.BusinessRule(...)` inside the handler |
| Keep `SkuInvalid` as a `const string` because the validator needs a raw code | Declare `SkuAlreadyExists` as a `const string` and wrap it in `AxisError.Conflict` at every call site |

See:
[`CheckoutProductHandler.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Application/Catalog/Products/UseCases/CheckoutProduct/v1/CheckoutProductHandler.cs) ·
[`CheckoutProductValidator.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Application/Catalog/Products/UseCases/CheckoutProduct/v1/CheckoutProductValidator.cs) ·
[`CheckoutConcurrencyDualDbTests.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Tests/Scaffolds.ECommerce.IntegrationTests/CheckoutConcurrencyDualDbTests.cs)

---

## Declaration formatting

One readability convention applies across the whole codebase.

- A type declaration with a primary constructor and a base list stays on **one line** as long as it fits (about
  160 characters). This is the default.
- When it doesn't fit, break it **fully**: each constructor parameter on its own line, then the closing
  `) : BaseOrInterfaces` alone on the next line. Don't put two or three parameters on one line, don't split the
  base list across lines, and don't leave the base list hanging under a one-line parameter list. A declaration is
  either all on one line or all in the long form.
- Interpolated `const` strings, such as column lists or SQL projection and `WHERE` fragments, stay on one line
  however long they get.
- Blank lines go between multi-line members and between deliberate groups. Consecutive one-line members of the
  same kind have no blank line between them.
- Private value-object constructors emitted by the generator, parameterless validators and private singleton
  constructors keep the shape they have. There is nothing to put on one line.

```csharp
internal sealed class GetProductHandler(IProductsReaderPort productsReader) : IAxisQueryHandler<GetProductQuery, GetProductResponse>
```

```csharp
internal sealed class CheckoutProductHandler(
    IAxisMediator mediator,
    IProductsWritePort productsWrite,
    IAxisBus bus,
    IUnitOfWork unitOfWork
) : IAxisCommandHandler<CheckoutProductCommand, CheckoutProductResponse>
```

```csharp
public const string All = $"{CustomerId}, {Email}, {Name}, {IsAdmin}, {ExternalId}, {Provider}, {EmailValidated}";
```

To reformat many files at once, use a tool that preserves encoding, BOM and line endings byte for byte.
Line-based tools can corrupt them.

See:
[`GetProductHandler.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Application/Catalog/Products/UseCases/GetProduct/v1/GetProductHandler.cs) ·
[`CheckoutProductHandler.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Application/Catalog/Products/UseCases/CheckoutProduct/v1/CheckoutProductHandler.cs) ·
[`CustomersColumns.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Adapters/Scaffolds.ECommerce.Adapters.Driven.Repository/Customers/Customers/CustomersColumns.cs) ·
[`ProductsColumns.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Adapters/Scaffolds.ECommerce.Adapters.Driven.Repository/Catalog/Products/ProductsColumns.cs)

---

## Entity records

A domain `{Entity}Properties` record and a repository `{Entity}DbEntity` both carry the persisted shape of an
entity in their positional parameters. When there are two or more parameters, each one gets its own line. A reader
can then scan the entity's shape top to bottom, and for a row record, the order of the columns it maps.

- The convention covers any record that implements an `I…Properties` interface (`I{Entity}EntityProperties` or
  `I{Entity}Properties`), whether it is the domain's own record or a repository row record, in any assembly. A
  `record struct` is a value object and is not covered.
- With two or more parameters, each parameter starts on its own line, and none shares the line of the `record`
  keyword. A record with a single parameter is not wrapped.
- The closing parenthesis sits on its own line, before the base list.
- A row record's static `FromReader` follows the same layout, one argument per line in its `new(...)`. The declared
  shape and the reader ordinals then line up column for column.

```csharp
internal sealed record CartItemDbEntity(
    CartId CartId,
    ProductId ProductId,
    int Quantity
) : ICartItemEntityProperties
{
    internal static CartItemDbEntity FromReader(DbDataReader reader)
        => new(
            (CartId)reader.GetString(0),
            (ProductId)reader.GetString(1),
            reader.GetInt32(2)
        );
}
```

[`AXIS0606`](analyzers.md#axis0606), from the `Axis.Conventions.Analyzers` package, enforces the parameter layout.
The reference application runs those analyzers on every project, in
[`Directory.Build.props`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Directory.Build.props).
It has to be an analyzer because no `.editorconfig` or formatter setting puts positional-record parameters one per
line. The closing parenthesis and the `FromReader` layout are a matter of style and are not analyzed.
[Persistence](persistence.md) covers row records.

See:
[`CartItemDbEntity.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Adapters/Scaffolds.ECommerce.Adapters.Driven.Repository/Catalog/CartItems/CartItemDbEntity.cs) ·
[`CustomerDbEntity.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Adapters/Scaffolds.ECommerce.Adapters.Driven.Repository/Customers/Customers/CustomerDbEntity.cs) ·
[`OrderProperties.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Domain/Catalog/Orders/OrderProperties.cs)
