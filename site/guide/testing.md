# Testing

An Axis application is tested in **four projects, each with one job**: architecture tests pin the dependency
boundaries, unit tests drive the core through its facades, integration tests prove persistence and sagas against
real databases, and end-to-end (E2E) tests drive the full HTTP pipeline. A new test goes into the **lowest** project
that can prove the behavior — anything a cheaper layer can assert does not belong higher up.

The reference application's suite lives in `scaffolds/Tests/` (one folder per project, linked in the
[pyramid table](#the-four-project-pyramid)) and runs on both supported databases (Postgres and MySQL). The test kit
[`Axis.Testing.XUnit`](https://github.com/axis-sys/axis-testing) supplies the `AxisResult` assertions.

## Map

| Section | You want to… |
|---|---|
| [Running the suite](#running-the-suite) | build and run every test |
| [The four-project pyramid](#the-four-project-pyramid) | decide where a test belongs |
| [Naming](#naming) | name a test method and class |
| [Arrange-Act-Assert](#arrange-act-assert) | lay out a test and assert a failure |
| [Allowed packages](#allowed-packages) | know which test libraries may be referenced |
| [Unit tests through the facade](#unit-tests-through-the-facade) | test a use case without I/O |
| [Fake entity property records](#fake-entity-property-records) | build domain data when entities are internal |
| [Integration fixtures](#integration-fixtures) | share Testcontainers databases across a collection |
| [The DI provider helper](#the-di-provider-helper) | compose a production-like container in a test |
| [A fresh scope per journey step](#a-fresh-scope-per-journey-step) | prove a multi-step flow really commits |
| [Dual-database parity and concurrency](#dual-database-parity-and-concurrency) | prove behavior on every supported engine |
| [End-to-end tests with WebApplicationFactory](#end-to-end-tests-with-webapplicationfactory) | stand up the real host against its own container |
| [Provider pinning](#provider-pinning) | choose the database the E2E suite runs on |
| [Quiet logging](#quiet-logging) | keep parallel host disposal from making tests flaky |
| [The controller coverage gate](#the-controller-coverage-gate) | know what E2E tests a controller needs |
| [Saga endpoints: 202 and polling](#saga-endpoints-202-and-polling) | test a saga the way a client consumes it |

---

## Running the suite

From the repository root:

```
dotnet build AxisFramework.slnx -c Release -warnaserror
dotnet test --solution AxisFramework.slnx -c Release
```

- **Docker must be running**: the integration and E2E projects start their databases with Testcontainers.
- The E2E suite runs on the **pinned provider** (`ECommerceProviderPin.DefaultProvider`, Postgres by default); the
  other provider's collection is reported as **skipped**, visibly, with the reason. See
  [Provider pinning](#provider-pinning).
- Build before testing: the architecture tests load the host assembly from its build output, and `-warnaserror`
  turns every analyzer warning into a failure.

## The four-project pyramid

| Project | Job | References | Never |
|---|---|---|---|
| [`ArchitectureTests`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Tests/Scaffolds.ECommerce.ArchitectureTests/HexagonalBoundaryTests.cs) | Executable dependency and boundary assertions; runs no application code | the core, the driving contracts, the facade adapter (the host is loaded by name from its build output) | executes a use case |
| [`UnitTests`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Tests/Scaffolds.ECommerce.UnitTests/Scaffolds.ECommerce.UnitTests.csproj) | Handlers, validators and domain rules, driven through the facades | the core (application, domain, kernel, contracts) plus the facade driving adapter | opens a database connection, a socket or an HTTP client; references a driven adapter or the host |
| [`IntegrationTests`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Tests/Scaffolds.ECommerce.IntegrationTests/Scaffolds.ECommerce.IntegrationTests.csproj) | Ports, facades, repositories and sagas against Testcontainers databases | the core, the facade adapter, the driven adapters, the framework store packages | boots the web host |
| [`E2ETests`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Tests/Scaffolds.ECommerce.E2ETests/Scaffolds.ECommerce.E2ETests.csproj) | The full HTTP pipeline over `WebApplicationFactory<Program>`: routing, auth, controllers, sagas | the host (the only project that does) and `Microsoft.AspNetCore.Mvc.Testing` | mocks the pipeline |

Where coverage of each piece comes from:

- **Driven adapters** (repositories, SQL dialects, token signers) are never unit-tested; integration and E2E tests
  cover them against real containers.
- The **facade driving adapter** carries zero logic and is exercised implicitly by every facade-driven test.
- The E2E project asserts against the `{Aggregate}Errors` members (and edge-owned errors) it reaches through its host
  reference, never against literal codes, and does not reference the application for them.
- The E2E project may also run the host's command line: the reference application runs the `migrate` verb as a
  process against an empty container
  ([`MigrateCommandTests.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Tests/Scaffolds.ECommerce.E2ETests/UseCases/v1/Admin/MigrateCommandTests.cs)).

The architecture tests of the reference application are plain xUnit facts over reflection —
`Assembly.GetReferencedAssemblies()` and `GetTypes()` — such as "the domain references only the shared kernel" or
"every controller constructor parameter is a facade"
([`FacadeBoundaryTests.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Tests/Scaffolds.ECommerce.ArchitectureTests/FacadeBoundaryTests.cs)).
Because only the E2E project may reference the host,
[`HostAssembly.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Tests/Scaffolds.ECommerce.ArchitectureTests/HostAssembly.cs)
loads it by name from its build output. The boundaries themselves are described in [Architecture](architecture.md).

## Naming

| Element | Convention | Example from the reference application |
|---|---|---|
| Test method | PascalCase `{WhatItDoes}When{Condition}`, no underscores | `FailsWithConflictWhenSkuAlreadyExistsAsync` |
| Asynchronous test | `Async` suffix | `RenewsTheTransactionBeforeTheLockingReadWhenCheckingOutAsync` |
| Synchronous test | no suffix | `KeepsTheUpperCaseCanonicalFormWhenTypedInLowerCase` |
| Test class | `{Subject}Tests`, after the unit or journey it covers | `CheckoutProductHandlerTests`, `ProductsRepositoryDualDbTests` |

The name reads as the specification of the case: what is asserted, and under which condition. It is the only
statement of what a test proves — a test carries no comment and no traceability tag. A snake_case sentence is a
divergence to correct, not an alternative; the conventions analyzers suggest the fix (`AXIS0603`).

## Arrange-Act-Assert

Every test has an arrangement, **one action** (or a named journey of actions) and the assertions, separated by
**blank lines** — no `// Arrange` comments, no comments at all.

```csharp
[Fact]
public async Task FailsWithInsufficientStockWhenBelowRequestedAsync()
{
    var productId = ProductId.New;
    ProductsWrite.Setup(p => p.GetByIdForUpdateAsync(productId))
        .ReturnsAsync(AxisResult.Ok<IProductEntityProperties>(new FakeProduct(productId, (Sku)"SKU-1", "Keyboard", Stock: 0)));
    var facade = Build<IProductsFacade>();

    var result = await facade.CheckoutProductAsync(new CheckoutProductCommand { CartId = CartId.New, ProductId = productId, Quantity = 1 });

    result.ShouldFailWithCode(ProductErrors.InsufficientStock.Code);
    ProductsWrite.Verify(p => p.ReserveStockAsync(It.IsAny<ProductId>(), It.IsAny<int>()), Times.Never);
}
```

- Repeated arrangement moves into **private static helpers** of the test class (`Build(...)`, `Command()`,
  `Request(...)`, `SeedAsync(...)`) or into the shared test host.
- Assertions use the **built-in xUnit `Assert` API** (`Assert.Equal`, `Assert.Contains`, `Assert.Single`,
  `Assert.All`, `Assert.True(result.IsSuccess)`) and the **`Axis.Testing.XUnit`** result assertions (`ShouldSucceed`,
  `ShouldSucceedWith`, `ShouldFail`, `ShouldFailWithCode`, `ShouldFailWithType`, and their `…Async` forms).
- A failure path asserts the **typed error code** (`ShouldFailWithCode(ProductErrors.InsufficientStock.Code)` or
  `Assert.Contains(result.Errors, e => e.Code == ...)`), never a message string.
- Journey tests (integration and E2E) chain several act + assert blocks in one method; each step still keeps its
  own act/assert grouping.

Reference: [`CheckoutProductHandlerTests.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Tests/Scaffolds.ECommerce.UnitTests/CheckoutProductHandlerTests.cs).

## Allowed packages

| Package | Where |
|---|---|
| xUnit v3 (`xunit.v3`, `xunit.runner.visualstudio`), `Microsoft.NET.Test.Sdk`, `coverlet.collector` | every test project |
| [`Axis.Testing.XUnit`](https://github.com/axis-sys/axis-testing) | every test project |
| `Moq` | unit and integration projects — the only mocking library |
| `Testcontainers.PostgreSql`, `Testcontainers.MySql` | only the projects that boot a real database: integration and E2E |
| An architecture-invariant library (NetArchTest-style), if you use one | only the architecture project |
| `Microsoft.AspNetCore.Mvc.Testing` | only the E2E project |
| FluentAssertions | **banned** — in no csproj and in no `using` under the test tree |

- Every test project is on the **xUnit v3** line, never xUnit 2.x. Under v3, `IAsyncLifetime` returns `ValueTask`,
  and cancellation flows through `TestContext.Current.CancellationToken`, which the E2E project passes to every
  `HttpClient` call.
- Every assertion goes through xUnit's `Assert` or the kit's assertions, which throw the native `XunitException`.
- Versions are pinned centrally (`Directory.Packages.props`); test csprojs carry no `Version` attribute.
- In the reference application the test projects are marked by
  [`scaffolds/Tests/Directory.Build.props`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Tests/Directory.Build.props),
  which sets `AxisTestProject` before the shared build properties are read — never a property in the `.csproj`,
  which is read after them. The shared build properties then add the common references (xUnit v3, the runner, the test SDK, coverlet, `Axis.Testing.XUnit`)
  and the global usings `Xunit`, `Axis` and `Axis.Testing`; each csproj lists only what is specific to its layer.

**Exceptions**: a test project may reference the real in-memory adapter of an infrastructure port (cache, bus, email)
to exercise the real implementation instead of a mock — the integration and E2E projects use the in-memory email
adapter. Validator unit tests may consume the validation library's `ValidationResult` transitively to assert error
codes, but never build validation rules in a test.

## Unit tests through the facade

A unit test exercises the core by building a `ServiceProvider` with the **real application registration plus the
facade adapter registration** and driving the use case **through its facade**, so the real mediator pipeline —
validation and behaviors — runs. The only test doubles are the **driven ports**: repository ports and the `IAxis*`
infrastructure ports. Driving adapters are never mocked.

A shared test host per area does the wiring
([`CatalogTestHost.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Tests/Scaffolds.ECommerce.UnitTests/CatalogTestHost.cs),
[`AuthTestHost.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Tests/Scaffolds.ECommerce.UnitTests/AuthTestHost.cs)):

1. Every driven port the pipeline needs is a `Mock<T>` with a **permissive default** set up in the constructor, so
   the whole application dependency chain provisions.
2. A test overrides **only the port behavior it exercises**, then calls `Build<TFacade>()`; ports it does not touch
   keep the default.
3. `Build` composes the container, sets the ambient identity and resolves the facade from a scope:

```csharp
protected TFacade Build<TFacade>(Action<IServiceCollection>? configure = null) where TFacade : notnull
{
    var services = new ServiceCollection()
        .AddLogging()
        .AddECommerceApplication()
        .AddECommerceFacade()
        .AddSingleton(Options.Create(Catalog))
        .AddSingleton(ProductsReader.Object)
        .AddSingleton(ProductsWrite.Object)
        .AddSingleton(OrdersReader.Object)
        .AddSingleton(OrdersWrite.Object)
        .AddSingleton(CartItemsReader.Object)
        .AddSingleton(CartItemsWrite.Object)
        .AddSingleton(Bus.Object)
        .AddSingleton(UnitOfWork.Object);
    configure?.Invoke(services);
    var provider = services.BuildServiceProvider();

    provider.GetRequiredService<IAxisMediatorContextAccessor>().AxisEntityId = Identity;
    return provider.CreateScope().ServiceProvider.GetRequiredService<TFacade>();
}
```

What is not obvious:

- **`AddLogging()` is required** — the mediator resolves a logger on every dispatch.
- The pipeline needs a **`TimeProvider`** (the application registration adds `TimeProvider.System` when none is
  registered) and an **`IAxisTelemetry`** whenever the telemetry behavior is wired (register
  `NullAxisTelemetry.Instance` from [`AxisTelemetry`](https://github.com/axis-sys/axis-telemetry)).
- The mediator and the facades are scoped: resolve the facade **inside a scope**.
- **Identity is ambient**, set through `IAxisMediatorContextAccessor.AxisEntityId`
  ([`AxisMediator.Contracts`](https://github.com/axis-sys/axis-mediator-contracts)); a test sets `Identity = null`
  to exercise the anonymous path.
- Side effects are asserted with `Mock.Verify(...)`.
- A test may add an **observer pipeline behavior** that only reads the pipeline context and replaces no dependency
  ([`ActorProbeBehavior.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Tests/Scaffolds.ECommerce.UnitTests/ActorProbeBehavior.cs),
  used by
  [`ActorStampBehaviorTests.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Tests/Scaffolds.ECommerce.UnitTests/ActorStampBehaviorTests.cs));
  the doubles stay on the driven ports.

Driving through the facade — not calling the handler directly — is what keeps validation and the behaviors honest.
Pure value-object rules can be asserted directly
([`SkuTests.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Tests/Scaffolds.ECommerce.UnitTests/SkuTests.cs)).

## Fake entity property records

Domain entities are internal, so tests cannot construct them. Test data is a **sealed positional record**
`Fake{Entity}` implementing the public `I{Entity}EntityProperties` interface, one constructor parameter per
interface property, **in interface order**. Write ports accept the properties interface, so the fake feeds
repositories and factories exactly like the real entity: canned reader results in unit tests, rows seeded through
write ports in integration tests.

```csharp
internal sealed record FakeProduct(
    ProductId ProductId,
    Sku Sku,
    string Name,
    int Stock
) : IProductEntityProperties;
```

- Declare the fakes close to the tests that use them, inside the test project — the reference application keeps
  one file per entity in each project that needs it
  ([`UnitTests/FakeProduct.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Tests/Scaffolds.ECommerce.UnitTests/FakeProduct.cs),
  [`IntegrationTests/FakeProduct.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Tests/Scaffolds.ECommerce.IntegrationTests/FakeProduct.cs)).
  Never add them to the internal domain assembly.
- Never construct, or reflect into, the internal domain entity types.
- Fakes of ports or facades swapped into a container (a failing write port, a fake unit of work) are a different
  concern and may live in a shared fakes folder.

The properties interface and the internal record it hides are described in [Domain](domain.md).

## Integration fixtures

Integration tests share their database containers through an **xUnit collection fixture**: one container (per
engine) and one migration run per collection keeps the suite fast and hermetic.

```csharp
public async ValueTask InitializeAsync()
{
    await Task.WhenAll(_postgres.StartAsync(), _mysql.StartAsync());
    await Task.WhenAll(MigrateAsync(DbProvider.Postgres), MigrateAsync(DbProvider.MySql));
}
```

```csharp
[CollectionDefinition(Name)]
public sealed class DualDbCollection : ICollectionFixture<DualDbFixture>
{
    public const string Name = "DualDbECommerceCollection";
}

[Collection(DualDbCollection.Name)]
public sealed class ProductsRepositoryDualDbTests(DualDbFixture fixture)
```

- A sealed `{X}Fixture : IAsyncLifetime` starts the containers and runs **all migrations exactly once** in
  `InitializeAsync` — the business schemas through the application's migrator port, then the framework stores (saga,
  bus, cache).
- A `[CollectionDefinition]` class binds the fixture with `ICollectionFixture<>`; test classes join with
  `[Collection(...)]`.
- Containers use **explicit image tags** (`postgres:18.6`, `mysql:8.4.10`) and automatic clean-up
  (`WithCleanUp(true)`).
- A multi-container fixture starts its containers **in parallel** with `Task.WhenAll`.
- The connection string **caps the pool** (`MaxPoolSizeWithinContainerConnectionLimit`) so the application and saga
  pools stay under the container's connection limit during fan-out.
- Tests isolate with **unique keys** per test (Guid v7 ids, `TestData.NewSku()`, `TestData.NewEmail()`), never by
  re-migrating or truncating between tests.

References: [`DualDbFixture.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Tests/Scaffolds.ECommerce.IntegrationTests/DualDbFixture.cs),
[`DualDbCollection.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Tests/Scaffolds.ECommerce.IntegrationTests/DualDbCollection.cs),
[`TestData.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Tests/Scaffolds.ECommerce.IntegrationTests/TestData.cs).
An E2E factory may itself be the collection fixture (factory plus container in one class), see
[below](#end-to-end-tests-with-webapplicationfactory).

## The DI provider helper

Integration tests build a real container per test from **one shared helper that mirrors production composition**,
instead of re-wiring it in every class
([`DualDbServices.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Tests/Scaffolds.ECommerce.IntegrationTests/DualDbServices.cs)):

| Helper | Composes |
|---|---|
| `DualDbServices.Build(fixture, provider)` | a **repository-only** container: the provider adapter, with a stubbed ambient mediator |
| `DualDbServices.BuildFullStack(fixture, provider, configure)` | the **full stack** as production wires it: application, facades, provider adapter, bus, cache and saga stores — with an `Action<IServiceCollection>` seam to swap one service |

```csharp
var mediator = new Mock<IAxisMediator>();
mediator.SetupGet(m => m.CancellationToken).Returns(CancellationToken.None);
mediator.SetupGet(m => m.TraceId).Returns("trace");
mediator.SetupGet(m => m.OriginId).Returns("origin");
services.AddSingleton(mediator.Object);
services.AddSingleton(TimeProvider.System);
services.AddSingleton<IAxisTelemetry>(NullAxisTelemetry.Instance);
```

- Any container that drives the Axis mediator and its behavior pipeline registers **`IAxisTelemetry`** (the null
  instance) and **`TimeProvider.System`** — the pipeline fails at run time without them.
- A repository-only container **stubs the ambient mediator** (canned trace and origin ids, `CancellationToken.None`)
  instead of booting the real one. Cancellation stays ambient, never a parameter.
- Ambient request context is bound to a fixed test value: the caller identity through
  `IAxisMediatorContextAccessor.AxisEntityId`, and — in a multi-tenant application — the current tenant. It is
  ambient, never a command field.
- Test-tuned settings are injected as singletons, and the name of the settings or of the test says why they differ
  from production.
- The `configure` seam swaps one service without forking the composition, for instance a write port that fails, to
  drive a saga into compensation.

## A fresh scope per journey step

A journey test (a multi-step business flow without HTTP) opens a **new `IServiceScope` for every step**. Each scope
gets its own unit of work and connection, so the journey proves that the data actually committed between steps
instead of riding one long-lived transaction that never commits.

```csharp
private static async Task<string> ValidateEmailAsync(ServiceProvider services, string code)
{
    using var scope = services.CreateScope();
    var emailValidation = scope.ServiceProvider.GetRequiredService<IEmailValidationFacade>();

    return (await emailValidation.ValidateEmailAsync(new ValidateEmailCommand { Code = code })).ShouldSucceed().SagaId;
}
```

- Each step runs inside its own `using var scope = services.CreateScope()`; no facade or port outlives its scope.
- State carried between steps is plain data (ids, strings), never a resolved service or an entity.
- Business actions go through the `I{Entities}Facade` contracts; reading through a reader port is reserved for
  asserting the persisted shape in a final scope.
- The service provider is disposed per test: `await using var services = DualDbServices.BuildFullStack(...)`.

Reference: [`ValidateEmailSagaCompensationDualDbTests.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Tests/Scaffolds.ECommerce.IntegrationTests/ValidateEmailSagaCompensationDualDbTests.cs)
— the saga id is the only thing carried from the start step to the polling steps, and the reverted flag is read
back through `ICustomersReaderPort` at the end.

## Dual-database parity and concurrency

When the same code runs on more than one database by composition, behavior that could diverge across dialects is
asserted on **all** of them. Single-provider applications assert against their one database and need neither
pattern.

### Parity

Shared SQL seams, seeded lookups, JSON round-trips, counts, cascades, case sensitivity: any new or changed
repository behavior with a dialect risk gets a parity test — **one `[Theory]` body**, one `[InlineData]` per
provider, never two copies (drift hides in a forgotten copy).

```csharp
[Theory]
[InlineData(DbProvider.Postgres)]
[InlineData(DbProvider.MySql)]
public async Task FailsWithConflictWhenTheSkusDifferOnlyByCaseAsync(DbProvider provider)
```

- The test name (PascalCase, no underscore) states the behavior that must be identical and the risk being pinned
  — `KeepsTwoCustomersApartWhenTheirEmailsDifferOnlyByCaseAsync` pins a case-sensitivity divergence on a unique key.
- Data is unique per test and per provider, so every theory runs against the long-lived shared containers.
- A split of business and saga storage across engines has its own setup (the `sagaConnectionString` argument of
  `BuildFullStack`), since that split is per store rather than the same schema on both.

References: [`ProductsRepositoryDualDbTests.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Tests/Scaffolds.ECommerce.IntegrationTests/ProductsRepositoryDualDbTests.cs),
[`CustomersRepositoryDualDbTests.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Tests/Scaffolds.ECommerce.IntegrationTests/CustomersRepositoryDualDbTests.cs),
[`DbProvider.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Tests/Scaffolds.ECommerce.IntegrationTests/DbProvider.cs).

### Concurrency

A read-then-act invariant that holds under one engine's read-committed default can silently break under another's
snapshot / repeatable-read default: the snapshot is pinned at the transaction's first read, so a count taken after
acquiring a row lock still sees stale data. Only real parallelism and an exact assertion catch it.

```csharp
var results = await Task.WhenAll(
    Enumerable.Range(0, ConcurrentCheckouts).Select(_ => CheckoutOneUnitAsync(services, product.ProductId)));

Assert.Equal(InitialStock, results.Count(result => result.IsSuccess));
Assert.All(results.Where(result => result.IsFailure), result => result.ShouldFailWithCode(ProductErrors.InsufficientStock.Code));
Assert.Equal(0, await ReadStockAsync(services, product.ProductId));
```

- A **`[Theory]` over every supported provider**, never single-provider when more than one engine is supported.
- The concurrent phase is `Task.WhenAll` over N parallel calls, **each in its own DI scope** (own unit of work and
  connection).
- The test **replicates the production transaction shape**, including the first read that pins a snapshot — the
  surest way is to drive the production handler through the facade, as the reference application does. The test
  name says so; no comment does.
- The final assertion checks the invariant **exactly** (the number of successes, the final stock), not merely that
  no error was thrown.
- A single-provider precursor test may stay alongside the theory once a second engine is added.

References: [`CheckoutConcurrencyDualDbTests.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Tests/Scaffolds.ECommerce.IntegrationTests/CheckoutConcurrencyDualDbTests.cs),
[`EnsureCustomerConcurrencyDualDbTests.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Tests/Scaffolds.ECommerce.IntegrationTests/EnsureCustomerConcurrencyDualDbTests.cs).
The persistence traps these tests guard are described in [Persistence](persistence.md).

## End-to-end tests with WebApplicationFactory

E2E tests drive the **full production pipeline** — real routing, authentication, authorization, DI composition and
middleware — against a real database container, and assert what a client observes. They are the most expensive
layer: reserve them for the wire contract and the journeys.

The DB-backed factory is an abstract
[`ECommerceWebAppFactory`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Tests/Scaffolds.ECommerce.E2ETests/Fixtures/Factories/ECommerceWebAppFactory.cs)
: `WebApplicationFactory<Program>, IAsyncLifetime`, with one concrete subclass per provider that owns its
Testcontainers instance
([`PostgresECommerceWebAppFactory.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Tests/Scaffolds.ECommerce.E2ETests/Fixtures/Factories/PostgresECommerceWebAppFactory.cs),
[`MySqlECommerceWebAppFactory.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Tests/Scaffolds.ECommerce.E2ETests/Fixtures/Factories/MySqlECommerceWebAppFactory.cs)).
The factory is the collection fixture: one host and one container serve the whole collection.

```csharp
public async ValueTask InitializeAsync()
{
    var connectionString = await StartContainerAsync();
    _connectionString = connectionString;
    foreach (var (variable, value) in BootEnvironment(connectionString))
        Environment.SetEnvironmentVariable(variable, value);

    await MigrateFrameworkStoresAsync(connectionString);
    using var warmup = CreateClient();
    await MigrateThroughThePortAsync();
    await GrantTheStaffMembersAsync();
}
```

**Hermetic by construction.** Configuration read before `builder.Build()` only sees the process environment and
build-time configuration; a factory that only layers in-memory configuration afterwards can silently run against a
developer's real database. So:

- `InitializeAsync` starts the container and exports the connection string — and both provider switches
  (`Database__Provider`, `Saga__Provider`) — as **environment variables**; `DisposeAsync` resets each of them to
  `null` before stopping the container.
- The factory **runs every migration itself**, once, in `InitializeAsync` (framework stores, then the business
  schemas through the migrator port). The host never migrates at startup.
- Settings that must exist before `builder.Build()` (signing key, issuer, audience, token lifetime, bootstrap
  administrator ids) live in **one boot-configuration dictionary** in `ConfigureWebHost`, not in scattered
  `UseSetting` calls.
- The **saga runtime is wired exactly as in production** — the same `AddAxisSagaPostgres` / `AddAxisSagaMySql`
  call from the host, hosted resumer included
  ([`AxisSaga.Postgres`](https://github.com/axis-sys/axis-saga-postgres),
  [`AxisSaga.MySql`](https://github.com/axis-sys/axis-saga-mysql)). A test-only in-process or resumer-disabled saga
  would hide what the real worker catches (schema bootstrap, crash recovery).
- Container-fragile tuning is applied for the test run and named for its reason — the connection pool is capped by
  `MaxPoolSizeWithinContainerConnectionLimit`.

**Real authentication.** `ConfigureTestServices` re-points the external-provider schemes to a test handler
(`PostConfigure<AuthenticationOptions>` sets their `HandlerType` to
[`TestAuthHandler`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Tests/Scaffolds.ECommerce.E2ETests/Fixtures/Auth/TestAuthHandler.cs),
which reads an `X-Test-User` or `X-Test-Subject` header). The session-token scheme stays real: the factory obtains
bearer tokens by POSTing the actual token-exchange endpoint, cached per test user
(`ECommerceClientAsync(EUserType.Staff)`), and grants the staff members their permissions through the real API.

**Journeys as plain static methods.** Each use case has a `{UseCase}Tests` static class under
`UseCases/v1/{BoundedContext}/` whose methods take the factory; reusable steps live in `Fixtures/Journeys/`. A
coverage class per provider holds the collection and fans the journeys out as one-line `[Fact]`s (see
[Provider pinning](#provider-pinning)).

```csharp
public static async Task ReturnsForbiddenWhenTheCallerLacksThePermissionAsync(ECommerceWebAppFactory factory)
{
    var customer = await factory.ECommerceClientAsync(EUserType.NormalUser);

    var response = await customer.PostAsJsonAsync(Routes.Products, CatalogSteps.NewProduct(), Ct);

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    Assert.Equal([AuthorizationErrors.PermissionDenied.Code], await ProblemResponses.ErrorCodesAsync(response));
}
```

- Every `HttpClient` call passes `TestContext.Current.CancellationToken`.
- Routes come from one
  [`Routes.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Tests/Scaffolds.ECommerce.E2ETests/Fixtures/Http/Routes.cs);
  ProblemDetails codes are read with
  [`ProblemResponses.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Tests/Scaffolds.ECommerce.E2ETests/Fixtures/Http/ProblemResponses.cs).
- Composition checks that need no running host (an option block refusing to start, a provider switch) are plain
  facts in the E2E project, the only one that may reference the host
  ([`AuthenticationCompositionTests.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Tests/Scaffolds.ECommerce.E2ETests/AuthenticationCompositionTests.cs),
  [`PersistenceCompositionTests.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Tests/Scaffolds.ECommerce.E2ETests/PersistenceCompositionTests.cs)).

**Exception — a DB-less factory.** When the bounded context under test has no real repository at all, an E2E factory
may compose the host's in-memory driven adapter (or swap ports for fakes in `ConfigureTestServices`) instead of
owning a container.

## Provider pinning

The provider the E2E suite runs on is a **compile-time decision**, not an environment lookup. One shared constant
pins it, and each provider's coverage class derives a `const` skip reason from it, so a disabled provider's whole
collection skips **visibly**:

```csharp
internal static class ECommerceProviderPin
{
    public const string DefaultProvider = "Postgres";
}
```

```csharp
[Collection(PostgresECommerceWebAppCollection.Name)]
public sealed class PostgresCoverageTests(PostgresECommerceWebAppFactory factory)
{
    private const string? Skip = ECommerceProviderPin.DefaultProvider == "Postgres"
        ? null
        : "Pinned provider is " + ECommerceProviderPin.DefaultProvider + " (see ECommerceProviderPin.DefaultProvider).";

    [Fact(Skip = Skip)]
    public Task ReturnsForbiddenOnTheRegistrationWhenTheCallerLacksThePermissionAsync()
        => RegisterProductTests.ReturnsForbiddenWhenTheCallerLacksThePermissionAsync(factory);
}
```

- To run the E2E suite on MySQL, set `DefaultProvider = "MySql"`; the Postgres collection then skips.
- The provider collections carry **`DisableParallelization = true`**: the factory's initialization mutates
  process-wide environment variables, and xUnit runs different collections in parallel by default.

```csharp
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PostgresECommerceWebAppCollection : ICollectionFixture<PostgresECommerceWebAppFactory>
```

References: [`ECommerceProviderPin.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Tests/Scaffolds.ECommerce.E2ETests/Fixtures/Factories/ECommerceProviderPin.cs),
[`PostgresCoverageTests.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Tests/Scaffolds.ECommerce.E2ETests/PostgresCoverageTests.cs),
[`MySqlCoverageTests.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Tests/Scaffolds.ECommerce.E2ETests/MySqlCoverageTests.cs),
[`PostgresECommerceWebAppCollection.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Tests/Scaffolds.ECommerce.E2ETests/Fixtures/Factories/PostgresECommerceWebAppCollection.cs).

## Quiet logging

On Windows, `WebApplication.CreateBuilder` registers the EventLog logger provider by default. Its native handle is
disposed when a host is torn down; under parallel collections, hosts are created and disposed concurrently, the
disposal races with in-flight request logging, and the run surfaces an `ObjectDisposedException` (on `TestServer` or
`EventLogInternal`) wrapped by the developer exception page. The failure is flaky — it passes in isolation and only
lands under full parallel load — and it masks the real result. Tests consume no logger, so:

- Every `WebApplicationFactory<Program>` host calls `ClearProviders()`.
- A factory that inspects the logs clears the providers first, then adds **only its own** collecting provider.
- Inline throwaway factories, built and disposed inside one test, clear the providers too.

```csharp
builder.ConfigureLogging(logging => logging.ClearProviders().AddProvider(new CollectingLoggerProvider(ServerErrors)));
```

A host that deliberately asserts on a specific provider's output may add that one provider explicitly after
`ClearProviders()` — never keep the implicit defaults. Reference:
[`CollectingLoggerProvider.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Tests/Scaffolds.ECommerce.E2ETests/Fixtures/Factories/CollectingLoggerProvider.cs)
(collects warnings and errors into `factory.ServerErrors` for diagnostics).

## The controller coverage gate

A new or changed controller is **not done** without its E2E journeys. Per controller surface:

| Case | Required | Example (`RegisterProductTests`) |
|---|---|---|
| Happy path | always | `RegistersTheProductWhenTheMemberHoldsThePermissionAsync` |
| Anonymous request → **401** | at least one per surface | `ReturnsUnauthorizedWhenTheCallerIsAnonymousAsync` |
| Authenticated without the permission → **403** | permission-guarded endpoints | `ReturnsForbiddenWhenTheCallerLacksThePermissionAsync` |
| Privileged bypass (administrator) | permission-guarded endpoints | `RegistersTheProductWhenTheCallerIsAnAdministratorAsync` |
| Foreign tenant header → **404** | multi-tenant applications only | — |

- Each controller's use cases have a matching `{UseCase}Tests` (or `{Subject}Tests`) file of static journeys under
  `UseCases/v1/{BoundedContext}/`, fanned out as `[Fact]`s by the coverage class that holds the shared collection.
- E2E tests exercise the **real pipeline** — routing, auth and DI composition, against a real database container
  where the application has one (the in-memory driven adapter otherwise) — never a mocked one.
- A **reflective composition test** activates every `ControllerBase` subtype from the production registrations and
  stays green, catching a missing DI registration before any request does:

```csharp
public static void ActivatesEveryControllerWhenResolvedFromTheProductionContainer(ECommerceWebAppFactory factory)
{
    var controllerTypes = typeof(Host.Program).Assembly.GetTypes()
        .Where(type => typeof(ControllerBase).IsAssignableFrom(type) && !type.IsAbstract)
        .ToArray();
    using var scope = factory.Services.CreateScope();

    Assert.NotEmpty(controllerTypes);
    foreach (var controllerType in controllerTypes)
        Assert.NotNull(ActivatorUtilities.CreateInstance(scope.ServiceProvider, controllerType));
}
```

A read-only endpoint with no permission attribute needs only the happy path and a 401. The reference application also
pins its OpenAPI document (every operation, its summary and its response codes) at this layer
([`OpenApiDocumentTests.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Tests/Scaffolds.ECommerce.E2ETests/Documentation/OpenApiDocumentTests.cs)).
How authentication and authorization are wired is an application choice ([HTTP edge](edge.md)); the gate only
requires the rejection paths to be exercised.

References: [`RegisterProductTests.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Tests/Scaffolds.ECommerce.E2ETests/UseCases/v1/Catalog/RegisterProductTests.cs),
[`CompositionTests.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Tests/Scaffolds.ECommerce.E2ETests/Fixtures/CompositionTests.cs).

## Saga endpoints: 202 and polling

Saga-backed endpoints are tested **exactly as clients consume them**: the start answers 202 with a saga id, the test
polls the run resource until a terminal status, then asserts the terminal payload.

```csharp
var response = await customer.PostAsJsonAsync(Routes.EmailValidations, new ValidateEmailCommand { Code = code }, Ct);

await HttpAssert.StatusAsync(HttpStatusCode.Accepted, response, factory.ServerErrors);
var accepted = await response.Content.ReadFromJsonAsync<ValidateEmailResponse>(Ct);
Assert.NotNull(accepted);
Assert.Equal(nameof(AxisSagaStatus.Pending), accepted.Status);
Assert.Equal(Routes.EmailValidationRun(accepted.SagaId), response.Headers.Location?.OriginalString);
Assert.NotNull(response.Headers.RetryAfter);
var run = await EmailValidationSteps.PollUntilTerminalAsync(factory, staff, accepted.SagaId);
```

- The accept assertion checks **202 Accepted and a pending-status body** (and here the `Location` and
  `Retry-After` headers) **before** any polling.
- Polling runs in **one bounded helper**
  ([`SagaPolling.PollUntilAsync`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Tests/Scaffolds.ECommerce.E2ETests/Fixtures/Http/SagaPolling.cs):
  a maximum number of attempts, a 404 read as "not visible yet"); no bare `Task.Delay` loops are scattered through
  test bodies.
- Terminal assertions verify the terminal status, the counts and the per-item outcomes; failed items are matched
  against the `{Aggregate}Errors` (or shared errors) members, never against string literals.
- Response bodies deserialize into the **production driving-contract records** (`ValidateEmailResponse`,
  `GetValidationStatusResponse`): the wire is pinned by the same types clients compile against.
- On an unexpected status the test fails **with the response body and the collected server logs**
  ([`HttpAssert.StatusAsync`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Tests/Scaffolds.ECommerce.E2ETests/Fixtures/Http/HttpAssert.cs)),
  as does a poll that never reaches its state.
- To catch a run in flight (to cancel it while it runs), the reference application holds a row lock the stage
  needs — `factory.LockCustomerAsync(customerId)` over
  [`RowLock.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Tests/Scaffolds.ECommerce.E2ETests/Fixtures/Factories/RowLock.cs)
  — instead of racing a timer
  ([`CancelValidationTests.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Tests/Scaffolds.ECommerce.E2ETests/UseCases/v1/EmailValidation/CancelValidationTests.cs)).

A test that deliberately pins raw bytes to detect a contract divergence may declare a local record for that single
assertion. References:
[`ValidateEmailTests.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Tests/Scaffolds.ECommerce.E2ETests/UseCases/v1/EmailValidation/ValidateEmailTests.cs),
[`EmailValidationSteps.cs`](https://github.com/axis-sys/axis-framework-dotnet/blob/main/scaffolds/Tests/Scaffolds.ECommerce.E2ETests/Fixtures/Journeys/EmailValidationSteps.cs).
The endpoints under test are described in [HTTP edge](edge.md#saga-endpoints).

## See also

- [HTTP edge](edge.md) — the controllers, authentication and saga endpoints these tests exercise.
- [Architecture](architecture.md) — the boundaries the architecture tests pin.
- [Persistence](persistence.md) — the repositories and dialect seams the integration tests prove.
- [Process](process.md) — the build and review gates the suite runs under.
- [`Axis.Testing.XUnit`](https://github.com/axis-sys/axis-testing) · [`AxisResult` docs](https://github.com/axis-sys/axis-result/blob/main/docs/index.md)
