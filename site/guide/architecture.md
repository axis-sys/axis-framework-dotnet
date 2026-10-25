# Architecture

An Axis application is a **hexagon split into projects** and **vertical-sliced by bounded context** inside each
project. Every business operation is a CQRS use case (command or query, response, handler, optional validator)
exposed by a **facade**; every piece of infrastructure is a **port** whose adapter is chosen once, at the
**composition root**. The point of the shape is that each decision has exactly one place to live, so a slice can
be found by its folder name, reviewed in isolation and, one day, moved into its own service without renaming
anything.

The conventions below are all followed by the reference application, a small e-commerce whose Auth,
Authorization, Catalog, Customers and EmailValidation bounded contexts (plus an Admin area for schema migration)
run in one host over Postgres or MySQL — solution
[`AxisFramework.slnx`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/AxisFramework.slnx), with the projects under `scaffolds/`.
Each section links the files that show the convention; when the reference application has no example yet, the
section says so.

## Map

| Section | You want to… |
|---|---|
| [Solution topology](#solution-topology) | lay out the projects and the direction of their dependencies |
| [Bounded contexts and how they talk](#bounded-contexts-and-how-they-talk) | draw a boundary and pick the channel between two contexts |
| [Vertical slices and folder layout](#vertical-slices-and-folder-layout) | know where a file goes and what its namespace is |
| [Contracts](#contracts) | declare a command, query, response, facade or saga payload |
| [Facades](#facades) | expose the use cases of an aggregate |
| [Handlers](#handlers) | write the body of a use case |
| [Ambient context](#ambient-context) | read the caller, honour cancellation, carry a tenant |
| [Dependency injection and the composition root](#dependency-injection-and-the-composition-root) | register a project and wire the host |
| [Services and policies](#services-and-policies) | share logic between use cases, bind options |
| [Validators](#validators) | validate the input of a use case |
| [Swappable infrastructure ports](#swappable-infrastructure-ports) | talk to e-mail, tokens, identity, cache, any SDK |
| [Bus events](#bus-events) | tell other contexts that something happened |
| [Cache-aside](#cache-aside) | cache a hot read and invalidate it |
| [Sagas](#sagas) | run a multi-context process with compensation |
| [Workers](#workers) | run background work on a schedule |
| [Access modifiers](#access-modifiers) | decide what is `public` |

Related articles: [Domain](domain.md) (aggregates, value objects) · [Persistence](persistence.md) (repositories,
SQL, migrations, unit of work) · [HTTP edge](edge.md) (controllers, OpenAPI, auth) · [Testing](testing.md) ·
[Style](style.md) (naming, errors, formatting) · [Process](process.md) (comments, configuration, docs).

---

## Solution topology

The solution is organized by **hexagonal role at the project level** and by **bounded context inside each
project**. The roles are separate projects — never folders inside one project — grouped into five sibling
solution folders:

```
scaffolds/
├─ Core/       Scaffolds.ECommerce.SharedKernel · .Domain · .Application
├─ Contracts/  Scaffolds.ECommerce.Contracts.Driving · .Contracts.Driven
├─ Adapters/   Scaffolds.ECommerce.Adapters.Driving.Facade · .Adapters.Driven.{Repository, Postgres, MySql, Jwt, AuthProviders, InMemory}
├─ Host/       Scaffolds.ECommerce.Host — the one composition root and the controllers
└─ Tests/      Scaffolds.ECommerce.{Unit, Integration, E2E, Architecture}Tests
```

`Contracts/` is a **sibling** of `Core/`, never nested inside it.

| Project | Holds | References (application projects) |
|---|---|---|
| `{App}.SharedKernel` | value objects and ids, bus events, shared errors and constants every context may read | none |
| `{App}.Domain` | entity property interfaces and properties ([Domain](domain.md)) | SharedKernel |
| `{App}.Application` | behavior: handlers, validators, factories, services, event handlers, sagas, pipeline behaviors | SharedKernel, Domain, both contracts |
| `{App}.Contracts.Driving` | driving ports: commands, queries, responses, facade interfaces, saga payloads | SharedKernel |
| `{App}.Contracts.Driven` | driven ports: reader/write ports, the unit of work, app-owned infra ports, `{Aggregate}Errors` | Domain, SharedKernel |
| `{App}.Adapters.Driving.Facade` | the facade implementations | Contracts.Driving, Application |
| `{App}.Adapters.Driven.Repository` | provider-agnostic repositories | Domain, SharedKernel, Contracts.Driven |
| `{App}.Adapters.Driven.Postgres` / `.MySql` | provider-specific SQL seams and the provider wiring | Adapters.Driven.Repository |
| `{App}.Adapters.Driven.{Role}` | other driven adapters (Jwt, AuthProviders, InMemory) | only what they implement: Jwt → Application, Contracts.Driven; AuthProviders → SharedKernel, Contracts.Driven; InMemory → none |
| `{App}.Host` | the composition root, the controllers, the `migrate` verb | everything it wires |

- **Dependencies point inward only.** Adapters depend on contracts and core; core never references an adapter;
  only the host wires adapters. The direction is executable:
  [`HexagonalBoundaryTests`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Tests/Scaffolds.ECommerce.ArchitectureTests/HexagonalBoundaryTests.cs)
  asserts by reflection that the shared kernel references no other project, the domain only the shared kernel,
  the application no adapter nor the host, and the driving contracts no domain, application or adapter;
  [`FacadeBoundaryTests`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Tests/Scaffolds.ECommerce.ArchitectureTests/FacadeBoundaryTests.cs)
  guards the facade adapter. A violation fails the build's test run.
- **There is no "ports" project.** Driving ports are the facades in the driving contracts; driven ports live in
  the driven contracts.
- **Entity property interfaces live in the domain**
  ([`IProductEntityProperties`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Domain/Catalog/Products/IProductEntityProperties.cs)),
  and the provider-agnostic repository adapter references the domain to consume them.
- **Only the host references the driven adapters**
  ([`Scaffolds.ECommerce.Host.csproj`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Host/Scaffolds.ECommerce.Host/Scaffolds.ECommerce.Host.csproj)).
- **Inside every project** folders are bounded context → aggregate → feature, and the namespace mirrors the folder
  path exactly. Because of that, promoting a bounded-context folder to its own project (a microservice) keeps every
  namespace unchanged: the folder simply becomes a project. Promotion is optional and driven by deployment needs —
  a context not yet promoted stays a folder.

Project naming (`{App}.Contracts.Driving`, `{App}.Adapters.Driving.{Role}`…) is covered in [Style](style.md); the
four test projects in [Testing](testing.md).

---

## Bounded contexts and how they talk

Decide the structural things **before** writing code, because they are the expensive ones to reverse: the
bounded-context boundary, its subdomain class (core, supporting or generic — an input from product planning), where
each element lives, the channel each context uses to reach another, and the host model (see
[the composition root](#the-composition-root)).

A bounded context never reaches into another context's repository ports or internals. It has exactly three
channels:

| The need | Channel | In the reference application |
|---|---|---|
| a synchronous read or write in another context | that context's **public facade** | [`RequestTokenHandler`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Application/Auth/UseCases/RequestToken/v1/RequestTokenHandler.cs) (Auth) calls `ICustomersFacade.EnsureCustomerAsync` (Customers) |
| a fact others may react to; the publisher must not know them | a **bus event** ([below](#bus-events)) | [`CheckoutProductHandler`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Application/Catalog/Products/UseCases/CheckoutProduct/v1/CheckoutProductHandler.cs) publishes `ProductCheckedOutEvent` without knowing who consumes it |
| a multi-context process that must undo its work on failure | a **saga** ([below](#sagas)) | [`ValidateEmailSaga`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Application/EmailValidation/Sagas/ValidateEmail/ValidateEmailSaga.cs) marks the email in Customers and removes the code in EmailValidation |

| Do | Don't |
|---|---|
| inject the other context's `I…Facade`, straight from the driving contracts | inject another context's reader or write port |
| define a local port and implement it over the foreign facade when the consumer should stay decoupled from it | call another context's handlers, factories or services |
| read another context's shared-kernel value objects and constants — the shared kernel is a leaf | write another context's data through its write port — not even inside a saga |
| let each saga stage reach the context that owns the data through that context's facade | pass anything between stages except the saga payload |

The one sanctioned shortcut: a **saga stage handler may read a sibling context's reader port** directly, because
the saga is the cross-context orchestration; its writes still go through the owning context.
[`GrantsCacheService`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Application/Authorization/Services/GrantsCache/GrantsCacheService.cs)
shows the everyday mix: Authorization reads its own `IMembersReaderPort` and reaches Customers only through
`ICustomersFacade`.

### Sequencing work across several contexts

When a change spans several contexts and is split among people working in parallel:

1. Build the **consumption graph** first. An edge means one context consumes another: calls its facade, reads its
   reader port from a saga stage, or subscribes to its bus event. Reading a shared kernel is not an edge.
2. Run two contexts in parallel only when there is **no edge between them in either direction**.
3. A consuming context starts as soon as the **specific piece** it consumes — the facade member, the saga stage,
   the event record — exists in the producer, not when the whole producer is finished. A context with no edge onto
   another starts immediately, even if it will be consumed later.
4. Write the graph and the resulting waves into the plan; never leave them implicit.

---

## Vertical slices and folder layout

The same `{BC}/{Aggregate}` tree repeats in every project, so one slice is traceable by folder name across all
layers. For the Catalog context's Products aggregate:

| Layer | Folder | Namespace |
|---|---|---|
| shared kernel | [`SharedKernel/Catalog/Products/ValueObjects/`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.SharedKernel/Catalog/Products/ValueObjects) | `…SharedKernel.Catalog.Products.ValueObjects` |
| domain | [`Domain/Catalog/Products/`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Domain/Catalog/Products) | `…Domain.Catalog.Products` |
| application | [`Application/Catalog/Products/UseCases/RegisterProduct/v1/`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Application/Catalog/Products/UseCases/RegisterProduct/v1) | `…Application.Catalog.Products.UseCases.RegisterProduct.v1` |
| driving contracts | [`Contracts.Driving/Catalog/Products/v1/RegisterProduct/`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Contracts/Scaffolds.ECommerce.Contracts.Driving/Catalog/Products/v1/RegisterProduct) | `…Contracts.Driving.Catalog.Products.v1.RegisterProduct` |
| driven contracts | [`Contracts.Driven/Catalog/Products/`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Contracts/Scaffolds.ECommerce.Contracts.Driven/Catalog/Products) | `…Contracts.Driven.Catalog.Products` |
| facade adapter | [`Adapters.Driving.Facade/Catalog/Products/`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Adapters/Scaffolds.ECommerce.Adapters.Driving.Facade/Catalog/Products) | `…Adapters.Driving.Facade.Catalog.Products` |
| repository adapter | [`Adapters.Driven.Repository/Catalog/Products/`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Adapters/Scaffolds.ECommerce.Adapters.Driven.Repository/Catalog/Products) | `…Adapters.Driven.Repository.Catalog.Products` |

- **The namespace equals the folder path** — every segment, including `v1`. No file declares anything else.
- **Every feature gets its own `{BC}/{Feature}` folder** in the domain, the driven contracts, the driven adapters
  and the shared kernel. A single-table context still does: Customers lives in
  [`Domain/Customers/Customers/`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Domain/Customers/Customers),
  never collapsed into the context root. The application and the driving contracts drop the aggregate segment for
  a single-aggregate context (`Customers/UseCases/…`, `Customers/v1/…`).
- **No grab-bag folders.** `Common/`, `Helpers/`, `Enumerators/` never group unrelated features. Types shared by
  several contexts live in the shared kernel
  ([`SharedKernel/Shared/`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.SharedKernel/Shared)),
  not under a feature folder. The only non-feature files allowed at a context root are its `DbInit`, its
  `DependencyInjection`
  ([`Repository/Catalog/`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Adapters/Scaffolds.ECommerce.Adapters.Driven.Repository/Catalog))
  and, in the application, its [options](#options) class
  ([`Application/Catalog/`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Application/Catalog)).
- **A child entity or association nests under its owning aggregate**, in a folder named for the relationship
  (`{Children}Of{Parent}`) with its own full file set. *(Not yet exemplified in the reference application.)*
- **A driven adapter family is its own project** under `Adapters/`. Provider-specific code splits into sibling
  projects that reference the shared adapter: `Adapters.Driven.Postgres` and `.MySql` implement the
  [`IMemberSqlDialect`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Adapters/Scaffolds.ECommerce.Adapters.Driven.Repository/Authorization/Members/IMemberSqlDialect.cs)
  seam of `Adapters.Driven.Repository` ([Persistence](persistence.md)). Each folder level that registers services
  has exactly one `DependencyInjection` ([below](#one-registration-entry-per-project)).

### The application slice

```
Scaffolds.ECommerce.Application/
├─ DependencyInjection.cs              the project's only public registration entry
├─ Pipelines/                          cross-cutting pipeline behaviors
└─ {BC}/
   ├─ DependencyInjection.cs           internal, per context
   ├─ {Area}Options.cs                 the context's options, bound by the host
   └─ [{Aggregate}/]
      ├─ UseCases/{UseCase}/v1/        {UseCase}Handler.cs · {UseCase}Validator.cs
      ├─ Aggregates/                   {Aggregate}Factory.cs
      ├─ Services/{Name}/              I{Name}Service.cs · {Name}Service.cs
      ├─ Policies/                     I{Name}Policy · {Name}Policy
      ├─ Events/                       {Event}Handler.cs (bus consumers)
      └─ Sagas/{Saga}/                 {Saga}Saga.cs · {Saga}PayloadFactory.cs · Handlers/
```

- A use-case folder is `{BC}/{Aggregate}/UseCases/{UseCase}/v1/` in a multi-aggregate context
  ([`CheckoutProduct/v1/`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Application/Catalog/Products/UseCases/CheckoutProduct/v1))
  and `{BC}/UseCases/{UseCase}/v1/` in a single-aggregate one
  ([`Auth/UseCases/RequestToken/v1/`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Application/Auth/UseCases/RequestToken/v1)).
- It holds **only** `{UseCase}Handler` and, when the input needs it, `{UseCase}Validator`; the class name equals
  the file name. The command, query and response live in the [driving contracts](#contracts).
- Supporting components sit in sibling folders, never inside a use-case folder: factories in `Aggregates/`
  ([`ProductFactory`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Application/Catalog/Products/Aggregates/ProductFactory.cs)),
  services in `Services/`
  ([`AuthTokenIssuer/`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Application/Auth/Services/AuthTokenIssuer)),
  policies in `Policies/`, bus consumers in `Events/`
  ([`Catalog/CartItems/Events/`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Application/Catalog/CartItems/Events)),
  sagas in `Sagas/{Saga}/`. Application-wide pipeline behaviors sit in
  [`Pipelines/`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Application/Pipelines).
- The `{Aggregate}Errors` class is not application code: it sits beside the ports in the driven contracts
  ([`ProductErrors`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Contracts/Scaffolds.ECommerce.Contracts.Driven/Catalog/Products/ProductErrors.cs);
  [Style](style.md) covers its shape).
- **One top-level type per file**, with one exception: an interface and its *only* implementation declared in the
  same project may share the implementation's file — named `{Name}.cs`, the interface `I{Name}` first, nothing
  else in it ([`ProductFactory.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Application/Catalog/Products/Aggregates/ProductFactory.cs)
  declares `IProductFactory` then `ProductFactory`).
- An interface declared in one project and implemented in another — a driven port and its adapter, a facade
  interface and its implementation — always has its own file
  ([`IProductsWritePort`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Contracts/Scaffolds.ECommerce.Contracts.Driven/Catalog/Products/IProductsWritePort.cs)
  vs [`ProductsRepository`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Adapters/Scaffolds.ECommerce.Adapters.Driven.Repository/Catalog/Products/ProductsRepository.cs)).
  The project decides, never the visibility.

---

## Contracts

### Where each contract lives

| Contract | Project | Folder |
|---|---|---|
| command, query, response, sub-DTOs | Contracts.Driving | `{BC}/[{Aggregate}/]v1/{UseCase}/` |
| facade interface, cross-use-case items | Contracts.Driving | `{BC}/[{Aggregate}/]v1/` (the version root) |
| DTO shared by several use cases | Contracts.Driving | `{BC}/[{Aggregate}/]v1/_Shared/` |
| saga payload, saga protocol records, saga-lifecycle events | Contracts.Driving | `{BC}/[{Aggregate}/]v1/Sagas/{Saga}/` |
| bus event | SharedKernel | `{BC}/Events/v1/` of the **publishing** context |
| reader/write ports, infra ports, `{Aggregate}Errors` | Contracts.Driven | `{BC}/{Aggregate}/`; a context's infra ports at `{BC}/` ([`Auth/`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Contracts/Scaffolds.ECommerce.Contracts.Driven/Auth)), the cross-context [`IUnitOfWork`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Contracts/Scaffolds.ECommerce.Contracts.Driven/IUnitOfWork.cs) at the root |

- No command, query or response is declared in the application — the application holds behavior only.
- The driving contracts project references only the [`AxisMediator.Contracts`](https://github.com/axis-sys/axis-mediator-contracts)
  package and the shared kernel — never the domain, the application, the driven contracts or an adapter
  ([`Contracts.Driving.csproj`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Contracts/Scaffolds.ECommerce.Contracts.Driving/Scaffolds.ECommerce.Contracts.Driving.csproj),
  asserted by `HexagonalBoundaryTests`). **Why:** controllers, sibling contexts and tests consume the contracts
  without taking a dependency on behavior.
- A saga-internal composition DTO that never crosses the context edge may stay in the application.

### Shape: primitive DTOs

```csharp
public sealed record EnsureCustomerCommand : IAxisCommand<EnsureCustomerResponse>
{
    public string? ExternalId { get; init; }

    public string? Email { get; init; }

    public string? Name { get; init; }

    public string? Provider { get; init; }
}
```

```csharp
public sealed record GetCustomerResponse : IAxisQueryResponse
{
    public required string CustomerId { get; init; }

    public required string Email { get; init; }

    public required string Name { get; init; }

    public required bool EmailValidated { get; init; }

    public required bool IsAdmin { get; init; }

    public required string? ExternalId { get; init; }
}
```

From [`EnsureCustomerCommand`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Contracts/Scaffolds.ECommerce.Contracts.Driving/Customers/v1/EnsureCustomer/EnsureCustomerCommand.cs)
and [`GetCustomerResponse`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Contracts/Scaffolds.ECommerce.Contracts.Driving/Customers/v1/GetCustomer/GetCustomerResponse.cs).

- A command is `public sealed record {UseCase}Command : IAxisCommand<{UseCase}Response>` (or `IAxisCommand` when
  it returns nothing); a query is `{UseCase}Query : IAxisQuery<{UseCase}Response>`; a response implements
  `IAxisCommandResponse` or `IAxisQueryResponse`.
- **Input** properties are nullable primitives with `init`, or collections defaulted to empty, so a missing field
  is reported by the [validator](#validators), not by the type system.
- **Output** properties are `required` with `init`, non-nullable unless the value is genuinely optional.
- Ids, slugs and codes cross the boundary as **strings**; validators and handlers parse them into value objects,
  and responses render value objects back as strings.
- Only strings, booleans, numbers, dates, collections and nested contract DTOs — no domain or driven-contract type
  ever appears in a driving contract.
- The contracts a controller exposes carry XML doc comments that feed the API reference
  ([`CheckoutProductCommand`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Contracts/Scaffolds.ECommerce.Contracts.Driving/Catalog/Products/v1/CheckoutProduct/CheckoutProductCommand.cs));
  a contract only a facade reaches, such as `EnsureCustomerCommand`, carries none ([Process](process.md),
  [HTTP edge](edge.md#xml-docs-feed-openapi)). A nested row DTO is declared inside or beside its response.
- A sub-DTO shared by a use case sits beside its command, follows the input shape and implements no CQRS marker.
  Positional records are for events and internal saga DTOs, never for commands or queries.

### Versioning

- The path is `{BC}/{Aggregate}/v1/{UseCase}/` holding `{UseCase}Command` or `{UseCase}Query` plus
  `{UseCase}Response`
  ([`Catalog/Products/v1/CheckoutProduct/`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Contracts/Scaffolds.ECommerce.Contracts.Driving/Catalog/Products/v1/CheckoutProduct)),
  with the aggregate segment omitted for a single-aggregate context
  ([`EmailValidation/v1/ValidateEmail/`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Contracts/Scaffolds.ECommerce.Contracts.Driving/EmailValidation/v1/ValidateEmail)).
- The facade and the cross-use-case items sit at the `v1` root; a second aggregate of the same context gets its own
  `{Aggregate}/v1` tree and facade
  ([`Catalog/Orders/v1/IOrdersFacade`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Contracts/Scaffolds.ECommerce.Contracts.Driving/Catalog/Orders/v1/IOrdersFacade.cs)).
- `Sagas/` is the reserved sibling folder for saga payloads and their lifecycle events. Bus events never live
  here — they live in the shared kernel.
- A sub-DTO used both by a use case and by a saga payload lives in the use-case folder that owns it and is
  referenced from the payload.
- Use-case names are verb + noun. Queries are `Get*`, `List*` or `Search*`; everything else is a command.
- **Never rename or repurpose a `v1` type.** A breaking shape change creates a `v2` sibling tree, so existing
  consumers keep compiling against `v1`.

---

## Facades

The facade is the **only public doorway** into a context's use cases. Controllers, other contexts and unit tests
all enter through it.

```csharp
public interface IProductsFacade
{
    Task<AxisResult<GetProductResponse>> GetProductAsync(GetProductQuery query);
    Task<AxisResult<CreateProductResponse>> CreateProductAsync(CreateProductCommand command);
    Task<AxisResult<RegisterProductResponse>> RegisterProductAsync(RegisterProductCommand command);
    Task<AxisResult<CheckoutProductResponse>> CheckoutProductAsync(CheckoutProductCommand command);
}
```

```csharp
internal sealed class ProductsFacade(IAxisMediator mediator) : IProductsFacade
{
    public Task<AxisResult<GetProductResponse>> GetProductAsync(GetProductQuery query)
        => mediator.Cqrs.QueryAsync<GetProductQuery, GetProductResponse>(query);

    public Task<AxisResult<CreateProductResponse>> CreateProductAsync(CreateProductCommand command)
        => mediator.Cqrs.ExecuteAsync<CreateProductCommand, CreateProductResponse>(command);

    public Task<AxisResult<RegisterProductResponse>> RegisterProductAsync(RegisterProductCommand command)
        => mediator.Cqrs.ExecuteAsync<RegisterProductCommand, RegisterProductResponse>(command);

    public Task<AxisResult<CheckoutProductResponse>> CheckoutProductAsync(CheckoutProductCommand command)
        => mediator.Cqrs.ExecuteAsync<CheckoutProductCommand, CheckoutProductResponse>(command);
}
```

From [`IProductsFacade`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Contracts/Scaffolds.ECommerce.Contracts.Driving/Catalog/Products/v1/IProductsFacade.cs)
and [`ProductsFacade`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Adapters/Scaffolds.ECommerce.Adapters.Driving.Facade/Catalog/Products/ProductsFacade.cs).

- **One public interface per aggregate** (or per single-aggregate context), named `I{Entities}Facade` — the plural
  of the aggregate entity, mirroring `I{Entities}ReaderPort` / `I{Entities}WritePort` — at the versioned root of the
  contract feature folder.
- **Methods map one-to-one to use cases**, are named `{Verb}[{Noun}]Async`, take exactly one parameter (the command
  or query, never a `CancellationToken`) and return `Task<AxisResult<{UseCase}Response>>`.
- **The implementation is an `internal sealed {Entities}Facade`** that injects only `IAxisMediator` through its
  primary constructor and contains zero logic — no mapping, validation or branching, one dispatch expression per
  method.
- Commands dispatch through `mediator.Cqrs.ExecuteAsync`, queries through `mediator.Cqrs.QueryAsync`
  ([`AxisMediator`](https://github.com/axis-sys/axis-mediator)); the facades are the only callers of that surface.
- **Controllers depend on facades, never on the mediator** — `FacadeBoundaryTests` asserts every controller
  constructor parameter is a facade ([HTTP edge](edge.md#controllers-depend-on-facades-only)). Handlers never
  dispatch other use cases: reuse goes through factories, services or another context's facade.
- **The implementations live in `{App}.Adapters.Driving.Facade`**, which references only the driving contracts and
  the application — never repositories, the host or driven adapters. `FacadeBoundaryTests` proves each facade is
  implemented exactly once there and never in the application.
- **One DI extension registers the mediator plus every facade as scoped**
  ([`AddECommerceFacade`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Adapters/Scaffolds.ECommerce.Adapters.Driving.Facade/DependencyInjection.cs)).

Exceptions: a read-only lookup facade may expose a single list method; a process-style facade that fronts a whole
context rather than one entity is named after the context or process
([`IAuthFacade`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Contracts/Scaffolds.ECommerce.Contracts.Driving/Auth/v1/IAuthFacade.cs),
[`IEmailValidationFacade`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Contracts/Scaffolds.ECommerce.Contracts.Driving/EmailValidation/v1/IEmailValidationFacade.cs)).

**Why:** the facade is a boundary contract, not an HTTP convenience. The same interface serves controllers,
synchronous cross-context calls inside the core and unit tests
([`CatalogTestHost`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Tests/Scaffolds.ECommerce.UnitTests/CatalogTestHost.cs)
builds the real application and resolves a facade). Handlers stay internal; the facade plus assembly scanning is
what makes them reachable.

---

## Handlers

```csharp
internal sealed class CheckoutProductHandler(
    IAxisMediator mediator,
    IProductsWritePort productsWrite,
    IAxisBus bus,
    IUnitOfWork unitOfWork
) : IAxisCommandHandler<CheckoutProductCommand, CheckoutProductResponse>
{
    public Task<AxisResult<CheckoutProductResponse>> HandleAsync(CheckoutProductCommand command)
    {
        var productId = (ProductId)command.ProductId!;

        return mediator.AxisEntityId.Rop()
            .Ensure(caller => caller.HasValue, SharedErrors.CustomerIdentityRequired)
            .ThenAsync(_ => unitOfWork.RenewAsync())
            .ThenAsync(_ => productsWrite.GetByIdForUpdateAsync(productId))
            .EnsureAsync(product => product.Stock >= command.Quantity, ProductErrors.InsufficientStock)
            .ThenAsync(product => productsWrite.ReserveStockAsync(product.ProductId, command.Quantity!.Value))
            .ThenAsync(product => bus.PublishAsync(
                    new ProductCheckedOutEvent(
                        command.CartId!,
                        product.ProductId,
                        command.Quantity!.Value
                    ), ProductCheckedOutEvent.Topic))
            .ThenAsync(_ => unitOfWork.SaveChangesAsync())
            .MapAsync(product => new CheckoutProductResponse
            {
                CustomerId = mediator.AxisEntityId!.Value,
                ProductId = product.ProductId,
            });
    }
}
```

From [`CheckoutProductHandler`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Application/Catalog/Products/UseCases/CheckoutProduct/v1/CheckoutProductHandler.cs).

- **`internal sealed {UseCase}Handler`**, one per use case, implementing `IAxisCommandHandler<,>` or
  `IAxisQueryHandler<,>` from [`AxisMediator.Contracts`](https://github.com/axis-sys/axis-mediator-contracts).
- **Dependencies through the primary constructor** — no fields, no constructor body.
- **`HandleAsync` takes the command or query as its only parameter** and returns `Task<AxisResult<{UseCase}Response>>`
  (or `Task<AxisResult>` for a valueless command). It never throws for flow control and takes **no
  `CancellationToken`** — cancellation is [ambient](#ambient-context).
- **Value-object casts from the already-validated input come first**
  (`(ProductId)command.ProductId!`); the rest of the method is a **single railway chain**. Private helpers are
  `static` where possible
  ([`MarkEmailValidatedHandler`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Application/Customers/UseCases/MarkEmailValidated/v1/MarkEmailValidatedHandler.cs)).
- **The response is built in the final `Map`.** A handler never returns a domain entity or a property interface.
- **Wall-clock time comes from an injected `TimeProvider`**, never `DateTime.Now` or `DateTimeOffset.Now`
  ([`AuthTokenIssuerService`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Application/Auth/Services/AuthTokenIssuer/AuthTokenIssuerService.cs);
  the application registers `TimeProvider.System` with `TryAddSingleton`, so a host's earlier registration wins).
- A command handler commits with **one `SaveChangesAsync` inside its chain**; queries and factories never commit
  ([Persistence](persistence.md)).

The chain itself rides [`AxisResult`](https://github.com/axis-sys/axis-result): return a typed `AxisError` instead
of throwing, compose with [`Then`](https://github.com/axis-sys/axis-result/blob/main/docs/then.md) /
[`Map`](https://github.com/axis-sys/axis-result/blob/main/docs/map.md) /
[`Ensure`](https://github.com/axis-sys/axis-result/blob/main/docs/ensure.md) instead of branching on `IsSuccess`,
never read `.Value` without proof of success, and keep `try/catch` off the track — the
[analyzers](https://github.com/axis-sys/axis-result/blob/main/docs/analyzers.md) flag each of these.

### Authorization stays at the edge

A handler runs behind the driving edge's authentication and authorization gate (`[Authorize]`, the auth
middleware). It reads the ambient identity for **business** use — stamping an actor, scoping a query — and does
not re-check what the edge already checked: no re-validating a bearer token, no re-asserting a role the policy
already required.

Two cases legitimately produce auth errors from inside a handler:

| Case | In the reference application |
|---|---|
| the handler **is** the authentication mechanism — token issuance, refresh-token consumption | [`RequestTokenHandler`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Application/Auth/UseCases/RequestToken/v1/RequestTokenHandler.cs) fails with `AuthErrors.ExternalTokenInvalid` when the external identity is unusable |
| a domain-level permission decision the edge cannot express — per-resource ownership, a tenant-scoped grant, an account-disabled gate | [`GetOrderHandler`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Application/Catalog/Orders/UseCases/GetOrder/v1/GetOrderHandler.cs) answers `OrderNotFound` for an order of another customer |

The distinction is contextual and applied in review: the smell is duplicating the edge's gate, never the auth
error itself. Edge-side permissions are in [HTTP edge](edge.md#permission-authorization).

---

## Ambient context

Execution context flows **around** the call, not through it.

| Concern | Read it from | Never |
|---|---|---|
| the caller's identity | `IAxisMediator.AxisEntityId` — `null` means anonymous or system | a parameter of a command, query or `HandleAsync`; `IAxisMediatorContextAccessor` or the HTTP context inside a handler |
| cancellation | the mediator pipeline (AsyncLocal) | a parameter of `HandleAsync`, a facade method, or a port a handler consumes |
| an app-scoped dimension, e.g. a tenant | a single scoped accessor read inside the handler | a parameter |

**The edge writes, the application reads.** Middleware placed right after authentication maps the principal's id
claim to `AxisEntityId` through `AxisEntityId.TryParse` — a missing or invalid claim leaves it `null`:

```csharp
app.UseAuthentication();
app.UseAxisAmbientIdentity(AuthClaimTypes.CustomerId);
app.UseAuthorization();
```

From [`Program.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Host/Scaffolds.ECommerce.Host/Program.cs);
the middleware ships with [`AxisResult.HttpResponse`](https://github.com/axis-sys/axis-result-httpresponse).
Within a request only that middleware writes `IAxisMediatorContextAccessor.AxisEntityId`; the application depends
only on `IAxisMediator`, the single read seam, which is nullable and never throws. A handler that needs the caller
injects `IAxisMediator` and guards the absence on the rail:

```csharp
var callerId = mediator.AxisEntityId is { } caller ? (CustomerId?)caller.ToString() : null;

return callerId.Rop()
    .Ensure(customerId => customerId.HasValue, SharedErrors.CustomerIdentityRequired)
```

From [`RequestEmailValidationHandler`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Application/EmailValidation/UseCases/RequestEmailValidation/v1/RequestEmailValidationHandler.cs);
pipeline behaviors read the same seam
([`ActorStampBehavior`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Application/Pipelines/ActorStampBehavior.cs)).

- A command or query never carries the caller's **own** identity. An operation that **targets** a specific
  principal carries that target explicitly as a value — `UpdateGrantsCommand.CustomerId` is the member whose
  grants change, taken from the route.
- Driven ports consumed by handlers declare no `CancellationToken`
  ([`ICustomersReaderPort`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Contracts/Scaffolds.ECommerce.Contracts.Driven/Customers/Customers/ICustomersReaderPort.cs)).
- Application pipelines do not use the cancellation-token-aware `AxisResult` overloads; those
  ([Cancellation](https://github.com/axis-sys/axis-result/blob/main/docs/cancellation.md)) exist for code running
  outside a mediator scope, where cancellation is passed explicitly.
- **Exception:** entry points that run outside a mediator scope — worker loops, a service's
  `RunOnceAsync(CancellationToken)` — do take an explicit token, because there is no ambient pipeline to carry it.
- A background, saga or worker flow establishes the app-scoped context (the tenant) through an
  `AxisResult`-returning write before dispatching scoped work. To attribute a user it sets the mediator context
  accessor in its own scope, or resolves the user from its own input data. A resumed saga stage re-establishes it
  from the payload ([stage handlers](#stage-handlers)).
- The trace id is generated by the framework; correlation ids stay unpopulated until telemetry is turned on, and the
  same edge middleware is their home.

**Why:** signatures stay stable, audit events carry the real user, and telemetry gets the user tag without any
handler doing anything. Multi-tenancy is not mandated — the tenant accessor is only one example of an app-scoped
dimension ([HTTP edge](edge.md#tenant-scoping-multi-tenant-applications)).

---

## Dependency injection and the composition root

### One registration entry per project

```csharp
public static class DependencyInjection
{
    public static IServiceCollection AddECommerceApplication(this IServiceCollection services)
    {
        services.AddTransient(typeof(IAxisPipelineBehavior<,>), typeof(ActorStampBehavior<,>));
        services.TryAddSingleton(TimeProvider.System);
        var assembly = typeof(DependencyInjection).Assembly;

        return services
            .AddAuthServices()
            .AddAuthorizationServices()
            .AddCatalogServices()
            .AddCustomersServices()
            .AddEmailValidationServices()
            .AddCqrsMediator(assembly)
            .AddAxisValidator(assembly)
            .AddAxisSagaHandlers(assembly);
    }
}
```

From the application's
[`DependencyInjection`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Application/DependencyInjection.cs).

- **A project exposes exactly one public registration extension** — a `public static class DependencyInjection`
  at the project root. Nothing else in the project is public for wiring; a build-time analyzer rejects a second
  public `DependencyInjection` in the same assembly.
- **A project spanning several contexts decomposes** that entry into one `internal static class DependencyInjection`
  per context or feature folder that registers services
  ([`Application/Auth/DependencyInjection`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Application/Auth/DependencyInjection.cs),
  [`Repository/Catalog/DependencyInjection`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Adapters/Scaffolds.ECommerce.Adapters.Driven.Repository/Catalog/DependencyInjection.cs)).
  The root calls each internal `Add{BC}…` in turn plus any cross-cutting registration — the shared `IUnitOfWork`
  binding in
  [`AddECommerceRepositories`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Adapters/Scaffolds.ECommerce.Adapters.Driven.Repository/DependencyInjection.cs).
  The decomposition never adds a second public door; a workers project follows the same shape ([Workers](#workers)).
- **Handlers, validators and saga stage handlers are found by assembly scans** — `AddCqrsMediator`
  ([`AxisMediator`](https://github.com/axis-sys/axis-mediator)), `AddAxisValidator`
  ([`AxisValidator.FluentValidation`](https://github.com/axis-sys/axis-validator-fluent-validation)),
  `AddAxisSagaHandlers` ([`AxisSaga`](https://github.com/axis-sys/axis-saga)) — never registered one type at a time.
- **Everything else is explicit and scoped**: factories, services, policies, application-implemented ports,
  repository ports, ambient accessors, each as an interface → implementation pair.
- **A class implementing two interfaces is registered once** and both interfaces forward to that same scoped
  instance:

  ```csharp
  services.AddScoped<ProductsRepository>();
  services.AddScoped<IProductsReaderPort>(provider => provider.GetRequiredService<ProductsRepository>());
  services.AddScoped<IProductsWritePort>(provider => provider.GetRequiredService<ProductsRepository>());
  ```

- **A default the host may override is registered with a `TryAdd`**, so the host's earlier registration wins
  (`TimeProvider.System` above).
- **Each saga contributes its own `Add{Saga}Saga` extension**, invoked from the owning context's registration,
  itself invoked from the project's single entry
  ([`EmailValidation/DependencyInjection`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Application/EmailValidation/DependencyInjection.cs)).
- **The reason behind a registration that encodes a decision** — a lifetime, a provider choice, an interface
  forward — is never written as a comment next to it. It lives in this guide or in the project's README
  ([Process](process.md)).

### The composition root

```csharp
builder.Services.AddAxisLogger();
builder.Services.AddSingleton<IAxisTelemetry>(NullAxisTelemetry.Instance);
builder.Services.AddECommerceApplication();
builder.Services.AddECommerceFacade();
builder.Services.AddECommercePersistence(builder.Configuration);
builder.Services.AddInMemoryEmail();
builder.Services.AddJwtAccessTokens();

builder.Services.AddECommerceAuthentication(builder.Configuration);
builder.Services.AddECommerceAuthorization();
builder.Services.AddECommerceCatalog(builder.Configuration);

var app = builder.Build();

if (MigrateCommand.IsRequested(args))
    return await MigrateCommand.RunAsync(app);
```

From [`Program.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Host/Scaffolds.ECommerce.Host/Program.cs).

- **Exactly one composition root wires the application**, and it is the only place that binds an `IAxis*`
  abstraction (or an app-owned port) to a concrete adapter, through `AddAxis*` / `Add*` extensions.
  [`Persistence/DependencyInjection`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Host/Scaffolds.ECommerce.Host/Persistence/DependencyInjection.cs)
  picks Postgres or MySQL from configuration for the business store, the bus outbox, the cache and the saga store.
- **No call site outside the composition root names a concrete adapter or provider** — handlers, facades and
  ports depend on abstractions only.
- **In a monolith one shared web host serves every bounded context.** There is no per-context BFF — neither as a
  host nor as a class library composed in through Application Parts. Controllers live in the host, one folder per
  context
  ([`Controllers/Catalog/ProductsV1Controller`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Host/Scaffolds.ECommerce.Host/Controllers/Catalog/ProductsV1Controller.cs)).
- The host splits its own wiring into internal per-area classes (`Host/Auth/`, `Host/Authorization/`,
  `Host/Catalog/`, `Host/Persistence/`), each a `DependencyInjection` called from `Program.cs`.
- **The host exposes `public partial class Program`** so the end-to-end tests boot the real pipeline through
  `WebApplicationFactory<Program>`:

  ```csharp
  namespace Scaffolds.ECommerce.Host
  {
      public partial class Program;
  }
  ```

- **Startup performs no I/O.** The schema is migrated by an explicit verb
  (`dotnet run --project Host/Scaffolds.ECommerce.Host -- migrate`), which
  [`MigrateCommand`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Host/Scaffolds.ECommerce.Host/Persistence/MigrateCommand.cs)
  runs through the admin facade ([Persistence](persistence.md)).
- **A bounded context promoted to a microservice gets its own host** and composition root — its own BFF — while
  keeping its namespace unchanged. The single host is a monolith default, not a ceiling.
- The same extensions serve every host model: a web host, a worker host and a test host all call them.

---

## Services and policies

Logic that serves several use cases, a worker or an event handler — orchestration, reconcilers, pipelines,
cross-context locators, pricing — is a named **service**; a reusable business predicate guarding one decision is a
**policy**. Neither is a use case: they carry no command or response and are reached through DI.

| Kind | Shape | Folder | In the reference application |
|---|---|---|---|
| service | `I{Name}Service` + `internal sealed {Name}Service` | `{BC}/[{Aggregate}/]Services/{Name}/` | [`AuthTokenIssuer/`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Application/Auth/Services/AuthTokenIssuer), [`OrderPricing/`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Application/Catalog/Orders/Services/OrderPricing), [`GrantsCache/`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Application/Authorization/Services/GrantsCache) |
| policy | `I{Name}Policy` + `internal sealed {Name}Policy`, returning `AxisResult` | `{BC}/[{Aggregate}/]Policies/` | not yet exemplified |

- The implementation is `internal sealed`; the interface is public only when a host driving project consumes it
  (public interface, internal implementation).
- The implementation name is the interface name without the `I` — no `Impl` suffix, no other deviation.
- The records a service exchanges sit in its own folder, internal sealed (`User` and `JwtAccessToken` beside
  `AuthTokenIssuerService`, `Grants` beside `GrantsCacheService`).
- Services and policies are **registered explicitly as scoped** in the context's registration — never scanned
  ([`Catalog/DependencyInjection`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Application/Catalog/DependencyInjection.cs)).
- A reconciler-style service exposes `RunOnceAsync(CancellationToken)`; the loop that schedules it lives in the
  [workers](#workers) project, not in the application.

### Options

Tunables a context reads are an options class at the context root, bound by the host:

```csharp
public sealed class CatalogOptions
{
    public const string SectionName = "ECommerce:Catalog";

    public decimal UnitPrice { get; init; }

    public decimal DiscountRate { get; init; }
}
```

```csharp
services
    .AddOptions<CatalogOptions>()
    .Bind(configuration.GetSection(CatalogOptions.SectionName));
```

From [`CatalogOptions`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Application/Catalog/CatalogOptions.cs)
and [`Host/Catalog/DependencyInjection`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Host/Scaffolds.ECommerce.Host/Catalog/DependencyInjection.cs).

- The class is `public sealed {Area}Options` with `init` properties and a `SectionName` constant — public only
  because the host binds it.
- The host binds it with `AddOptions<T>().Bind(…)`, adding `ValidateDataAnnotations().ValidateOnStart()` when
  values are required
  ([`AuthTokenOptions`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Application/Auth/AuthTokenOptions.cs),
  bound in [`Host/Auth/DependencyInjection`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Host/Scaffolds.ECommerce.Host/Auth/DependencyInjection.cs)).
- Services and handlers consume it as `IOptions<T>`
  ([`OrderPricingService`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Application/Catalog/Orders/Services/OrderPricing/OrderPricingService.cs)).

Configuration keys and secrets are covered in [Process](process.md).

---

## Validators

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

From [`CheckoutProductValidator`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Application/Catalog/Products/UseCases/CheckoutProduct/v1/CheckoutProductValidator.cs).

Validation is declarative, built on [`AxisValidator`](https://github.com/axis-sys/axis-validator). The validation
behavior runs in the mediator pipeline and short-circuits before the handler, which is why a handler may trust and
cast validated fields.

- The validator is `internal sealed {UseCase}Validator : AxisValidatorBase<{UseCase}Command|Query>`, with its rules
  registered **in the constructor only**, in the same `v1` folder as the handler.
- **Only Axis helper methods declare rules** (`RequiredTryParse`, `RequiredWithMaxLength`, `RequiredRange`,
  `RequiredEmail`, `RequiredGuid7`…). The raw FluentValidation API — `RuleFor`, `WithErrorCode`,
  `using FluentValidation` — never appears in application code. A missing helper is added to the AxisValidator
  library, never worked around.
- A bespoke check no intent helper covers uses `Satisfies` or `EachSatisfies`, passing the predicate and the error
  constant
  ([`UpdateGrantsValidator`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Application/Authorization/UseCases/UpdateGrants/v1/UpdateGrantsValidator.cs)).
- **Every helper call passes a constant** from the aggregate's `{Aggregate}Errors` class
  (`ProductErrors.ProductIdInvalid`) — or from the kernel's shared errors class when several aggregates use the code
  ([`SharedErrors`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.SharedKernel/Shared/SharedErrors.cs))
  — never an inline string literal.

Value-object inputs are where wire strings become value objects:

| Input | Helper |
|---|---|
| required — one code for absent and invalid (the default) | `RequiredTryParse(x => x.CustomerId, SharedErrors.CustomerIdInvalid, CustomerId.TryParse)` ([`MarkEmailValidatedValidator`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Application/Customers/UseCases/MarkEmailValidated/v1/MarkEmailValidatedValidator.cs)) |
| required — the contract needs distinct "required" and "invalid" codes | `NotNullOrEmpty` (required code) plus `OptionalTryParse` (invalid code): exactly one error per case |
| optional — absent is valid | `OptionalTryParse` with the `{VO}.TryParse` method group, never a `Satisfies` over `IsNullOrWhiteSpace \|\| TryParse` |

- A nested or complex sub-DTO gets its own `AxisValidatorBase` subclass in the same `v1` folder, composed through
  AxisValidator's composition helpers.
- A use case whose input needs no validation simply has **no validator file**
  ([`RequestEmailValidation/v1/`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Application/EmailValidation/UseCases/RequestEmailValidation/v1)
  has only a handler) and the behavior no-ops. A query that takes an id still parses it in a validator
  ([`GetProductValidator`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Application/Catalog/Products/UseCases/GetProduct/v1/GetProductValidator.cs)).
- **Not in a validator:** domain invariants stay in the domain and the factories ([Domain](domain.md));
  database-dependent checks stay in the handler chain as a `Then` over a port (`CreateProductHandler` checks the
  SKU with `GetBySkuAsync(…).RequireNotFoundAsync(…)`).

---

## Swappable infrastructure ports

Non-repository infrastructure — messaging, cache, object storage, e-mail, token issuing, external identity,
migrations — is a **small interface that sees only kernel types and its own DTO records**. Adapters implement it,
catch every SDK exception and translate it to an `AxisError`; callers compose the `AxisResult` and never
`try/catch`. Swapping the adapter is a DI change only.

| Port | Declared by | Adapter in the reference application | Wired by |
|---|---|---|---|
| `IAxisEmailService` | [`AxisEmail`](https://github.com/axis-sys/axis-email) | [`InMemoryEmailService`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Adapters/Scaffolds.ECommerce.Adapters.Driven.InMemory/EmailValidation/InMemoryEmailService.cs) | `AddInMemoryEmail` |
| `IAxisBus` | [`AxisBus`](https://github.com/axis-sys/axis-bus) | [`AxisBus.Postgres`](https://github.com/axis-sys/axis-bus-postgres) / [`AxisBus.MySql`](https://github.com/axis-sys/axis-bus-mysql) outbox | `AddECommercePersistence` |
| `IAxisCache` | [`AxisCache`](https://github.com/axis-sys/axis-cache) | [`AxisCache.Postgres`](https://github.com/axis-sys/axis-cache-postgres) / [`AxisCache.MySql`](https://github.com/axis-sys/axis-cache-mysql) | `AddECommercePersistence` |
| `IAxisTelemetry` | [`AxisTelemetry`](https://github.com/axis-sys/axis-telemetry) | `NullAxisTelemetry` | `Program.cs` |
| `IExternalIdentityContext` | the application ([`Contracts.Driven/Auth/`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Contracts/Scaffolds.ECommerce.Contracts.Driven/Auth/IExternalIdentityContext.cs)) | [`HttpExternalIdentityContext`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Adapters/Scaffolds.ECommerce.Adapters.Driven.AuthProviders/HttpExternalIdentityContext.cs) | `AddAuthProviders` |
| `IAccessTokenSignerPort` | the application ([`Contracts.Driven/Auth/`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Contracts/Scaffolds.ECommerce.Contracts.Driven/Auth/IAccessTokenSignerPort.cs)) | [`AccessTokenSigner`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Adapters/Scaffolds.ECommerce.Adapters.Driven.Jwt/Auth/AccessTokenSigner.cs), over [`AxisJwt`](https://github.com/axis-sys/axis-jwt) | `AddJwtAccessTokens` |
| `IUnitOfWork` | the application, over the framework's keyed `IAxisUnitOfWork` | [`UnitOfWorkAdapter`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Adapters/Scaffolds.ECommerce.Adapters.Driven.Repository/UnitOfWorkAdapter.cs) | `AddECommerceRepositories` |

- **Handlers and services depend only on the port interface**, never on a concrete SDK type or a provider-specific
  package — `RequestEmailValidationHandler` sees `IAxisEmailService`, not an e-mail SDK. The provider choice never
  leaks into the context.
- **Every port method returns `AxisResult` or `AxisResult<T>` and never throws** to the caller; the `try/catch`
  that converts an SDK exception into an `AxisError` lives only inside the adapter
  ([Exceptions at the boundary](https://github.com/axis-sys/axis-result/blob/main/docs/boundary-and-try.md)).
- **Adapter selection happens only in composition**, through an `Add*` extension; swapping the provider never
  touches a call site.
- **Port inputs and outputs are kernel types or sealed record DTOs declared beside the port**
  ([`ExternalUser`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Contracts/Scaffolds.ECommerce.Contracts.Driven/Auth/ExternalUser.cs),
  [`AccessTokenDescriptor`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Contracts/Scaffolds.ECommerce.Contracts.Driven/Auth/AccessTokenDescriptor.cs))
  — never domain types.
- When an adapter must be parameterized per call, the port takes a **descriptor** and a single factory creates the
  configured instance; do not split the factory by caller role. *(Not yet exemplified.)*
- **Framework-owned ports are consumed as-is.** An application declares only the infra ports specific to it, and may
  layer an app-owned port over a framework port when only the application can resolve the provider choice
  (`IUnitOfWork` over the keyed `IAxisUnitOfWork`).
- **Exception:** a port method may be synchronous when its adapter is purely in-memory and cannot fail with an I/O
  error (`IExternalIdentityContext.Get` reads the current principal; `IAccessTokenSignerPort.Sign` signs in memory).
- The set of adapters is open-ended: any adapter named here is an example, not a catalogue
  ([`AxisStorage`](https://github.com/axis-sys/axis-storage), for instance, has adapters for several blob stores).

### When a framework package owns the SDK boundary

When the infrastructure comes from a framework provider package that already owns the exception and retry
boundary, the application's own adapter over that port shrinks to provider selection plus `AxisResult`
composition:

- It holds **no `try/catch` and no `AxisResult.Try`**
  ([`CustomersRepository`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Adapters/Scaffolds.ECommerce.Adapters.Driven.Repository/Customers/Customers/CustomersRepository.cs)
  over [`AxisRepository`](https://github.com/axis-sys/axis-repository)'s `IAxisDbRepository`). If an SDK exception
  needs mapping, the fix belongs in the provider package, not in the adapter.
- **Transient faults** (network, timeout, throttling, 5xx) are retried inside the provider with capped exponential
  backoff; the application never wraps the port in its own retry loop.
- **Configuration and authorization faults** — a valid credential still denied for lack of a role — surface
  immediately and are never retried: they are not transient.
- **A failure reaches the caller as an `AxisError`** whose code is the `Code` of a member of the area's
  `{Aggregate}Errors` class (`CustomerErrors.CustomerNotFound.Code`) — never a raw SDK exception, never the
  exception message as the code ([Errors and types](https://github.com/axis-sys/axis-result/blob/main/docs/errors-and-types.md)).
- **A domain-shaped failure is decided by the application:** an edge result only the application knows to be an
  error is mapped to an `{Aggregate}Errors` member at the application boundary — `CheckoutProductHandler` turns a
  stock shortfall into `ProductErrors.InsufficientStock`.

One framework surface in the reference application still throws: the migration runner's `RunAsync`.
[`DatabaseMigratorAdapter`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Adapters/Scaffolds.ECommerce.Adapters.Driven.Repository/DatabaseMigratorAdapter.cs)
is therefore the exception boundary for it — one `AxisResult.TryAsync`, logged and mapped to
`AdminErrors.DatabaseMigrationFailed`.

---

## Bus events

Asynchronous integration uses the `IAxisBus` + `IAxisEvent` + `IAxisEventHandler<T>` trio from
[`AxisBus`](https://github.com/axis-sys/axis-bus). An event is the vocabulary published between contexts, so a
consumer never comes to depend on the publisher's contracts — and the publisher never learns who consumed it.

```csharp
public sealed record ProductCheckedOutEvent(string CartId, string ProductId, int Quantity) : IAxisEvent
{
    public const string Topic = "product_checked_out";
}
```

From [`ProductCheckedOutEvent`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.SharedKernel/Catalog/Events/v1/ProductCheckedOutEvent.cs),
published by `CheckoutProductHandler` ([above](#handlers)) and consumed by:

```csharp
internal sealed class ProductCheckedOutEventHandler(
    ICartItemFactory cartItemFactory,
    IUnitOfWork unitOfWork,
    IAxisLogger<ProductCheckedOutEventHandler> logger
) : IAxisEventHandler<ProductCheckedOutEvent>
{
    public async Task<AxisResult> HandleAsync(ProductCheckedOutEvent @event)
    {
        var associated = await NewCartItemOf(@event)
            .ThenAsync(cartItemFactory.CreateAsync)
            .ThenAsync(_ => unitOfWork.SaveChangesAsync());

        logger.LogResult(ProductCheckedOutEvent.Topic, associated);
        return AxisResult.Ok();
    }

    private static AxisResult<ICartItemFactory.NewArgs> NewCartItemOf(ProductCheckedOutEvent @event)
        => CartId.TryParse(@event.CartId, out var cartId) && ProductId.TryParse(@event.ProductId, out var productId)
            ? new ICartItemFactory.NewArgs { CartId = cartId, ProductId = productId, Quantity = @event.Quantity }
            : CartItemErrors.ProductCheckedOutEventMalformed;
}
```

From [`ProductCheckedOutEventHandler`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Application/Catalog/CartItems/Events/ProductCheckedOutEventHandler.cs).

**The event**

- `public sealed record {Thing}{PastTenseVerb}Event(…) : IAxisEvent` — a positional record in the **shared
  kernel**, under the publishing context's `{BC}/Events/v1/` folder. The one exception is a saga-lifecycle event,
  which stays in the driving contracts next to its payload (`{BC}/[{Aggregate}/]v1/Sagas/{Saga}/`).
- It declares its own `public const string Topic` in snake_case, and publishing always passes it.
- The payload carries **ids and outcome flags** as strings and primitives — never e-mails, names or record data.
  PII stays out of the bus.

**Publishing — inside the unit of work**

- The event is enqueued with `IAxisBus.PublishAsync` **inside the handler or saga stage that persists its
  triggering state change, before that unit of work's `SaveChangesAsync`** — in `CheckoutProductHandler` the stock
  reservation, the publish and the commit are consecutive steps of one chain. With the durable outbox
  ([`AxisBus.Postgres`](https://github.com/axis-sys/axis-bus-postgres) /
  [`AxisBus.MySql`](https://github.com/axis-sys/axis-bus-mysql)) the event is written in the same transaction as
  the change, so consumers only ever see committed state.
- **There is no publish-only use case or saga stage.** An event enqueued outside a committing unit of work is never
  drained and is silently lost; fold the publish into the step that persists the change.
- The context that produces the event publishes it; the others consume it off the bus.
- **Exception:** the in-process [`AxisMemoryBus`](https://github.com/axis-sys/axis-memory-bus) delivers
  synchronously and has no unit of work; the discipline above governs the durable outbox. The host toggles the
  outbox dispatcher with `Bus:DispatcherEnabled` and paces it with `Bus:PollInterval`.

**Consuming**

- A consumer is an `internal sealed {Event}Handler : IAxisEventHandler<TEvent>` in the consuming context's
  `{BC}/[{Aggregate}/]Events/` folder, returning `Task<AxisResult>`. Both consumers of the reference application
  live in the publishing context itself — Catalog's CartItems reacting to Products, and Authorization evicting its
  own cache
  ([`GrantsUpdatedEventHandler`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Application/Authorization/Events/GrantsUpdatedEventHandler.cs));
  a consumer in another context is written the same way, in that context's application.
- It **defensively `TryParse`s the incoming string ids** — bus payloads are untrusted — and maps bad input to a
  constant of its errors class.
- A fan-out consumer that does real work **logs its own failures and returns `Ok`**, so delivery stays independent
  of the publisher and a consumer's failure never propagates back to the publishing use case.
- A long-lived consumer that needs scoped services creates its own scope through the scope factory.

---

## Cache-aside

A hot read path gets a dedicated cache component composing `IAxisCache.GetOrCreateAsync` over the real source —
another context's facade or a reader port. Mutating use cases never touch the cache: they publish an event and an
event handler in the caching context evicts. Hot paths stay fast and invalidation stays decoupled from the
mutators.

```csharp
private const string KeyPrefix = "scaffolds-ecommerce.authorization.grants.v1";
private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);

public Task<AxisResult<Grants>> GetAsync(CustomerId customerId)
    => cache.GetOrCreateAsync(KeyOf(customerId), () => ComposeAsync(customerId), Ttl);

public Task<AxisResult> EvictAsync(CustomerId customerId)
    => cache.RemoveAsync(KeyOf(customerId));
```

From [`GrantsCacheService`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Application/Authorization/Services/GrantsCache/GrantsCacheService.cs).

| Step | In the reference application |
|---|---|
| read, populating lazily on a miss | `GetGrantsHandler` → `GrantsCacheService.GetAsync` → `ComposeAsync` (Customers facade + `IMembersReaderPort`) |
| mutate | [`UpdateGrantsHandler`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Application/Authorization/UseCases/UpdateGrants/v1/UpdateGrantsHandler.cs) writes the member and publishes `GrantsUpdatedEvent` — it never injects the cache |
| evict | [`GrantsUpdatedEventHandler`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Application/Authorization/Events/GrantsUpdatedEventHandler.cs) calls `EvictAsync` → `RemoveAsync`; the next read repopulates |
| prove it | [`GrantsCacheDualDbTests`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Tests/Scaffolds.ECommerce.IntegrationTests/GrantsCacheDualDbTests.cs) covers miss, hit and eviction on both databases |

- The component is **internal** — static key/TTL helpers or a sealed class — and composes the `IAxisCache` port
  from [`AxisCache`](https://github.com/axis-sys/axis-cache); it never wraps an SDK type.
- **Keys** come from static helpers under a stable, namespaced prefix (`KeyOf`); the **TTL** is a
  `static readonly TimeSpan` on the component.
- **A read is one line of cache-aside**: `cache.GetOrCreateAsync(key, () => ComposeAsync(…), Ttl)`.
- **Invalidation is event-driven eviction** — `RemoveAsync` of the affected keys, or an epoch/version bump to
  invalidate a whole scope; repopulation happens on the next read.
- **Mutating handlers never inject the cache**; they publish an event and the caching context's handler evicts.
- An entry that **guards correctness** rather than caching data — an epoch counter — never expires, because an
  expired guard could resurrect entries written under an older version. *(Not yet exemplified.)*
- **`IAxisCache` is registered at the composition root**, never inside the application's own registration — the
  host picks a two-tier adapter (in-memory L1 over a durable SQL L2) in
  [`Persistence/DependencyInjection`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Host/Scaffolds.ECommerce.Host/Persistence/DependencyInjection.cs);
  [`AxisMemoryCache`](https://github.com/axis-sys/axis-memory-cache) is the in-memory alternative.

---

## Sagas

A process that crosses two or more contexts and must **undo** its work when a later step fails — where one ACID
transaction is impossible — is a saga, built with [`AxisSaga`](https://github.com/axis-sys/axis-saga). In the
reference application, validating an e-mail marks the customer as validated (Customers) and then removes the code
(EmailValidation); if the removal fails, the mark is reverted.

### Definition

```csharp
internal static class ValidateEmailSaga
{
    public const string Name = "ValidateEmail";
    public const string MarkEmailValidatedStage = nameof(MarkEmailValidatedStageHandler);
    public const string RemoveValidationCodeStage = nameof(RemoveValidationCodeStageHandler);
    public const string RevertEmailValidatedStage = nameof(RevertEmailValidatedStageHandler);

    public static void Configure(IAxisSagaConfigurator<ValidateEmailPayload> saga)
    {
        saga.AddStage(MarkEmailValidatedStage)
            .NextStageOnSuccess(RemoveValidationCodeStage);

        saga.AddStage(RemoveValidationCodeStage)
            .FinishOnSuccess()
            .RouteToOnError(RevertEmailValidatedStage);

        saga.AddErrorStage(RevertEmailValidatedStage).FinishOnSuccess();
    }
}
```

From [`ValidateEmailSaga`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Application/EmailValidation/Sagas/ValidateEmail/ValidateEmailSaga.cs).

- **Declare it with `AxisSagaDefinitions.Define<TPayload>(Name, Configure)`**, where `Configure` is a static method
  and every stage name is a `nameof` constant on the saga class.
- **Register it through its own `Add{Saga}Saga` extension**
  ([`ValidateEmailSagaDependencyInjection`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Application/EmailValidation/Sagas/ValidateEmail/ValidateEmailSagaDependencyInjection.cs)
  adds the definition as a singleton), invoked from the owning context's registration.
- **Start it from a use case through `IAxisSagaMediator.StartAsync`**:

  ```csharp
  .ThenAsync(stored => sagaMediator
      .StartAsync(ValidateEmailSaga.Name, ValidateEmailPayloadFactory.Create(stored.CustomerId))
      .MapErrorAsync(ValidateEmailRuns.RunErrorOf))
  ```

  From [`ValidateEmailHandler`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Application/EmailValidation/UseCases/ValidateEmail/v1/ValidateEmailHandler.cs).
- **A started saga still pending answers 202** with the `Location` of a polling `GET` for its status
  ([HTTP edge](edge.md#saga-endpoints)). Status and cancellation are use cases over `GetByIdAsync` and `CancelAsync`;
  [`ValidateEmailRuns`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Application/EmailValidation/Sagas/ValidateEmail/ValidateEmailRuns.cs)
  finds a run of this saga and remaps the framework's saga errors to the application's codes with
  [`MapError`](https://github.com/axis-sys/axis-result/blob/main/docs/map-errors.md).
- **A stage failure runs its declared compensation chain** — the `RouteToOnError` stages, declared with
  `AddErrorStage`, in listed (undo) order; the author lists them as the reverse of the forward stages.
- **The resumer that advances pending sagas is hosted by the framework** and toggled by configuration
  (`Saga:ResumerEnabled`, beside `Saga:JanitorEnabled`, read in the host's
  [`Persistence/DependencyInjection`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Host/Scaffolds.ECommerce.Host/Persistence/DependencyInjection.cs)).
  Its settings cover whether it runs, its poll interval, the age after which a pending saga resumes and the batch
  size. The application never hosts a resumer worker.
- Every saga-persistence adapter ([`AxisSaga.Postgres`](https://github.com/axis-sys/axis-saga-postgres),
  [`AxisSaga.MySql`](https://github.com/axis-sys/axis-saga-mysql)) honours that contract: it self-hosts the
  resumer per configuration and recovers orphaned pending sagas.

### Payload

```csharp
public sealed record ValidateEmailPayload
{
    public required string CustomerId { get; init; }

    public bool EmailMarkedValidated { get; init; }

    public bool EmailWasAlreadyValidated { get; init; }

    public bool CodeRemoved { get; init; }

    public bool EmailValidationReverted { get; init; }
}
```

From [`ValidateEmailPayload`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Contracts/Scaffolds.ECommerce.Contracts.Driving/EmailValidation/v1/Sagas/ValidateEmail/ValidateEmailPayload.cs).

- The payload is a serialized contract, so it lives in the **driving contracts** under
  `{BC}/[{Aggregate}/]v1/Sagas/{Saga}/`, as `public sealed record {Saga}Payload`.
- It is an **accumulator**: `required init` properties for the stage-one inputs, optional or defaulted `init`
  properties for what later stages produce (resolved ids, outcomes, counters).
- Every member is a primitive, a contract DTO or a list of them; **ids are strings**, because the payload
  round-trips through the saga store as JSON. A payload may reference a use-case DTO from a sibling folder.
- The initiating handler builds the initial payload through a dedicated **static `{Saga}PayloadFactory`**
  ([`ValidateEmailPayloadFactory`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Application/EmailValidation/Sagas/ValidateEmail/ValidateEmailPayloadFactory.cs),
  internal, in the application's `Sagas/{Saga}/`) right at the `StartAsync` call — never assembled inline.
- Saga-scoped result and protocol records, and the saga's completion and progress events, live in the same
  `Sagas/{Saga}/` folder; the events follow the [event rules](#bus-events).

### Stage handlers

```csharp
internal sealed class RevertEmailValidatedStageHandler(
    ICustomersFacade customersFacade
) : IAxisSagaStageHandler<ValidateEmailPayload>
{
    public string SagaName => ValidateEmailSaga.Name;
    public string StageName => ValidateEmailSaga.RevertEmailValidatedStage;

    public Task<AxisResult<ValidateEmailPayload>> ExecuteAsync(ValidateEmailPayload payload)
    {
        if (!payload.EmailMarkedValidated || payload.EmailWasAlreadyValidated || payload.EmailValidationReverted)
            return AxisResult.Ok(payload).AsTaskAsync();

        return customersFacade
            .RevertEmailValidatedAsync(new RevertEmailValidatedCommand { CustomerId = payload.CustomerId })
            .MapAsync(_ => payload with { EmailValidationReverted = true });
    }
}
```

From [`RevertEmailValidatedStageHandler`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Application/EmailValidation/Sagas/ValidateEmail/Handlers/RevertEmailValidatedStageHandler.cs).

- A stage handler is `internal sealed`, implements `IAxisSagaStageHandler<TPayload>`, sits in
  `{BC}/Sagas/{Saga}/Handlers/`, and exposes `SagaName` / `StageName` as expression-bodied properties referencing
  the saga's constants.
- `ExecuteAsync(TPayload payload)` returns `Task<AxisResult<TPayload>>`. The payload is immutable and evolves only
  through `with` expressions; stages communicate **only** through it.
- **Every stage is idempotent.** Stages re-run after a crash or a resumer pickup, so each first checks whether its
  work already happened — a payload flag, a `GetBy` lookup or an already-applied read — before writing
  ([`MarkEmailValidatedStageHandler`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Application/EmailValidation/Sagas/ValidateEmail/Handlers/MarkEmailValidatedStageHandler.cs)
  short-circuits on `EmailMarkedValidated`).
- **A compensation stage guards on a payload flag recording what this run actually did**, so it never undoes work it
  did not perform — above, nothing is reverted when the e-mail was already validated before the run.
- **A business outcome the saga must route on travels in the payload** (`payload with { Succeeded = false,
  ErrorCode = … }`) and is returned as `Ok`; only unexpected or infrastructure problems surface as an errored
  `AxisResult`. That split is what lets the saga route to compensation instead of failing hard. *(Not yet
  exemplified.)*
- **Ambient context does not survive a resume.** App-scoped context such as a tenant is re-established from the
  payload before any facade call.
- **Cross-context work goes through the owning context's facade** — the mark and revert stages call
  `ICustomersFacade`, never Customers' write port. A stage may read a sibling context's reader port directly.
- Stage handlers are discovered by the `AddAxisSagaHandlers` scan; there is no per-stage registration.

The compensation path is proven against both databases by
[`ValidateEmailSagaCompensationDualDbTests`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Tests/Scaffolds.ECommerce.IntegrationTests/ValidateEmailSagaCompensationDualDbTests.cs).

---

## Workers

Background hosted services are a **driving adapter of their own** — a workers project such as
`{App}.Adapters.Driving.Workers` — organized in vertical slices mirroring `{BC}/{Aggregate}`. Workers are thin
timers: the work itself is an application service the worker resolves on each pass. *(The reference application
has no worker project yet; its background work — the bus dispatcher, the cache sweep, the saga resumer and janitor —
is hosted by the framework packages and toggled in
[`appsettings.json`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Host/Scaffolds.ECommerce.Host/appsettings.json).)*

```
{App}.Adapters.Driving.Workers/
├─ DependencyInjection.cs          public — calls every Add{Name}Worker
└─ {BC}/{Aggregate}/
   ├─ {Name}Worker.cs              internal sealed BackgroundService
   ├─ {Name}WorkerSettings.cs      sealed record: Enabled + poll interval
   └─ DependencyInjection.cs       internal static — Add{Name}Worker(this IServiceCollection, settings)
```

- Each slice ships exactly those three files. `Add{Name}Worker` adds the settings singleton plus the hosted service.
- The project's **single public** `DependencyInjection` calls each `Add{Name}Worker` with the configuration it read
  from the host and a default; the host never calls one extension per worker.
- Settings are sealed records with an `Enabled` flag and a poll interval; the `Enabled` flag in configuration is how
  the host chooses which workers run.
- A disabled worker logs once and returns. Each pass creates its own scope and resolves the application service
  (its `RunOnceAsync(CancellationToken)`, see [Services](#services-and-policies)) — the worker owns no business
  logic.
- The loop catches per-pass exceptions (log, retry on the next poll) and treats cancellation as shutdown; no
  unhandled exception may kill the host.
- A pass that fails only with transient errors — the schema not ready before migrations run, say — logs once and
  retries silently with a backoff capped at the poll interval, never a warning per poll.
- The project references only the application plus the hosting, DI and logging abstractions — never repositories
  or the web host.
- **The saga resumer is not a worker here**: AxisSaga hosts it when its resumer setting is enabled.

---

## Access modifiers

The application exposes almost nothing. Reachability is engineered — public facades plus assembly scanning — not
granted by visibility.

| Type | Declaration | In the reference application |
|---|---|---|
| handler, validator, factory, service, policy, event handler, cache component, stage handler | `internal sealed class` | [`RegisterProductHandler`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Application/Catalog/Products/UseCases/RegisterProduct/v1/RegisterProductHandler.cs) |
| facade implementation, repository, adapter | `internal sealed class` | [`ProductsRepository`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Adapters/Scaffolds.ECommerce.Adapters.Driven.Repository/Catalog/Products/ProductsRepository.cs) |
| saga definition, payload factory, helpers | `internal static class` | `ValidateEmailSaga`, `ValidateEmailPayloadFactory` |
| interface used only inside the project | `internal interface` | `IProductFactory`, `IGrantsCacheService` |
| per-context registration | `internal static class DependencyInjection` | `Application/Auth/DependencyInjection` |
| the project's registration entry | `public static class DependencyInjection` | [`Application/DependencyInjection`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Application/DependencyInjection.cs) |
| options the host binds or an adapter reads | `public sealed class` | [`AuthTokenOptions`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Application/Auth/AuthTokenOptions.cs) |
| contracts, facade interfaces, ports, errors classes, shared-kernel types | `public` — they cross projects by design | `IProductsFacade`, `IProductsReaderPort`, `ProductErrors` |

- **The default is `internal sealed class`** (or `internal static class` for helpers, `internal interface` for
  seams); a type is `public` only with an explicit reachability reason.
- **`public` is reserved** for the single DI composition entry, the service interfaces and settings a host driving
  project must resolve, and any parser or seam consumed at the edge.
- A public seam interface may be implemented by an `internal sealed` class — the contract is visible, the
  implementation stays behind the assembly wall.
- **Handlers are never public.** Unit tests reach them through the facade and hosts through the mediator; test
  projects get `InternalsVisibleTo` where they need more
  ([`Application.csproj`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Core/Scaffolds.ECommerce.Application/Scaffolds.ECommerce.Application.csproj)).
- **Concrete classes are sealed unless static.** An internal-but-unsealed class is a defect to fix, not a variant.
- Whether an interface shares its implementation's file depends on the project both live in, never on visibility;
  a port declared in another project never shares a file with its adapter ([layout](#the-application-slice)).
- Error classes are `public static` and live beside the ports in the driven contracts, not in the application
  ([Style](style.md)).

---

## See also

- [Domain](domain.md) — the aggregates, factories and value objects the handlers drive.
- [Persistence](persistence.md) — repositories, SQL, migrations and the unit of work behind the driven ports.
- [HTTP edge](edge.md) — controllers over the facades, OpenAPI, authentication and the saga endpoints.
- [Testing](testing.md) — the four test projects, unit tests through the facade, the architecture tests.
- [Style](style.md) — naming, the `{Aggregate}Errors` classes, formatting.
- [Process](process.md) — comments, configuration and documentation policy.
- [AxisResult documentation](https://github.com/axis-sys/axis-result/blob/main/docs/index.md) — the railway every
  handler, port and facade rides.
