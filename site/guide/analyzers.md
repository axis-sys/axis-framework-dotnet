# Analyzers · `AXIS0600`–`AXIS0608`

> Roslyn analyzers that turn the decidable part of this guide into build diagnostics. They ship in the
> **`Axis.Conventions.Analyzers`** package, released from this repository, which is separate and opt-in: the
> conventions govern how an application is built, so they never ride along with an Axis package a project
> references for a single primitive.

Reference the package once for every project of the application, in its `Directory.Build.props`, and build with
`-warnaserror`:

```xml
<ItemGroup>
  <PackageReference Include="Axis.Conventions.Analyzers" PrivateAssets="all" />
</ItemGroup>
```

The reference application, which lives in the same repository as the analyzers, runs them from source through a
project reference instead
([`scaffolds/Directory.Build.props`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Directory.Build.props)).

| Id | Flags | Category | Default severity |
|---|---|---|---|
| [`AXIS0600`](#axis0600) | a use-case handler that is not `internal sealed` | `Axis.Architecture` | warning |
| [`AXIS0601`](#axis0601) | a validator that is not `internal sealed` | `Axis.Architecture` | warning |
| [`AXIS0602`](#axis0602) | a controller that is not `sealed` | `Axis.Edge` | warning |
| [`AXIS0603`](#axis0603) | a test method in snake_case, or an asynchronous one without the `Async` suffix | `Axis.Testing` | info |
| [`AXIS0604`](#axis0604) | a domain properties record that is not `internal sealed` | `Axis.Architecture` | warning |
| [`AXIS0605`](#axis0605) | a command property filled from the route that is not `[JsonIgnore]` | `Axis.Edge` | warning |
| [`AXIS0606`](#axis0606) | an entity properties record whose constructor parameters share a line | `Axis.Style` | warning |
| [`AXIS0607`](#axis0607) | a second `public` `DependencyInjection` class in the same assembly | `Axis.Architecture` | warning |
| [`AXIS0608`](#axis0608) | an `AxisError` built at the call site from a `const` code of an errors class | `Axis.Style` | warning |

Detection is **semantic** — through the implemented interface or the base type, never the name — except `AXIS0603`,
which reads the syntax (the test attribute and the method name), and `AXIS0606`, which finds the record semantically
and reads its layout from the syntax. Generated code is not analyzed. Most analyzers do nothing in a project where the
Axis type they look for is absent:

| Id | Runs when the compilation has |
|---|---|
| `AXIS0600` | a handler interface of `AxisMediator.Contracts` |
| `AXIS0601` | `AxisValidatorBase<T>` (`AxisValidator.FluentValidation`) |
| `AXIS0602` | `Microsoft.AspNetCore.Mvc.ControllerBase` |
| `AXIS0604`, `AXIS0606`, `AXIS0608` | `AxisError` (`AxisResult`) |
| `AXIS0605` | `IAxisRequest` (`AxisMediator.Contracts`) and `System.Text.Json`'s `JsonIgnoreAttribute` |
| `AXIS0603`, `AXIS0607` | always — they need no Axis type |

The conventions an application must follow ship as warnings, the one it should follow (`AXIS0603`) as info. Fix the
code rather than silence a diagnostic; to make one stricter, raise it — or a whole category — in the `.editorconfig`:

```ini
[*.cs]
dotnet_diagnostic.AXIS0600.severity = error
dotnet_analyzer_diagnostic.category-Axis.Architecture.severity = error
```

---

## AXIS0600

**Use-case handler is not `internal sealed`** · warning · `Axis.Architecture`

**Flags** a class that implements one of the handler interfaces of `AxisMediator.Contracts` —
`IAxisCommandHandler<TCommand>`, `IAxisCommandHandler<TCommand, TResponse>`, `IAxisQueryHandler<TQuery, TResponse>`,
`IAxisStreamQueryHandler<TQuery, TItem>` or `IAxisEventHandler<TEvent>` — and is not both `internal` and `sealed`. A
class merely named `*Handler` is not a handler; a handler named otherwise still is. Abstract, static and generic
classes are not inspected.

**Why.** A handler is registered by assembly scanning and reached only through the mediator, behind a facade. It never
crosses a project boundary as a public type, and nothing derives from it.

**Fix.** Declare it `internal sealed`, with its ports in the primary constructor ([Handlers](architecture.md#handlers)).

```csharp
// Before
public class CheckoutProductHandler(IProductsWritePort productsWrite)
    : IAxisCommandHandler<CheckoutProductCommand, CheckoutProductResponse> { … }

// After
internal sealed class CheckoutProductHandler(IProductsWritePort productsWrite)
    : IAxisCommandHandler<CheckoutProductCommand, CheckoutProductResponse> { … }
```

---

## AXIS0601

**Validator is not `internal sealed`** · warning · `Axis.Architecture`

**Flags** a class that derives from `AxisValidatorBase<T>` and is not both `internal` and `sealed`. Abstract, static
and generic classes are not inspected.

**Why.** A validator is an implementation detail: assembly scanning discovers it and the mediator's validation
behavior runs it before the handler. It never crosses a project boundary as a public type.

**Fix.** Declare it `internal sealed {UseCase}Validator`, in the same `v1` folder as the handler
([Validators](architecture.md#validators)).

```csharp
// Before
public class EnsureCustomerValidator : AxisValidatorBase<EnsureCustomerCommand> { … }

// After
internal sealed class EnsureCustomerValidator : AxisValidatorBase<EnsureCustomerCommand> { … }
```

---

## AXIS0602

**Controller is not `sealed`** · warning · `Axis.Edge`

**Flags** a class that derives from `ControllerBase` — directly or through the view-aware `Controller` — and is not
`sealed`. An abstract base controller is meant to be inherited and is not flagged.

**Why.** A controller is a leaf of the edge: it is never subclassed. Leaving it open invites inheritance the edge does
not support.

**Fix.** Seal it. The analyzer checks only `sealed`; inheriting `ControllerBase` rather than `Controller`, the
attribute stack and the action shape are the rest of the [controller shape](edge.md#controller-shape), which review
covers.

```csharp
// Before
public class ProductsV1Controller(IProductsFacade productsFacade) : ControllerBase { … }

// After
public sealed class ProductsV1Controller(IProductsFacade productsFacade) : ControllerBase { … }
```

---

## AXIS0603

**Test method name is not PascalCase with an `Async` suffix** · info · `Axis.Testing`

**Flags** a test method — one with an attribute written `Fact`, `Theory`, `FactAttribute`, `TheoryAttribute`, or a
qualified name ending in `.Fact` or `.Theory` — whose name contains an underscore, or which is asynchronous (the
`async` modifier, or a `Task` / `ValueTask` return type) and does not end in `Async`. The check is syntactic: it reads
the attribute name and the method name, nothing else.

**Why.** The name is the specification of the case under test — `{WhatItDoes}When{Condition}`, in PascalCase, with
`Async` on asynchronous tests. A snake_case sentence is a divergence to correct, not an alternative
([Naming](testing.md#naming)).

**Fix.** Rename the method. The analyzer cannot judge whether the name says what the test proves; review does.

```csharp
// Before
[Fact]
public async Task Checkout_fails_when_stock_is_short() { … }

// After
[Fact]
public async Task FailsWithInsufficientStockWhenTheQuantityExceedsTheStockAsync() { … }
```

---

## AXIS0604

**Domain properties record is not `internal sealed`** · warning · `Axis.Architecture`

**Flags** a concrete class or record that implements an interface named `I…Properties` declared **in the same
project**, and is not `internal`, or is `internal` but not `sealed`. Record structs (value objects) and types that
implement no such interface are not inspected. A repository's `DbEntity` implements the interface from another
assembly and is out of scope (its layout is [`AXIS0606`](#axis0606)).

**Why.** The domain exposes entity state through the public `I{Entity}EntityProperties` interface and hides the
concrete record. A public record lets another layer build domain state directly, around the factory and the ports
that traffic in the interface ([The Properties pair](domain.md#the-properties-pair)).

**Exception.** An N2 domain entity may stay `internal` and non-sealed when it declares at least one `protected` (or
`protected internal`) member: the rules an application-layer aggregate inherits. The subclass lives in a downstream
project that no single-compilation analyzer can see, so the `protected` member — the only accessibility a subclass in
another assembly can reach — is what the analyzer checks. An `internal` non-sealed type without one is flagged.

**Fix.** Declare the record `internal sealed`.

```csharp
// Before
public record ProductProperties(
    ProductId ProductId,
    Sku Sku,
    string Name,
    int Stock
) : IProductEntityProperties;

// After
internal sealed record ProductProperties(
    ProductId ProductId,
    Sku Sku,
    string Name,
    int Stock
) : IProductEntityProperties;
```

---

## AXIS0605

**Route-injected command property is not `[JsonIgnore]`** · warning · `Axis.Edge`

**Flags** an assignment inside a `with` expression over a command or query (a type that implements `IAxisRequest`)
whose value is a parameter of the enclosing method — a route or query-string value — when the target property does
not carry `System.Text.Json.Serialization.JsonIgnoreAttribute`. Implicit conversions are looked through, so a
value-object cast of the parameter counts. A local, a field or a literal is not route-bound and is not flagged, and an
object initializer (`new GetOrderQuery { OrderId = orderId }`) is not inspected.

**Why.** Without `[JsonIgnore]` the request body can also send the property: the deserializer fills it, the `with`
expression overwrites it with the route value, and the body value is dropped without a word. The route is the single
source of a route-bound id ([Route-injected command properties](edge.md#route-injected-command-properties)).

**Fix.** Mark the property `[JsonIgnore]`, nullable with an `init` accessor, so only the edge sets it.

```csharp
public sealed record CheckoutProductCommand : IAxisCommand<CheckoutProductResponse>
{
    public string? CartId { get; init; }

    [JsonIgnore]
    public string? ProductId { get; init; }   // set by the controller: command with { ProductId = productId }

    public int? Quantity { get; init; }
}
```

A command whose id only ever travels in the body has no route segment to collide with and keeps the property
serializable.

---

## AXIS0606

**Entity properties record crams its constructor parameters onto shared lines** · warning · `Axis.Style`

**Flags** a record class with two or more primary-constructor parameters that implements an interface named
`I…Properties` from **any** assembly — the domain's `{Entity}Properties` record and a repository's `{Entity}DbEntity`
alike — when a parameter starts on the line of the record's name, or two parameters share a line. It reports once per
record, on the parameter list. A record struct is a value object and is not inspected; a single-parameter record
needs no wrapping.

**Why.** The primary constructor of these records is the persisted shape of the entity — and, for a `DbEntity`, the
column order it maps. One parameter per line lets a reader scan that shape vertically. No `.editorconfig` or formatter
setting wraps positional-record parameters that way, so the guarantee is an analyzer
([Entity records](style.md#entity-records)).

**Fix.** Put each parameter on its own line. The closing parenthesis on its own line and the matching layout of the
`FromReader` call are style the analyzer does not check.

```csharp
// Before
internal sealed record CartItemDbEntity(CartId CartId, ProductId ProductId, int Quantity)
    : ICartItemEntityProperties;

// After
internal sealed record CartItemDbEntity(
    CartId CartId,
    ProductId ProductId,
    int Quantity
) : ICartItemEntityProperties;
```

From [`CartItemDbEntity`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Adapters/Scaffolds.ECommerce.Adapters.Driven.Repository/Catalog/CartItems/CartItemDbEntity.cs).

---

## AXIS0607

**More than one public `DependencyInjection` class in the same assembly** · warning · `Axis.Architecture`

**Flags** every `public` class named exactly `DependencyInjection` declared in a project, when the project declares
more than one. The diagnostic is computed at the end of the compilation, so the build always reports it; an editor
shows it only when it analyzes the whole solution.

**Why.** A project exposes exactly one public registration extension, at its root: that is the only door another
project calls. A project that spans several bounded contexts decomposes the entry into one `DependencyInjection` per
context or feature folder that registers services, and each of those stays `internal`, called only by the root
([One registration entry per project](architecture.md#one-registration-entry-per-project)).

**Fix.** Keep the root class `public` and make every per-context one `internal static`.

```csharp
// The project's single public entry
namespace Scaffolds.ECommerce.Application;
public static class DependencyInjection { … }

// A per-context decomposition, called only by the root
namespace Scaffolds.ECommerce.Application.Auth;
internal static class DependencyInjection { … }
```

---

## AXIS0608

**`AxisError` rebuilt at the call site from an errors-class `const`** · warning · `Axis.Style`

**Flags** a call to an `AxisError` factory (`AxisError.NotFound`, `Conflict`, `BusinessRule`…) whose argument is a
`const string` declared on a static class whose name ends in `Errors`, made outside that class. It reports once per
call, so a code rebuilt in two places is flagged twice. It does not flag the factory call inside the errors class
itself (that is the declaration), a call site that returns a ready member, a `const` used as a raw code (the
validator helpers take one), a `const` from a class not named `*Errors`, or a code composed from runtime context.

**Why.** A failure is a code **and** a category, and the category is what the edge renders as an HTTP status.
Building the `AxisError` at each call site lets the same code leave one handler as a 500 and another as a 400. The
errors class declares the finished error, and call sites return it ([Error member shape](style.md#error-member-shape)).

**Fix.** Declare a `public static readonly AxisError` member — or a static method returning `AxisError` when the code
carries runtime context — and return it. Keep a `const string` only for a code that the validator helpers consume.

```csharp
// Before
public static class ProductErrors
{
    public const string INSUFFICIENT_STOCK = "INSUFFICIENT_STOCK";
}
… .EnsureAsync(p => p.Stock >= quantity, AxisError.BusinessRule(ProductErrors.INSUFFICIENT_STOCK))   // AXIS0608

// After
public static class ProductErrors
{
    public static readonly AxisError InsufficientStock = AxisError.BusinessRule("INSUFFICIENT_STOCK");
}
… .EnsureAsync(p => p.Stock >= quantity, ProductErrors.InsufficientStock)
```

The analyzer covers the decidable half of the convention. A category chosen wrongly, or a code that should have been
parameterized, is for review.
