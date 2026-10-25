> Copy this file into whatever agent or instructions format your coding assistant uses (an instructions file, a
> custom-agent file, a system prompt). It teaches the assistant to build and change an application the Axis way.

# Building an application with the Axis Framework

An Axis application is a **hexagon split into projects** and **vertical-sliced by bounded context** inside each
project. Every business operation is a CQRS use case — command or query, response, handler, optional validator —
exposed by a **facade**; every piece of infrastructure is a **port** that returns `AxisResult` and never throws, and
its adapter is chosen once, in the **host**. Expected failures travel as `AxisError` values on the second rail of
`AxisResult`; nothing above the infrastructure boundary throws or catches.

This file says **where each piece goes, what shape it has and what to avoid**. Three sources go deeper; read them
instead of guessing:

- **The reference application** — a small e-commerce (Auth, Authorization, Catalog, Customers, EmailValidation over
  Postgres or MySQL) in [axis-framework-dotnet](https://github.com/axis-sys/axis-framework-dotnet). Every path below
  (`scaffolds/...`) is relative to that repository. Before writing a new piece, open the nearest example of the
  same kind there and mirror it.
- **The application guide** — one page per concern, each convention with its reason and its example:
  [architecture](https://github.com/axis-sys/axis-framework-dotnet/blob/main/site/guide/architecture.md),
  [domain](https://github.com/axis-sys/axis-framework-dotnet/blob/main/site/guide/domain.md),
  [persistence](https://github.com/axis-sys/axis-framework-dotnet/blob/main/site/guide/persistence.md),
  [HTTP edge](https://github.com/axis-sys/axis-framework-dotnet/blob/main/site/guide/edge.md),
  [testing](https://github.com/axis-sys/axis-framework-dotnet/blob/main/site/guide/testing.md),
  [style](https://github.com/axis-sys/axis-framework-dotnet/blob/main/site/guide/style.md),
  [process](https://github.com/axis-sys/axis-framework-dotnet/blob/main/site/guide/process.md).
- **Each package's own usage agent** — `agents/<repository>.md` in the package repository (for example
  [axis-result](https://github.com/axis-sys/axis-result/blob/main/agents/axis-result.md) for the operators of the
  railway), and the XML docs of every public member in IntelliSense.

## The solution

Five sibling solution folders, one project per hexagonal role (`{App}` is the application name):

| Folder | Project | Holds | References |
|---|---|---|---|
| `Core/` | `{App}.SharedKernel` | value objects and ids, bus events, shared errors and code constants | — |
| `Core/` | `{App}.Domain` | entity state: `I{Entity}EntityProperties` + `internal sealed record {Entity}Properties` | SharedKernel |
| `Core/` | `{App}.Application` | handlers, validators, factories, services, event handlers, sagas, pipeline behaviors | SharedKernel, Domain, both contracts |
| `Contracts/` | `{App}.Contracts.Driving` | commands, queries, responses, facade interfaces, saga payloads | SharedKernel |
| `Contracts/` | `{App}.Contracts.Driven` | reader/write ports, `IUnitOfWork`, app-owned infrastructure ports, `{Aggregate}Errors` | Domain, SharedKernel |
| `Adapters/` | `{App}.Adapters.Driving.Facade` | the facade implementations | Contracts.Driving, Application |
| `Adapters/` | `{App}.Adapters.Driven.Repository` | provider-agnostic repositories, tables, migrations | Domain, SharedKernel, Contracts.Driven |
| `Adapters/` | `{App}.Adapters.Driven.Postgres` / `.MySql` | provider wiring and SQL dialect seams | Adapters.Driven.Repository |
| `Host/` | `{App}.Host` | the only composition root, controllers, the `migrate` verb | everything it wires |
| `Tests/` | `{App}.{Architecture,Unit,Integration,E2E}Tests` | the four-project pyramid | what each layer needs |

- Dependencies point **inward**; only the host references driven adapters. The architecture tests enforce it
  (`scaffolds/Tests/Scaffolds.ECommerce.ArchitectureTests/HexagonalBoundaryTests.cs`, `FacadeBoundaryTests.cs`).
- Inside every project, folders are `{BoundedContext}/{Aggregate}/{Feature}` and **the namespace equals the folder
  path**, `v1` segments included. No `Common/`, `Helpers/` or `Utils/`; a type every context reads goes in
  `SharedKernel/Shared/`.
- One top-level type per file, named after the type. An interface may share the file of its only implementation
  when both live in the same project (`scaffolds/Core/Scaffolds.ECommerce.Application/Catalog/Products/Aggregates/ProductFactory.cs`).

## Where a new piece goes

| You are adding… | Put it in… | Mirror |
|---|---|---|
| a command / query / response | `Contracts.Driving/{BC}/[{Aggregate}/]v1/{UseCase}/` | `scaffolds/Contracts/Scaffolds.ECommerce.Contracts.Driving/Catalog/Products/v1/CheckoutProduct/` |
| the facade of an aggregate | `Contracts.Driving/{BC}/[{Aggregate}/]v1/I{Entities}Facade.cs` + `Adapters.Driving.Facade/{BC}/[{Aggregate}/]{Entities}Facade.cs` | `.../Catalog/Products/v1/IProductsFacade.cs`, `scaffolds/Adapters/Scaffolds.ECommerce.Adapters.Driving.Facade/Catalog/Products/ProductsFacade.cs` |
| a handler (+ validator) | `Application/{BC}/[{Aggregate}/]UseCases/{UseCase}/v1/` | `scaffolds/Core/Scaffolds.ECommerce.Application/Catalog/Products/UseCases/CheckoutProduct/v1/` |
| entity state | `Domain/{BC}/{Aggregate}/` | `scaffolds/Core/Scaffolds.ECommerce.Domain/Catalog/Products/` |
| a factory | `Application/{BC}/[{Aggregate}/]Aggregates/` | `scaffolds/Core/Scaffolds.ECommerce.Application/Catalog/Products/Aggregates/ProductFactory.cs` |
| a value object | `SharedKernel/{BC}/{Aggregate}/ValueObjects/` | `scaffolds/Core/Scaffolds.ECommerce.SharedKernel/Catalog/Products/ValueObjects/ProductId.cs`, `Sku.cs` |
| reader/write ports and errors | `Contracts.Driven/{BC}/{Aggregate}/` | `scaffolds/Contracts/Scaffolds.ECommerce.Contracts.Driven/Catalog/Products/` |
| a repository, its columns, table and row record | `Adapters.Driven.Repository/{BC}/{Aggregate}/` | `scaffolds/Adapters/Scaffolds.ECommerce.Adapters.Driven.Repository/Catalog/Products/` |
| a bus event | `SharedKernel/{BC}/Events/v1/` | `scaffolds/Core/Scaffolds.ECommerce.SharedKernel/Catalog/Events/v1/ProductCheckedOutEvent.cs` |
| a bus consumer | `Application/{BC}/[{Aggregate}/]Events/` | `scaffolds/Core/Scaffolds.ECommerce.Application/Catalog/CartItems/Events/ProductCheckedOutEventHandler.cs` |
| a service shared by use cases | `Application/{BC}/[{Aggregate}/]Services/{Name}/` | `scaffolds/Core/Scaffolds.ECommerce.Application/Authorization/Services/GrantsCache/` |
| a saga | `Application/{BC}/Sagas/{Saga}/` + payload in `Contracts.Driving/{BC}/v1/Sagas/{Saga}/` | `scaffolds/Core/Scaffolds.ECommerce.Application/EmailValidation/Sagas/ValidateEmail/` |
| an endpoint | `Host/Controllers/{BC}/{Resource}V{n}Controller.cs` | `scaffolds/Host/Scaffolds.ECommerce.Host/Controllers/Catalog/ProductsV1Controller.cs` |
| a test | the lowest of the four test projects that can prove it | see [Tests](#tests) |

## Use cases

- **Contracts** are `public sealed record`s of primitives: inputs are nullable `init` properties (ids as strings),
  outputs `required init`. No domain types cross the facade. A breaking change is a `v2` sibling tree; `v1` is never
  edited incompatibly.
- **Facade:** `I{Entities}Facade` has one method per use case, one parameter, returning
  `Task<AxisResult<{UseCase}Response>>`. Its implementation is `internal sealed`, injects only `IAxisMediator` and
  makes one `mediator.Cqrs.ExecuteAsync`/`QueryAsync` call per method.
- **Handler:** `internal sealed` with a primary constructor, only `HandleAsync(command)` returning
  `Task<AxisResult<TResponse>>`. Cast the string inputs to value objects first, then write **one railway chain**
  ending in the `Map` to the response. Read time from `TimeProvider`, never `DateTime.Now`. Never throw, never
  dispatch another use case through the mediator, never re-check the authorization the edge already enforced.
- **Ambient context:** the caller is `IAxisMediator.AxisEntityId` (null means anonymous), set by the edge; the
  `CancellationToken` is ambient. Neither is ever a contract field or a parameter of a handler, facade or port.
- **Validator:** `internal sealed {UseCase}Validator : AxisValidatorBase<T>`, rules in the constructor with the Axis
  helpers only (`RequiredTryParse`, `OptionalTryParse`, `Satisfies`…), every code from `{Aggregate}Errors` or
  `SharedErrors`. It validates input shape; database and domain checks belong to the handler
  (`.../CreateProduct/v1/CreateProductValidator.cs`).
- **Registration:** one `public static class DependencyInjection` per project root; per-context ones are
  `internal static`. Handlers, validators and saga stages are assembly-scanned; every other seam is registered
  explicitly, scoped (`scaffolds/Core/Scaffolds.ECommerce.Application/DependencyInjection.cs`).

## Domain

- Classify each aggregate: **N0** lookup (`I{X}Properties` + reader port only), **N1** CRUD (Properties pair, reader
  and write ports, a factory), **N2** behavior-rich — introduced in the change that adds the first rule guarding the
  aggregate's state. Every aggregate of the reference application is N1.
- **State:** a public get-only `I{Entity}EntityProperties` and an `internal sealed record {Entity}Properties`, one
  constructor parameter per line; records carry no behavior.
- **Factory:** `internal interface I{Aggregate}Factory` with a nested `sealed record NewArgs` and
  `CreateAsync(NewArgs)`; it reads references through reader ports, stages through the write port and never commits.
  Create-or-replace is `CreateAsync(...).RecoverConflictAsync(...)`
  (`scaffolds/Core/Scaffolds.ECommerce.Application/EmailValidation/Aggregates/ValidationCodeFactory.cs`).
- **Value objects:** `[ValueObject] public readonly partial record struct` (AxisTypes) with a private constructor
  that validates and throws — the only throw site in the core — and a private `Value`. The cast from string throws;
  at a boundary parse with `TryParse` or the validator helpers.
- **Codes, not enums:** `public static class {X}Codes` of `public const string` values
  (`scaffolds/Core/Scaffolds.ECommerce.SharedKernel/Authorization/PermissionCodes.cs`).

## Persistence

- **Ports:** `I{Entities}ReaderPort` and `I{Entities}WritePort` in the driven contracts, returning
  `Task<AxisResult[<T>]>` over property interfaces, ids as value objects, no `Save` member.
- **Repository:** `internal sealed`, composing `IAxisDbRepository` (AxisRepository) by constructor injection — never
  a base class. SQL that differs per provider goes behind a per-entity `I{Entity}SqlDialect`, one implementation per
  provider project (`.../Authorization/Members/MembersRepository.cs`, `IMemberSqlDialect.cs`).
- **Rows:** `internal sealed record {Entity}DbEntity : I{Entity}EntityProperties` with a static ordinal
  `FromReader`, reading in the order of `{Entities}Columns.All`, the single source of column names
  (`.../Catalog/Products/ProductDbEntity.cs`, `ProductsColumns.cs`).
- **SQL:** hand-written and parameterized, no ORM; bind every runtime value, interpolate only constants and dialect
  members. Identifiers are `UPPER_SNAKE_CASE`, tables plural, one schema per bounded context.
- **DDL:** declared once as an `AxisTable` (`{Entities}Table.Define()`), rendered per dialect; one `{BC}DbInit` per
  context; `DatabaseMigratorAdapter` runs them. Migrations never run at startup: use the host's `migrate` verb
  (`scaffolds/Host/Scaffolds.ECommerce.Host/Persistence/MigrateCommand.cs`) or the admin endpoint.
- **Unit of work:** `IUnitOfWork` exposes `SaveChangesAsync` and `RenewAsync` only. **One `SaveChangesAsync` per use
  case**, inside the chain, owned by the command handler, saga stage, event handler or service entry point; queries,
  validators, factories and ports never commit. `RenewAsync` only before a locking read-then-act.
- **No `try/catch` in repositories:** pass the duplicate-key and not-found codes from `{Aggregate}Errors` to
  `IAxisDbRepository` and let it classify the failure.
- **Both providers:** every persistence change ships and is tested on Postgres and MySQL; concurrency code renews,
  locks by key (`SELECT ... FOR UPDATE`) and is proven on both engines.

## Between bounded contexts

- A context calls another through its **facade** (synchronous), a **bus event** (asynchronous, fire-and-forget) or a
  **saga** (a process with compensation). Never inject another context's ports.
- **Bus event:** `public sealed record {Thing}{PastVerb}Event(...) : IAxisEvent` in the shared kernel, ids only, a
  snake_case `Topic` constant. Publish inside the chain that persists the change, **before** `SaveChangesAsync`
  (outbox). A consumer parses the ids, logs a failure and returns Ok — it never fails the publisher.
- **Cache-aside:** a named service over `IAxisCache` with a namespaced key and a fixed TTL, `GetOrCreateAsync` on
  read; mutators publish an event and an event handler evicts with `RemoveAsync`
  (`.../Authorization/Services/GrantsCache/GrantsCacheService.cs`, `.../Authorization/Events/GrantsUpdatedEventHandler.cs`).
- **Saga** (AxisSaga): an `internal static {Saga}Saga` with the DSL in `Configure`, a primitive accumulator payload,
  idempotent `IAxisSagaStageHandler<T>` stages that evolve the payload with `with` and compensate only what the run
  did; started with `IAxisSagaMediator.StartAsync`, answered at the edge with 202 and polled
  (`scaffolds/Core/Scaffolds.ECommerce.Application/EmailValidation/Sagas/ValidateEmail/`).
- **Infrastructure ports** owned by the application are small interfaces returning `AxisResult`; only their adapter
  holds a `try/catch` (`scaffolds/Contracts/Scaffolds.ECommerce.Contracts.Driven/Auth/IAccessTokenSignerPort.cs`).
  Over a framework provider package, add no `try/catch`, `Try` or retry of your own.

## HTTP edge

- **Controller:** `sealed`, `ControllerBase`, primary constructor injecting **only facades**; attributes
  `[ApiController]`, `[Tags]`, `[Authorize]`, `[ApiVersion("1.0")]`,
  `[Route("api/v{version:apiVersion}/{plural-kebab}")]`. One expression-bodied `{Verb}{Noun}Async` action per use
  case, returning `Task<IActionResult>`.
- **Render, never branch:** every action ends in `HttpContext.SendAsync(...)` (AxisResult.HttpResponse), or
  `SendAcceptedAsync(...)` for a saga; never `if (result.IsSuccess)` or `Match` in a controller.
- **Route values** bind as `string` and merge with `command with { Id = id }`; that command property is
  `[JsonIgnore]` so the body cannot supply it
  (`scaffolds/Contracts/Scaffolds.ECommerce.Contracts.Driving/Catalog/Products/v1/CheckoutProduct/CheckoutProductCommand.cs`).
- **OpenAPI:** full `///` docs on controllers and actions (`<summary>`, `<param>`, a `<response>` per status, error
  codes included) and a matching `ProducesResponseType` for each; the document at `/scalar` is the documentation hub
  (`scaffolds/Host/Scaffolds.ECommerce.Host/OpenApi/ECommerceOpenApi.cs`).
- **Auth:** the session JWT is the default scheme; external identity providers are accepted only on the session
  exchange. Permissions use `[RequirePermission(PermissionCodes.X)]` over one global filter backed by the
  authorization facade (`scaffolds/Host/Scaffolds.ECommerce.Host/Authorization/PermissionAuthorizationFilter.cs`).
- **Saga endpoints:** POST answers 202 with the run id, `Location` and `Retry-After`; GET polls the run; POST
  `{id}/cancel` cancels it (`scaffolds/Host/Scaffolds.ECommerce.Host/Controllers/EmailValidation/EmailValidationsV1Controller.cs`).

## Tests

| Project | Proves | Example |
|---|---|---|
| `ArchitectureTests` | the dependency boundaries, by reflection | `scaffolds/Tests/Scaffolds.ECommerce.ArchitectureTests/HexagonalBoundaryTests.cs` |
| `UnitTests` | a use case **through its facade** over a `ServiceProvider` with the real core, mocking only driven ports | `scaffolds/Tests/Scaffolds.ECommerce.UnitTests/CatalogTestHost.cs` |
| `IntegrationTests` | repositories, unit of work, sagas and journeys against real Postgres and MySQL (Testcontainers) | `scaffolds/Tests/Scaffolds.ECommerce.IntegrationTests/DualDbFixture.cs`, `DualDbServices.cs` |
| `E2ETests` | the HTTP journey through the host (`WebApplicationFactory<Program>`) | `scaffolds/Tests/Scaffolds.ECommerce.E2ETests/Fixtures/Factories/ECommerceWebAppFactory.cs` |

- Name tests `{WhatItDoes}When{Condition}Async`; Arrange-Act-Assert separated by blank lines; no comments, no tags.
- Packages: xUnit v3, `Axis.Testing.XUnit` (`ShouldSucceed`, `ShouldFailWithCode`), Moq, Testcontainers. Never
  FluentAssertions. Assert on typed error codes, never on messages.
- Seed entities with `internal sealed record Fake{Entity}(...) : I{Entity}EntityProperties`.
- Integration: one fixture per collection, migrations once, a fresh DI scope per journey step, a `[Theory]` per
  provider for parity and concurrency.
- E2E: every controller has a happy path and a 401, plus a 403 when it is permission-gated; sagas are asserted as
  202 then polled. The suite runs on the provider pinned in `ECommerceProviderPin.DefaultProvider`.

## Style

- Technical verbs in English, domain terms in the solution's language, use cases verb-first; every I/O method ends
  in `Async`.
- `internal sealed` by default. Public only for contracts, ports, facade interfaces, errors classes, options, the
  shared kernel and the project's `DependencyInjection`. Concrete classes are sealed.
- One `public static {Aggregate}Errors` beside the aggregate's ports, holding `static readonly AxisError` members
  with stable `UPPER_SNAKE_CASE` codes (`scaffolds/Contracts/Scaffolds.ECommerce.Contracts.Driven/Catalog/Products/ProductErrors.cs`);
  shared codes in `scaffolds/Core/Scaffolds.ECommerce.SharedKernel/Shared/SharedErrors.cs`. Never rebuild an error at
  the call site.
- No `//` comments and no `#region`. `///` only on what renders in OpenAPI (controllers, request and response
  contracts).
- Configuration is layered: committed defaults in `appsettings.json`, local values in `appsettings.Development.json`,
  credentials in user-secrets (listed in `secrets.template.json`), environment variables in containers. Provider
  switches (`Database:Provider`, `Saga:Provider`) are read only by the composition root
  (`scaffolds/Host/Scaffolds.ECommerce.Host/Persistence/PersistenceConfiguration.cs`).

## Smells and what replaces them

| Instead of… | Write… |
|---|---|
| a `CancellationToken` or user id parameter on a handler, facade or port | the ambient `IAxisMediator` context |
| a controller injecting `IAxisMediator`, a port or a service | a facade |
| `if (result.IsSuccess)` / `Match` in a controller | `HttpContext.SendAsync(...)` |
| `if (r.IsFailure) return r;`, `.Value`, `try/catch` inside a handler | one railway chain (`Then`, `Map`, `Ensure`, `Recover*`) |
| `SaveChangesAsync` in a repository, factory or validator, or twice in a use case | one commit in the use case's chain |
| publishing an event after `SaveChangesAsync` | publish before it, inside the same chain |
| injecting another bounded context's reader or write port | its facade, a bus event or a saga |
| an ORM, string-built SQL or a `try/catch` in a repository | parameterized SQL through `IAxisDbRepository` and error codes |
| an `enum` for a stored status or permission | a `{X}Codes` class of constants |
| a domain type or value object in a command or response | a primitive (ids as strings), parsed in the validator |
| migrating the database at startup | the `migrate` verb or the admin endpoint |
| `Common/` or `Helpers/` folders, namespaces that differ from the folder | `{BC}/{Aggregate}/{Feature}` with the namespace equal to the path |
| a public handler or implementation | `internal sealed` |
| FluentAssertions, or a test named after a rule | xUnit `Assert` and `Axis.Testing.XUnit`, `{WhatItDoes}When{Condition}Async` |

## Before you hand work back

1. `dotnet build <solution> -c Release -warnaserror` — zero warnings. The analyzers shipped with the packages turn
   these conventions into build errors: `AXIS0001`–`AXIS0008` (AxisResult), `AXIS0200`–`AXIS0201` (AxisTypes),
   `AXIS0300` (AxisBus), `AXIS0400`–`AXIS0403` (AxisMediator.Contracts) and `AXIS0600`–`AXIS0608`
   (`Axis.Conventions.Analyzers`). Fix the code; never suppress them with `#pragma` or `.editorconfig`.
2. `dotnet test --solution <solution> -c Release`, with Docker running for the integration and E2E tests.
3. Then review the diff for what no analyzer decides: the dependency direction, the folder and namespace layout,
   one commit per use case, events before the commit, no cross-context ports, E2E coverage for every new or changed
   controller.
